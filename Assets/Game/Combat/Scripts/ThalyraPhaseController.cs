using UnityEngine;

namespace Astraleum
{
    public enum ThalyraTideState
    {
        Haute,  // Défensif : Armure + Réduction de dégâts temporaires
        Basse,  // Agressif : action bonus
    }

    /// <summary>
    /// Gère le Cycle des Marées de Thalyra — mécanique inédite parmi les 3 Boss : un état
    /// OSCILLANT (Haute ↔ Basse) plutôt qu'une progression à sens unique (contrairement aux
    /// seuils de PV de BossPhaseController/Voragoth ou à l'invulnérabilité événementielle de
    /// VaelthorPhaseController). Une seule CardData tout le combat (pas de swap de phase) —
    /// l'état "marée" est un simple bool géré ici, jamais une identité de carte différente.
    /// Script parallèle à BossPhaseController/VaelthorPhaseController — ne les modifie jamais.
    /// </summary>
    public class ThalyraPhaseController : MonoBehaviour
    {
        public static ThalyraPhaseController Instance;

        [Header("Décor (optionnel — SlotsBoard_Thalyra, toléré vide)")]
        public GameObject thalyraBoard;
        [Tooltip("Décor de plateau par défaut (\"SlotsBoard\", PvP/IA) — masqué pendant le combat Thalyra pour laisser thalyraBoard s'afficher sans être recouvert/mélangé. Toléré vide.")]
        public GameObject defaultSlotsBoard;

        [Header("Config — cycle de marées")]
        [Tooltip("ID du joueur humain — l'escalade n'est vérifiée qu'à la fin de SON tour.")]
        public int humanPlayerID = 0;

        [Tooltip("Durée du cycle (tours du Boss) avant escalade (≤33% PV).")]
        public int cycleLengthBase = 3;
        [Tooltip("Durée du cycle (tours du Boss) après escalade — le rythme s'accélère.")]
        public int cycleLengthEscalated = 2;

        [Range(0f, 1f)]
        [Tooltip("Seuil de PV (% des PV Max) déclenchant l'escalade du cycle.")]
        public float escalationThreshold = 0.33f;

        [Header("Marée Haute — buff défensif temporaire, dure exactement un cycle")]
        public int tideHighArmor = 8;
        [Range(0f, 1f)] public float tideHighDamageReduction = 0.05f;

        [Header("Marée Basse — agressivité")]
        [Tooltip("Actions bonus accordées à l'entrée en Marée Basse.")]
        public int tideBassBonusActions = 1;
        [Range(0f, 1f)]
        [Tooltip("Bonus de dégâts infligés (SelfDamageAmplify) pendant la Marée Basse, dure exactement un cycle comme les buffs de Marée Haute.")]
        public float tideBassDamageBoost = 0.05f;

        [Header("Auto-stack Eau (AddStack non implémenté dans le moteur — croissance via StackManager directement, même recette que Voragoth/Ténèbres)")]
        public int eauStackPerTurn = 1;
        public int eauStackDuration = 3;

        /// <summary>Déclenché juste après la bascule d'état — utilisé par AudioThalyraManager/ThalyraTideIndicator.</summary>
        public event System.Action<ThalyraTideState> OnTideChanged;

        private CardInstance thalyraCard;
        private int turnsUntilTideChange;

        public ThalyraTideState CurrentTideState { get; private set; } = ThalyraTideState.Haute;
        public int TurnsUntilTideChange => turnsUntilTideChange;
        public bool IsEscalated { get; private set; }

        private int CurrentCycleLength => IsEscalated ? cycleLengthEscalated : cycleLengthBase;

        /// <summary>Probabilité Focus (vs Chaos) — s'intensifie légèrement après escalade, même logique que Voragoth/Vaelthor.</summary>
        public float FocusChance => IsEscalated ? 0.8f : 0.6f;

        private void Awake() => Instance = this;

