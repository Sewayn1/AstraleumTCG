using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Astraleum
{
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance;

        /// <summary>
        /// Vrai pendant toute la durée d'une animation d'attaque.
        /// Bloquer les inputs joueur tant que ce flag est actif.
        /// </summary>
        public bool IsAnimating { get; private set; }

        /// <summary>Déclenché à la fin de chaque coroutine ExecuteSkill (pour la sync réseau).</summary>
        public event System.Action OnActionComplete;

        private void Awake() => Instance = this;

        public void OnTurnStart() { }

        /// <summary>Point d'entrée principal. Lance la coroutine d'exécution avec VFX.</summary>
        public void ExecuteSkill(CardInstance attacker, int skillIndex, CardInstance target)
        {
            if (NetworkBridge.IsActive)
            {
                // En réseau : déléguer au serveur via SignalR, pas d'exécution locale
                NetworkBridge.OnExecuteSkillRequested?.Invoke(attacker, skillIndex, target);
                return;
            }
            ExecuteSkillLocal(attacker, skillIndex, target);
        }

        /// <summary>
        /// Exécution locale effective, sans passer par le bridge réseau.
        /// Appelée par ExecuteSkill en offline, et directement par LocalAIGameController
        /// (humain via bridge, ou IA) pour le mode solo vs IA.
        /// </summary>
        public void ExecuteSkillLocal(CardInstance attacker, int skillIndex, CardInstance target)
        {
            CardSkill skill = skillIndex switch
            {
                0 => attacker.data.skillOne,
                1 => attacker.data.skillTwo,
                2 => attacker.data.skillThree,
                _ => null,
            };
            if (skill == null) return;
            StartCoroutine(ExecuteSkillCoroutine(attacker, skill, skillIndex, target));
        }

        private IEnumerator ExecuteSkillCoroutine(CardInstance attacker, CardSkill skill,
                                                   int skillIndex, CardInstance target)
        {
            IsAnimating = true;
            try
            {
                // ── Consomme l'action immédiatement (avant l'animation) ───
                attacker.UseSkill(skillIndex);
                TurnManager.Instance?.UseAction();

            // ── VFX — effet sur l'attaquant ───────────────────────────
            var attackerVFX = attacker.GetComponent<CardVFXHandler>();
            bool hasVFX = skill.attackVFXPrefab != null || skill.trailVFXPrefab != null || skill.impactVFXPrefab != null;

            // Attack VFX : statique sur l'attaquant (WindUp/charge)
            // Trail VFX  : voyage vers la cible (missile/projectile)
            // Si trailVFXPrefab absent et attackVFXIsProjectile : rétrocompat (attackVFX voyage)
            GameObject attackEffect = null;
            GameObject trailEffect  = null;

            Vector3 targetPos = default;
            if (target != null)
            {
                var targetVFX = target.GetComponent<CardVFXHandler>();
                targetPos = targetVFX != null ? targetVFX.GetAnchorPosition() : target.transform.position;
            }

            if (skill.attackVFXPrefab != null && attackerVFX != null)
            {
                bool useAsProjectile = skill.attackVFXIsProjectile
                                    && skill.trailVFXPrefab == null
                                    && target != null;
                attackEffect = useAsProjectile
                    ? attackerVFX.SpawnProjectileVFX(skill.attackVFXPrefab, targetPos, skill.vfxTravelTime, skill.attackVFXScale)
                    : attackerVFX.SpawnVFX(skill.attackVFXPrefab, skill.vfxTravelTime + 1f);
                if (!useAsProjectile && attackEffect != null && skill.attackVFXScale != 1f)
                    attackEffect.transform.localScale = Vector3.one * skill.attackVFXScale;
            }

            if (skill.trailVFXPrefab != null && attackerVFX != null && target != null)
                trailEffect = attackerVFX.SpawnProjectileVFX(skill.trailVFXPrefab, targetPos, skill.vfxTravelTime, skill.trailVFXScale);

            // ── Attendre le temps de vol ──────────────────────────────
            if (hasVFX && skill.vfxTravelTime > 0f)
                yield return new WaitForSeconds(skill.vfxTravelTime);

            // ── VFX — impact sur la/les cible(s) (non-incantation seulement) ──
            if (!skill.isIncantation && skill.impactVFXPrefab != null)
                SpawnImpactVFX(skill, attacker, target);

            if (!skill.isIncantation && hasVFX && skill.vfxImpactDuration > 0f)
                yield return new WaitForSeconds(skill.vfxImpactDuration);

            // Pour les incantations avec un VFX de lancement : laisser l'animation complète
            // avant de démarrer le loop. Le +1f correspond au surplus de durée du SpawnVFX.
            if (skill.isIncantation && attackEffect != null)
                yield return new WaitForSeconds(1f);

            if (attackEffect != null) Destroy(attackEffect);
            if (trailEffect  != null) Destroy(trailEffect);

            // ── Logique de jeu ────────────────────────────────────────
            if (skill.isIncantation)
            {
                attacker.AddIncantation(skill, skillIndex, target, skill.castDelayTurns);
                CombatLogManager.Instance?.AddEntry(
                    $"{attacker.data.cardName} — incantation {skill.skillName} ({skill.castDelayTurns}T)",
                    playerID: attacker.ownerPlayerID);
            }
            else
            {
                SkillExecutor.Execute(attacker, skill, target);

                // ── Victoire ──────────────────────────────────────────────
                if (BoardManager.Instance.CheckVictory(attacker.ownerPlayerID))
                    GameManager.Instance.EndGame(attacker.ownerPlayerID);
            }

            } // end try
            finally
            {
                IsAnimating = false;
                OnActionComplete?.Invoke();
            }
        }

        /// <summary>Joue les VFX d'un skill sans exécuter la logique de jeu. Utilisé en réseau.</summary>
        public void PlaySkillVFXOnly(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            StartCoroutine(PlaySkillVFXOnlyCoroutine(attacker, skill, target));
        }

        private IEnumerator PlaySkillVFXOnlyCoroutine(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            var attackerVFX = attacker.GetComponent<CardVFXHandler>();
            bool hasVFX = skill.attackVFXPrefab != null || skill.trailVFXPrefab != null || skill.impactVFXPrefab != null;
            if (!hasVFX) yield break;

            Vector3 targetPos = default;
            if (target != null)
            {
                var targetVFX = target.GetComponent<CardVFXHandler>();
                targetPos = targetVFX != null ? targetVFX.GetAnchorPosition() : target.transform.position;
            }

            GameObject attackEffect = null;
            GameObject trailEffect  = null;

            if (skill.attackVFXPrefab != null && attackerVFX != null)
            {
                bool useAsProjectile = skill.attackVFXIsProjectile
                                    && skill.trailVFXPrefab == null
                                    && target != null;
                attackEffect = useAsProjectile
                    ? attackerVFX.SpawnProjectileVFX(skill.attackVFXPrefab, targetPos, skill.vfxTravelTime, skill.attackVFXScale)
                    : attackerVFX.SpawnVFX(skill.attackVFXPrefab, skill.vfxTravelTime + 1f);
                if (!useAsProjectile && attackEffect != null && skill.attackVFXScale != 1f)
                    attackEffect.transform.localScale = Vector3.one * skill.attackVFXScale;
            }

            if (skill.trailVFXPrefab != null && attackerVFX != null && target != null)
                trailEffect = attackerVFX.SpawnProjectileVFX(skill.trailVFXPrefab, targetPos, skill.vfxTravelTime, skill.trailVFXScale);

            if (skill.vfxTravelTime > 0f)
                yield return new WaitForSeconds(skill.vfxTravelTime);

            if (!skill.isIncantation && skill.impactVFXPrefab != null)
                SpawnImpactVFX(skill, attacker, target);

            if (!skill.isIncantation && skill.vfxImpactDuration > 0f)
                yield return new WaitForSeconds(skill.vfxImpactDuration);

            if (attackEffect != null) Destroy(attackEffect);
            if (trailEffect  != null) Destroy(trailEffect);
        }

        /// <summary>Spawne l'impact VFX sur la ou les cibles selon le targetType du skill.</summary>
        public void SpawnImpactVFX(CardSkill skill, CardInstance attacker, CardInstance primaryTarget)
        {
            if (skill.impactVFXPrefab == null) return;

            var off = skill.impactVFXOffset;
            void Spawn(CardInstance card)
            {
                var go = card.GetComponent<CardVFXHandler>()?.SpawnVFX(skill.impactVFXPrefab, 2f, off);
                if (go != null && skill.impactVFXScale != 1f)
                    go.transform.localScale = Vector3.one * skill.impactVFXScale;
            }

            switch (skill.targetType)
            {
                case SkillTargetType.AllEnemies:
                {
                    int enemyID = attacker.ownerPlayerID == 0 ? 1 : 0;
                    foreach (var enemy in BoardManager.Instance.GetAliveCards(enemyID))
                        Spawn(enemy);
                    break;
                }
                case SkillTargetType.AllAllies:
                {
                    foreach (var ally in BoardManager.Instance.GetAliveCards(attacker.ownerPlayerID))
                        Spawn(ally);
                    break;
                }
                case SkillTargetType.Self:
                    Spawn(attacker);
                    break;
                case SkillTargetType.AdjacentEnemies:
                {
                    if (primaryTarget != null)
                    {
                        Spawn(primaryTarget);
                        foreach (var adj in BoardManager.Instance.GetAdjacentCards(primaryTarget))
                            Spawn(adj);
                    }
                    break;
                }
                default:
                    if (primaryTarget != null) Spawn(primaryTarget);
                    break;
            }
        }

        public void ApplyDamage(CardInstance target, int damage)
        {
            target.currentHP -= damage;
            target.currentHP = Mathf.Max(0, target.currentHP);
            if (!target.IsAlive)
            {
                // DestroyCard appelle déjà PassiveManager.OnCardDestroyed en interne
                BoardManager.Instance.DestroyCard(target);
            }
        }

        public void ApplyHeal(CardInstance target, int amount)
            => target.Heal(amount);

        private void OnCardDestroyed(CardInstance card)
        {
            // DestroyCard appelle déjà PassiveManager.OnCardDestroyed en interne
            BoardManager.Instance.DestroyCard(card);

            // Vérifier victoire
            if (BoardManager.Instance.CheckVictory(card.ownerPlayerID == 0 ? 1 : 0))
                GameManager.Instance.EndGame(card.ownerPlayerID == 0 ? 1 : 0);
        }
    }
}