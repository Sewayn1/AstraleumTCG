using System.Collections;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Gère les transitions de phase du Boss Voragoth (1 carte, 3 CardData successives).
    /// Vérifie le seuil de PV à la fin du tour du joueur humain (jamais mid-combo), swap
    /// CardInstance.data vers la phase suivante en préservant currentHP/armure/effets actifs,
    /// applique les effets d'entrée de phase, et fait croître les stacks Ténèbres du Boss.
    /// </summary>
    public class BossPhaseController : MonoBehaviour
    {
        public static BossPhaseController Instance;

        [Header("Données de phase (3 CardData, même maxHP sur les 3)")]
        public CardData phase1Data;
        public CardData phase2Data;
        public CardData phase3Data;

        [Header("Config")]
        [Tooltip("ID du joueur humain — la transition n'est vérifiée qu'à la fin de SON tour.")]
        public int humanPlayerID = 0;

        [Range(0f, 1f)] public float phase2Threshold = 2f / 3f;
        [Range(0f, 1f)] public float phase3Threshold = 1f / 3f;

        [Tooltip("Stacks Ténèbres temporaires ajoutés à chaque tour de Voragoth (AddStack n'étant pas implémenté dans le moteur, la croissance passe directement par StackManager).")]
        public int tenebresStackPerTurn = 1;
        [Tooltip("Durée (tours) des stacks Ténèbres temporaires — renouvelée à chaque tour de Voragoth pour ne jamais expirer tant qu'il agit.")]
        public int tenebresStackDuration = 3;

        [Header("Décors de phase (SlotsBoard_Voragoth_P1/P2/P3 — à créer/assigner)")]
        [Tooltip("GameObject du plateau visuel de la Phase 1. Laisser vide tant que le décor n'existe pas — le swap de décor est alors simplement ignoré (log d'avertissement).")]
        public GameObject phase1Board;
        public GameObject phase2Board;
        public GameObject phase3Board;

        [Header("Transition d'écran (masque le changement de décor/carte/musique)")]
        public float transitionHoldDuration = 2f;

        /// <summary>Déclenché une fois l'écran couvert, juste avant le swap visuel — utilisé par AudioBossManager.</summary>
        public event System.Action<int> OnPhaseChanged;

        private CardInstance bossCard;
        private int totalMaxHP;
        private int currentPhase = 1;

        public int CurrentPhase => currentPhase;
        public bool IsPhase2Or3 => currentPhase >= 2;

        /// <summary>Vrai pendant toute la durée de la présentation (écran couvert → révélé).</summary>
        public bool IsTransitioning { get; private set; }

        /// <summary>
        /// Vrai dès qu'une transition de phase vient de se produire, jusqu'à ce que
        /// BossGameController la consomme via ConsumeJustTransitioned() — Voragoth ne joue pas
        /// son tour immédiatement après une transition (il "se transforme" au lieu d'agir).
        /// </summary>
        public bool JustTransitioned { get; private set; }

        public void ConsumeJustTransitioned() => JustTransitioned = false;

        /// <summary>Probabilité de choisir le mode Focus (vs Chaos) ce tour — P1 50/50, P2 70/30, P3 90/10.</summary>
        public float FocusChance => currentPhase switch
        {
            1 => 0.5f,
            2 => 0.7f,
            _ => 0.9f,
        };

        private void Awake() => Instance = this;

        private void OnDisable()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnTurnEnd   -= HandleTurnEnd;
                TurnManager.Instance.OnTurnStart -= HandleTurnStart;
            }
        }

        /// <summary>Appelé une fois le Boss instancié sur le plateau (spawn de l'encounter).</summary>
        public void RegisterBoss(CardInstance boss)
        {
            bossCard = boss;
            currentPhase = 1;
            totalMaxHP = phase1Data != null ? phase1Data.maxHP : boss.data.maxHP;
            SwapBoard(1); // active le décor de la Phase 1 dès le début du combat

            if (TurnManager.Instance != null)
            {
                // -= puis += : évite un double-abonnement si RegisterBoss est rappelé (nouveau combat).
                TurnManager.Instance.OnTurnEnd   -= HandleTurnEnd;
                TurnManager.Instance.OnTurnEnd   += HandleTurnEnd;
                TurnManager.Instance.OnTurnStart -= HandleTurnStart;
                TurnManager.Instance.OnTurnStart += HandleTurnStart;
            }
        }

        private void HandleTurnStart(int playerID)
        {
            if (bossCard == null || !bossCard.IsAlive) return;
            if (playerID != bossCard.ownerPlayerID) return;

            // Croissance des stacks Ténèbres de Voragoth. AddStack (EffectType) n'a aucune
            // implémentation dans SkillExecutor — les stacks permanents sont 100% automatiques
            // (1 par carte vivante de l'élément, voir StackManager.RefreshPermanentStacks), donc
            // Voragoth seul ne peut jamais dépasser 1 stack permanent. On utilise ici le système
            // de stacks TEMPORAIRES (déjà existant, jamais relié à un effet de compétence) pour
            // lui faire réellement gagner des stacks Ténèbres au fil du combat.
            StackManager.Instance?.AddTemporaryStack(bossCard.ownerPlayerID, Element.Tenebres,
                                                      tenebresStackPerTurn, tenebresStackDuration);
        }

        private void HandleTurnEnd(int playerID)
        {
            if (bossCard == null || !bossCard.IsAlive) return;
            if (playerID != humanPlayerID) return; // seuil vérifié uniquement à la fin du tour du JOUEUR

            float hpPercent = totalMaxHP > 0 ? (float)bossCard.currentHP / totalMaxHP : 0f;

            if (currentPhase == 1 && hpPercent <= phase2Threshold)
                TransitionTo(2);
            else if (currentPhase == 2 && hpPercent <= phase3Threshold)
                TransitionTo(3);
        }

        private void TransitionTo(int newPhase)
        {
            CardData newData = newPhase == 2 ? phase2Data : phase3Data;
            if (newData == null)
            {
                Debug.LogError($"[BossPhaseController] CardData Phase {newPhase} non assignée !");
                return;
            }

            // ── Bascule logique — instantanée, ne dépend jamais de la présentation visuelle ──
            bossCard.data = newData;
            currentPhase = newPhase;

            if (newPhase == 2)
            {
                // Purge les debuffs en cours + armure temporaire pour marquer la bascule
                bossCard.activeEffects.RemoveAll(e =>
                    e.type == EffectType.Stun ||
                    e.type == EffectType.Cancel ||
                    e.type == EffectType.DamageReduction);

                bossCard.ApplyEffect(new ActiveEffect
                {
                    type           = EffectType.GiveArmor,
                    value          = 10,
                    remainingTurns = 2,
                    sourceName     = "Transition de phase",
                });
            }
            else if (newPhase == 3)
            {
                bossCard.ApplyEffect(new ActiveEffect
                {
                    type           = EffectType.Inarretable,
                    value          = 1,
                    remainingTurns = 3,
                    sourceName     = "Transition de phase",
                });

                bossCard.ApplyEffect(new ActiveEffect
                {
                    type           = EffectType.AttackBoostFlat,
                    value          = 10,
                    remainingTurns = 2,
                    sourceName     = "Transition de phase",
                });
            }

            CombatLogManager.Instance?.AddEntry(
                $"Voragoth entre en Phase {newPhase} : {newData.cardTitle} !",
                playerID: bossCard.ownerPlayerID);

            // Voragoth ne joue pas son tour immédiatement après une transition — il se
            // "transforme" au lieu d'agir. Consommé par BossGameController.
            JustTransitioned = true;

            // ── Présentation visuelle/audio — masquée derrière une transition d'écran ──
            StartCoroutine(PlayPhasePresentation(newPhase, newData));
        }

        private IEnumerator PlayPhasePresentation(int newPhase, CardData newData)
        {
            IsTransitioning = true;

            if (ScreenTransition.Instance != null)
                yield return StartCoroutine(ScreenTransition.Instance.Cover());

            // Écran masqué (ou pas de ScreenTransition en scène) — bascule instantanée du visuel.
            var img = bossCard.GetComponent<UnityEngine.UI.Image>();
            if (img != null && newData.artwork != null)
                img.sprite = newData.artwork;
            bossCard.GetComponent<CardVisualUpdater>()?.UpdateVisuals();

            SwapBoard(newPhase);
            OnPhaseChanged?.Invoke(newPhase);

            if (ScreenTransition.Instance != null)
            {
                yield return new WaitForSeconds(transitionHoldDuration);
                yield return StartCoroutine(ScreenTransition.Instance.Reveal());
            }

            IsTransitioning = false;
        }

        private void SwapBoard(int newPhase)
        {
            if (phase1Board == null && phase2Board == null && phase3Board == null)
            {
                Debug.LogWarning("[BossPhaseController] Aucun décor de phase assigné (phase1Board/2Board/3Board) — swap de décor ignoré.");
                return;
            }

            phase1Board?.SetActive(newPhase == 1);
            phase2Board?.SetActive(newPhase == 2);
            phase3Board?.SetActive(newPhase == 3);
        }
    }
}