        private void OnDisable()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnTurnEnd   -= HandleTurnEnd;
                TurnManager.Instance.OnTurnStart -= HandleTurnStart;
            }
        }

        /// <summary>Appelé une fois Thalyra instanciée sur le plateau (spawn de l'encounter).</summary>
        public void RegisterThalyra(CardInstance thalyra)
        {
            thalyraCard = thalyra;
            IsEscalated = false;
            turnsUntilTideChange = cycleLengthBase;

            if (defaultSlotsBoard != null)
                defaultSlotsBoard.SetActive(false);

            if (thalyraBoard != null)
            {
                thalyraBoard.SetActive(true);
                foreach (var ps in thalyraBoard.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Play(true);
            }

            // Démarre en Marée Haute — pose le buff défensif initial sans re-décrémenter le compteur.
            EnterTide(ThalyraTideState.Haute);

            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnTurnEnd   -= HandleTurnEnd;
                TurnManager.Instance.OnTurnEnd   += HandleTurnEnd;
                TurnManager.Instance.OnTurnStart -= HandleTurnStart;
                TurnManager.Instance.OnTurnStart += HandleTurnStart;
            }
        }

        private void HandleTurnStart(int playerID)
        {
            if (thalyraCard == null || !thalyraCard.IsAlive) return;
            if (playerID != thalyraCard.ownerPlayerID) return;

            StackManager.Instance?.AddTemporaryStack(thalyraCard.ownerPlayerID, Element.Eau,
                                                      eauStackPerTurn, eauStackDuration);

            turnsUntilTideChange--;
            if (turnsUntilTideChange <= 0)
            {
                EnterTide(CurrentTideState == ThalyraTideState.Haute ? ThalyraTideState.Basse : ThalyraTideState.Haute);
                turnsUntilTideChange = CurrentCycleLength;
            }
        }

        private void HandleTurnEnd(int playerID)
        {
            if (thalyraCard == null || !thalyraCard.IsAlive) return;
            if (playerID != humanPlayerID) return; // escalade vérifiée uniquement à la fin du tour du JOUEUR
            if (IsEscalated) return;

            float hpPercent = thalyraCard.data.maxHP > 0 ? (float)thalyraCard.currentHP / thalyraCard.data.maxHP : 0f;
            if (hpPercent <= escalationThreshold)
            {
                IsEscalated = true;
                CombatLogManager.Instance?.AddEntry(
                    "Thalyra entre en crue — le cycle des marées s'accélère !",
                    playerID: thalyraCard.ownerPlayerID);
            }
        }

        private void EnterTide(ThalyraTideState newState)
        {
            CurrentTideState = newState;

            if (newState == ThalyraTideState.Haute)
            {
                thalyraCard.ApplyEffect(new ActiveEffect
                {
                    type           = EffectType.GiveArmor,
                    value          = tideHighArmor,
                    remainingTurns = CurrentCycleLength,
                    sourceName     = "Marée Haute",
                });
                thalyraCard.ApplyEffect(new ActiveEffect
                {
                    type           = EffectType.DamageReduction,
                    value          = tideHighDamageReduction,
                    remainingTurns = CurrentCycleLength,
                    sourceName     = "Marée Haute",
                });
                CombatLogManager.Instance?.AddEntry(
                    "Thalyra entre en Marée Haute — mur défensif.",
                    playerID: thalyraCard.ownerPlayerID);
            }
            else
            {
                // BonusAction est un effet one-shot (traité par SkillExecutor.ApplyEffect quand il
                // provient d'une compétence) — ThalyraCard.ApplyEffect (stockage d'ActiveEffect
                // durable) ne l'appliquerait pas réellement. On incrémente donc directement le
                // compteur, comme le fait SkillExecutor en interne.
                thalyraCard.bonusActionsRemaining += tideBassBonusActions;

                // +X% DGT infligés — SelfDamageAmplify (lu côté attaquant dans DamageCalculator,
                // ≠ DamageAmplify qui amplifie les dégâts SUBIS), même mécanisme que Voragoth.
                // remainingTurns = CurrentCycleLength : expire naturellement pile à la bascule
                // suivante, même patron que GiveArmor/DamageReduction en Marée Haute.
                thalyraCard.ApplyEffect(new ActiveEffect
                {
                    type           = EffectType.SelfDamageAmplify,
                    value          = tideBassDamageBoost,
                    remainingTurns = CurrentCycleLength,
                    sourceName     = "Marée Basse",
                });

                CombatLogManager.Instance?.AddEntry(
                    "Thalyra entre en Marée Basse — flot déchaîné !",
                    playerID: thalyraCard.ownerPlayerID);
            }

            OnTideChanged?.Invoke(newState);
        }
    }
}
