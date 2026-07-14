using System.Collections;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Gère les 2 phases du Boss Vaelthor. Contrairement à BossPhaseController (Voragoth,
    /// seuil de PV, 1 carte), la transition Vaelthor est purement événementielle : dès que
    /// les 2 gardiens invoqués (Faucheur des Âmes, Gardien des Âmes) sont morts, Vaelthor
    /// perd son invulnérabilité (CardInstance.isImmortal) et bascule en Phase 2 avec un
    /// reset explicite de currentHP (sa Phase 1 n'a jamais de vrais PV réels).
    /// Script parallèle à BossPhaseController — ne le modifie jamais.
    /// </summary>
    public class VaelthorPhaseController : MonoBehaviour
    {
        public static VaelthorPhaseController Instance;

        [Header("Données de phase")]
        public CardData phase1Data;
        public CardData phase2Data;

        [Header("Décors de phase (optionnels)")]
        public GameObject phase1Board;
        public GameObject phase2Board;

        [Header("Transition d'écran")]
        public float transitionHoldDuration = 2f;

        /// <summary>Déclenché une fois l'écran couvert, juste avant le swap visuel — utilisé par AudioVaelthorManager.</summary>
        public event System.Action<int> OnPhaseChanged;

        private CardInstance vaelthorCard;
        private CardInstance faucheurCard;
        private CardInstance gardienCard;
        private int currentPhase = 1;
        private bool transitionTriggered = false;

        public int CurrentPhase => currentPhase;

        /// <summary>Vrai pendant toute la durée de la présentation (écran couvert → révélé).</summary>
        public bool IsTransitioning { get; private set; }

        /// <summary>
        /// Vrai dès que la transition vient de se produire, jusqu'à ce que VaelthorGameController
        /// la consomme via ConsumeJustTransitioned() — Vaelthor ne joue pas son tour immédiatement
        /// après sa transformation (même sémantique que Voragoth).
        /// </summary>
        public bool JustTransitioned { get; private set; }

        public void ConsumeJustTransitioned() => JustTransitioned = false;

        /// <summary>Probabilité Focus (vs Chaos) — Phase 1 gardiens 50/50, Phase 2 Vaelthor seul 80/20.</summary>
        public float FocusChance => currentPhase >= 2 ? 0.8f : 0.5f;

        private void Awake() => Instance = this;

        /// <summary>Appelé une fois le trio instancié sur le plateau (spawn de l'encounter).</summary>
        public void RegisterVaelthor(CardInstance vaelthor, CardInstance faucheur, CardInstance gardien)
        {
            vaelthorCard = vaelthor;
            faucheurCard = faucheur;
            gardienCard = gardien;
            currentPhase = 1;
            transitionTriggered = false;

            vaelthorCard.isImmortal = true;
            SwapBoard(1);
        }

        private void Update()
        {
            if (transitionTriggered || vaelthorCard == null || !vaelthorCard.IsAlive) return;
            if (currentPhase != 1) return;

            bool faucheurDead = faucheurCard == null || !faucheurCard.IsAlive;
            bool gardienDead = gardienCard == null || !gardienCard.IsAlive;
            if (faucheurDead && gardienDead)
            {
                transitionTriggered = true;
                TransitionToPhase2();
            }
        }

        private void TransitionToPhase2()
        {
            if (phase2Data == null)
            {
                Debug.LogError("[VaelthorPhaseController] CardData Phase 2 non assignée !");
                return;
            }

            // ── Bascule logique — instantanée, ne dépend jamais de la présentation visuelle ──
            vaelthorCard.isImmortal = false;
            vaelthorCard.data = phase2Data;
            // Reset EXPLICITE : la Phase 1 de Vaelthor n'a jamais de vrais PV réels (invulnérable),
            // donc contrairement à Voragoth, on ne préserve jamais currentHP entre phases.
            vaelthorCard.currentHP = phase2Data.maxHP;
            currentPhase = 2;

            CombatLogManager.Instance?.AddEntry(
                $"Vaelthor entre en Phase 2 : {phase2Data.cardTitle} !",
                playerID: vaelthorCard.ownerPlayerID);

            // Vaelthor ne joue pas son tour immédiatement après la transition — il se
            // "transforme" au lieu d'agir. Consommé par VaelthorGameController.
            JustTransitioned = true;

            StartCoroutine(PlayPhasePresentation());
        }

        private IEnumerator PlayPhasePresentation()
        {
            IsTransitioning = true;

            if (ScreenTransition.Instance != null)
                yield return StartCoroutine(ScreenTransition.Instance.Cover());

            var img = vaelthorCard.GetComponent<UnityEngine.UI.Image>();
            if (img != null && phase2Data.artwork != null)
                img.sprite = phase2Data.artwork;
            vaelthorCard.GetComponent<CardVisualUpdater>()?.UpdateVisuals();

            SwapBoard(2);
            OnPhaseChanged?.Invoke(2);

            if (ScreenTransition.Instance != null)
            {
                yield return new WaitForSeconds(transitionHoldDuration);
                yield return StartCoroutine(ScreenTransition.Instance.Reveal());
            }

            IsTransitioning = false;
        }

        private void SwapBoard(int newPhase)
        {
            if (phase1Board == null && phase2Board == null)
                return; // décor optionnel, pas assigné — ignoré silencieusement

            if (phase1Board == phase2Board)
            {
                // Décor partagé entre les deux phases (ex. pas de décor Phase 2 dédié pour
                // l'instant) — toujours actif, jamais désactivé/réactivé. Les deux lignes
                // SetActive ci-dessous se contrediraient sinon puisqu'elles ciblent le même
                // GameObject (la dernière évaluée à false écraserait la première).
                phase1Board.SetActive(true);
            }
            else
            {
                if (phase1Board != null) phase1Board.SetActive(newPhase == 1);
                if (phase2Board != null) phase2Board.SetActive(newPhase == 2);
            }

            // root.Play(true) obligatoire pour enregistrer la hiérarchie auprès du URP Render
            // Graph — un SetActive(true) seul (même avec playOnAwake) peut laisser les
            // ParticleSystem invisibles. Voir feedback_vfx_cardvfxhandler.
            var activeBoard = newPhase == 1 ? phase1Board : phase2Board;
            if (activeBoard != null)
                foreach (var ps in activeBoard.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Play(true);
        }
    }
}
