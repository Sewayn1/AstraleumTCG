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

        private bool hasReplayedThisTurn = false;
        private bool airExtraActionUsed = false;

        public void OnTurnStart()
        {
            hasReplayedThisTurn = false;
            airExtraActionUsed = false;
        }

        /// <summary>Point d'entrée principal. Lance la coroutine d'exécution avec VFX.</summary>
        public void ExecuteSkill(CardInstance attacker, int skillIndex, CardInstance target)
        {
            var skill = skillIndex == 0 ? attacker.data.skillOne : attacker.data.skillTwo;
            if (skill == null) return;
            StartCoroutine(ExecuteSkillCoroutine(attacker, skill, skillIndex, target));
        }

        private IEnumerator ExecuteSkillCoroutine(CardInstance attacker, CardSkill skill,
                                                   int skillIndex, CardInstance target)
        {
            IsAnimating = true;

            // ── Consomme l'action immédiatement (avant l'animation) ───
            attacker.UseSkill(skillIndex);
            TurnManager.Instance?.UseAction();

            // ── VFX — effet sur l'attaquant ───────────────────────────
            var attackerVFX = attacker.GetComponent<CardVFXHandler>();
            bool hasVFX = skill.attackVFXPrefab != null || skill.impactVFXPrefab != null;

            Debug.Log($"[VFX] Coroutine démarrée — skill={skill.skillName} | " +
                      $"attackVFX={skill.attackVFXPrefab} | impactVFX={skill.impactVFXPrefab} | " +
                      $"CardVFXHandler={attackerVFX} | hasVFX={hasVFX}");

            GameObject attackEffect = null;
            if (skill.attackVFXPrefab != null)
                attackEffect = attackerVFX?.SpawnVFXAttached(skill.attackVFXPrefab, skill.vfxTravelTime + 1f);

            // ── Attendre le temps de vol ──────────────────────────────
            if (hasVFX && skill.vfxTravelTime > 0f)
                yield return new WaitForSeconds(skill.vfxTravelTime);

            // ── VFX — impact sur la/les cible(s) ─────────────────────
            if (skill.impactVFXPrefab != null)
                SpawnImpactVFX(skill, attacker, target);

            if (hasVFX && skill.vfxImpactDuration > 0f)
                yield return new WaitForSeconds(skill.vfxImpactDuration);

            // Nettoie l'effet d'attaque s'il est toujours présent
            if (attackEffect != null) Destroy(attackEffect);

            // ── Logique de jeu ────────────────────────────────────────
            SkillExecutor.Execute(attacker, skill, target);

            // 🌪️ Air — chance de relance (mineur + majeur 3)
            if (StackManager.Instance != null && target != null && target.IsAlive)
            {
                float replayChance = StackManager.Instance.GetAirReplayChance(attacker.ownerPlayerID);
                if (attacker.data.element == Element.Air)
                    replayChance += StackManager.Instance.GetAirMajorReplayBonus(attacker.ownerPlayerID);

                if (replayChance > 0f && !hasReplayedThisTurn)
                {
                    float roll = UnityEngine.Random.Range(0f, 1f);
                    if (roll < replayChance)
                    {
                        hasReplayedThisTurn = true;
                        CombatLogManager.Instance?.AddEntry(
                            $"{attacker.data.cardName} rejoue ! (Air {replayChance * 100:0}%)");

                        // Rejoue avec VFX rapide
                        if (skill.impactVFXPrefab != null)
                            SpawnImpactVFX(skill, attacker, target);
                        if (hasVFX && skill.vfxImpactDuration > 0f)
                            yield return new WaitForSeconds(skill.vfxImpactDuration);

                        SkillExecutor.Execute(attacker, skill, target);
                    }
                }
            }

            // 🌪️ Air majeur 5 → +1 action aux cartes Air uniquement
            if (StackManager.Instance != null
                && attacker.data.element == Element.Air
                && StackManager.Instance.AirGrantsExtraAction(attacker.ownerPlayerID)
                && !airExtraActionUsed)
            {
                airExtraActionUsed = true;
                TurnManager.Instance.actionsRemaining++;
                CombatLogManager.Instance?.AddEntry("Air 5 stacks : +1 action accordée !");
            }

            // ── Victoire ──────────────────────────────────────────────
            if (BoardManager.Instance.CheckVictory(attacker.ownerPlayerID))
                GameManager.Instance.EndGame(attacker.ownerPlayerID);

            IsAnimating = false;
            OnActionComplete?.Invoke();
        }

        /// <summary>Spawne l'impact VFX sur la ou les cibles selon le targetType du skill.</summary>
        private void SpawnImpactVFX(CardSkill skill, CardInstance attacker, CardInstance primaryTarget)
        {
            if (skill.impactVFXPrefab == null) return;

            switch (skill.targetType)
            {
                case SkillTargetType.AllEnemies:
                {
                    int enemyID = attacker.ownerPlayerID == 0 ? 1 : 0;
                    foreach (var enemy in BoardManager.Instance.GetAliveCards(enemyID))
                        enemy.GetComponent<CardVFXHandler>()?.SpawnVFX(skill.impactVFXPrefab, 2f);
                    break;
                }
                case SkillTargetType.AllAllies:
                {
                    foreach (var ally in BoardManager.Instance.GetAliveCards(attacker.ownerPlayerID))
                        ally.GetComponent<CardVFXHandler>()?.SpawnVFX(skill.impactVFXPrefab, 2f);
                    break;
                }
                case SkillTargetType.Self:
                    attacker.GetComponent<CardVFXHandler>()?.SpawnVFX(skill.impactVFXPrefab, 2f);
                    break;
                case SkillTargetType.AdjacentEnemies:
                {
                    if (primaryTarget != null)
                    {
                        primaryTarget.GetComponent<CardVFXHandler>()?.SpawnVFX(skill.impactVFXPrefab, 2f);
                        foreach (var adj in BoardManager.Instance.GetAdjacentCards(primaryTarget))
                            adj.GetComponent<CardVFXHandler>()?.SpawnVFX(skill.impactVFXPrefab, 2f);
                    }
                    break;
                }
                default:
                    primaryTarget?.GetComponent<CardVFXHandler>()?.SpawnVFX(skill.impactVFXPrefab, 2f);
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