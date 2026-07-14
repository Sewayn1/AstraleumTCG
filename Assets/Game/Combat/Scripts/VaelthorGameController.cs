using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Contrôleur du mode Boss Vaelthor dans la scène Combat. Script parallèle à
    /// BossGameController (Voragoth) — ne le modifie jamais. Différence architecturale
    /// principale : le côté Boss compte 3 cartes actives en Phase 1 (Vaelthor + 2 gardiens),
    /// chacune agissant indépendamment sur le budget d'actions partagé (TurnManager.actionsRemaining,
    /// 2/tour) — contrairement à la boucle mono-carte de BossGameController.RunBossTurnCoroutine.
    /// </summary>
    public class VaelthorGameController : MonoBehaviour
    {
        public static VaelthorGameController Instance { get; private set; }

        private const int HUMAN_PLAYER_ID = 0;
        private const int BOSS_PLAYER_ID = 1;
        private const int MAX_ACTIONS_SAFETY = 4; // watchdog anti-boucle infinie

        [Header("Données de spawn (Faucheur/Gardien — Vaelthor Phase 1 vient de VaelthorPhaseController.phase1Data)")]
        public CardData faucheurData;
        public CardData gardienData;

        [Header("Défaite de Vaelthor — séquence spectaculaire (Piloto Studio uniquement)")]
        public GameObject[] defeatVFXPrefabs;
        public float defeatVFXScale = 2f;
        public float defeatMusicFadeDuration = 3f;

        [Header("Apparition du Boss — VFX de spawn (Piloto Studio, CardSpawn_x)")]
        public GameObject spawnVFXPrefab;
        public float spawnVFXDuration = 1.5f;

        private CardInstance vaelthorCard;
        private bool bossDefeatSequenceStarted;

        // Rotation équitable des acteurs en Phase 1 (Vaelthor + Faucheur + Gardien) — sans elle,
        // GetAliveCards().FirstOrDefault(IsReady) favorise toujours les 2 premières cartes de
        // allCards (Faucheur, Vaelthor) tant qu'elles restent prêtes, et le Gardien n'agit
        // quasiment jamais. La carte qui vient d'agir passe en fin de liste.
        private List<CardInstance> actorRotation;

        private void Awake() => Instance = this;

        private void Start()
        {
            if (!AI.GameModeContext.IsBossMatch || AI.GameModeContext.BossID != 1) { enabled = false; return; }

            NetworkBridge.LocalPlayerID = HUMAN_PLAYER_ID;
            NetworkBridge.OpponentPlayerName = "Vaelthor";

            NetworkBridge.OnEndTurnRequested = () => TurnManager.Instance.EndTurnLocal();
            NetworkBridge.OnExecuteSkillRequested = (a, i, t) => CombatManager.Instance.ExecuteSkillLocal(a, i, t);
            NetworkBridge.OnGiveUpRequested = HandleHumanGiveUp;

            NetworkBridge.OnArrowShowRequested = (_, __) => { };
            NetworkBridge.OnArrowHideRequested = () => { };
            NetworkBridge.OnArrowTargetRequested = (_, __, ___, ____) => { };
            NetworkBridge.OnArrowTargetHideRequested = () => { };
            NetworkBridge.OnCardSelectedRequested = (_, __) => { };
            NetworkBridge.OnCardDeselectedRequested = () => { };

            var vaelthorPhase1Data = VaelthorPhaseController.Instance != null ? VaelthorPhaseController.Instance.phase1Data : null;
            BoardSpawner.Instance.SpawnVaelthorEncounter(AI.GameModeContext.PlayerDeckNumbers,
                                                          vaelthorPhase1Data, faucheurData, gardienData);

            var faucheurCard = BoardManager.Instance.GetCardAtSlot(BOSS_PLAYER_ID, 1);
            vaelthorCard = BoardManager.Instance.GetCardAtSlot(BOSS_PLAYER_ID, 2);
            var gardienCard = BoardManager.Instance.GetCardAtSlot(BOSS_PLAYER_ID, 3);

            CanvasGroup bossCanvasGroup = null;
            if (vaelthorCard != null && faucheurCard != null && gardienCard != null)
            {
                VaelthorPhaseController.Instance?.RegisterVaelthor(vaelthorCard, faucheurCard, gardienCard);
                if (VaelthorPhaseController.Instance != null)
                    VaelthorPhaseController.Instance.OnPhaseChanged += HandlePhaseChanged;

                actorRotation = new List<CardInstance> { faucheurCard, vaelthorCard, gardienCard };

                // Masquée jusqu'au VFX de spawn — révélée dans PlayBossSpawnSequence().
                bossCanvasGroup = vaelthorCard.gameObject.GetComponent<CanvasGroup>();
                if (bossCanvasGroup == null) bossCanvasGroup = vaelthorCard.gameObject.AddComponent<CanvasGroup>();
                bossCanvasGroup.alpha = 0f;
            }
            else
                Debug.LogError("[VaelthorGameController] Trio Vaelthor/Faucheur/Gardien introuvable après spawn !");

            // Pas de BossHealthBar.Bind ici — Vaelthor est invulnérable en Phase 1, sa jauge
            // n'a aucun sens tant que les gardiens vivent. Bind uniquement à l'entrée en Phase 2
            // (voir HandlePhaseChanged).

            if (EndGameHandler.Instance != null)
                EndGameHandler.Instance.suppressBossVictory = true;

            CombatManager.Instance?.GetComponent<AudioSource>()?.Stop();

            if (TurnManager.Instance != null && TurnManager.Instance.timerBackground != null)
                TurnManager.Instance.timerBackground.gameObject.SetActive(false);

            TurnManager.Instance.OnTurnStart += OnTurnStart;

            StartCoroutine(PlayBossSpawnSequence(bossCanvasGroup));
        }

        private void HandlePhaseChanged(int newPhase)
        {
            if (newPhase == 2 && vaelthorCard != null)
                BossHealthBar.Instance?.Bind(vaelthorCard, vaelthorCard.data.maxHP, showPhaseMarkers: false);
        }

        private IEnumerator PlayBossSpawnSequence(CanvasGroup bossCanvasGroup)
        {
            var vfxHandler = vaelthorCard != null ? vaelthorCard.GetComponent<CardVFXHandler>() : null;
            if (vfxHandler != null && spawnVFXPrefab != null)
                vfxHandler.SpawnVFX(spawnVFXPrefab, spawnVFXDuration + 1f);

            if (bossCanvasGroup != null)
                bossCanvasGroup.alpha = 1f;

            yield return new WaitForSeconds(spawnVFXDuration);

            AudioVaelthorManager.Instance?.StartCombatMusic();
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.OnTurnStart -= OnTurnStart;
            if (VaelthorPhaseController.Instance != null)
                VaelthorPhaseController.Instance.OnPhaseChanged -= HandlePhaseChanged;
            NetworkBridge.Reset();
        }

        // Watchdog spécifique à Vaelthor lui-même — la mort des gardiens est attendue et ne doit
        // jamais déclencher la séquence de défaite (seule sa propre mort en Phase 2 compte).
        private void Update()
        {
            if (bossDefeatSequenceStarted || vaelthorCard == null || vaelthorCard.IsAlive) return;
            bossDefeatSequenceStarted = true;
            StartCoroutine(PlayBossDefeatSequence());
        }

        private IEnumerator PlayBossDefeatSequence()
        {
            var vfxHandler = vaelthorCard.GetComponent<CardVFXHandler>();
            if (vfxHandler != null && defeatVFXPrefabs != null)
            {
                foreach (var prefab in defeatVFXPrefabs)
                {
                    if (prefab == null) continue;
                    var go = vfxHandler.SpawnVFX(prefab, defeatMusicFadeDuration + 1f);
                    if (go != null) go.transform.localScale *= defeatVFXScale;
                }
            }

            if (AudioVaelthorManager.Instance != null)
                yield return StartCoroutine(AudioVaelthorManager.Instance.FadeOutAndStop(defeatMusicFadeDuration));
            else
                yield return new WaitForSeconds(defeatMusicFadeDuration);

            // Déblocage des 3 cartes de récompense — persisté, message combiné affiché une seule
            // fois (les victoires suivantes contre Vaelthor n'affichent rien de plus).
            bool anyNewlyUnlocked = false;
            if (PlayerCollection.Instance != null)
            {
                anyNewlyUnlocked |= PlayerCollection.Instance.UnlockRewardCard(49);
                anyNewlyUnlocked |= PlayerCollection.Instance.UnlockRewardCard(50);
                anyNewlyUnlocked |= PlayerCollection.Instance.UnlockRewardCard(51);
            }

            if (EndGameHandler.Instance != null)
            {
                if (anyNewlyUnlocked)
                    EndGameHandler.Instance.pendingUnlockMessage = LocalizationManager.Get("endgame_vaelthor_cards_unlocked");

                EndGameHandler.Instance.suppressBossVictory = false;
                EndGameHandler.Instance.ShowEndGame(HUMAN_PLAYER_ID);
            }
        }

        private void OnTurnStart(int playerID)
        {
            if (playerID != BOSS_PLAYER_ID) return;
            StartCoroutine(RunVaelthorTurnCoroutine());
        }

        private IEnumerator RunVaelthorTurnCoroutine()
        {
            // Une transition de phase vient de se produire : Vaelthor ne joue pas ce tour (il se
            // "transforme" au lieu d'agir) — on attend la fin de la présentation puis on rend
            // directement la main au joueur, sans passer par la boucle d'action.
            if (VaelthorPhaseController.Instance != null && VaelthorPhaseController.Instance.JustTransitioned)
            {
                while (VaelthorPhaseController.Instance.IsTransitioning)
                    yield return null;

                VaelthorPhaseController.Instance.ConsumeJustTransitioned();

                if (TurnManager.Instance.currentPlayerID == BOSS_PLAYER_ID)
                    TurnManager.Instance.EndTurnLocal();

                yield break;
            }

            yield return new WaitForSeconds(0.6f); // délai UX

            int safety = 0;
            // Boucle multi-cartes : contrairement à Voragoth (1 carte), le côté Vaelthor compte
            // jusqu'à 3 cartes actives en Phase 1 (Vaelthor + 2 gardiens), chacune se partageant
            // le budget d'actions du joueur (TurnManager.actionsRemaining, 2/tour) — même
            // mécanisme qu'une équipe PvP normale de 5 cartes partageant 2 actions/tour.
            while (TurnManager.Instance.currentPlayerID == BOSS_PLAYER_ID
                   && TurnManager.Instance.actionsRemaining > 0
                   && safety < MAX_ACTIONS_SAFETY)
            {
                var actor = actorRotation?.FirstOrDefault(c => c != null && c.IsAlive && c.IsReady);
                if (actor == null) break;

                float focusChance = VaelthorPhaseController.Instance != null ? VaelthorPhaseController.Instance.FocusChance : 0.5f;
                var action = AI.BossAIController.DecideAction(actor, HUMAN_PLAYER_ID, focusChance);
                if (action == null) break; // cet acteur n'a aucune compétence utilisable — évite un blocage

                CombatManager.Instance.ExecuteSkillLocal(actor, action.Value.SkillIndex, action.Value.Target);
                yield return new WaitUntil(() => !CombatManager.Instance.IsAnimating);
                yield return new WaitForSeconds(0.4f);

                // L'acteur qui vient de jouer passe en fin de rotation — la prochaine sélection
                // favorise les cartes qui attendent depuis le plus longtemps.
                actorRotation.Remove(actor);
                actorRotation.Add(actor);

                safety++;
            }

            if (TurnManager.Instance.currentPlayerID == BOSS_PLAYER_ID)
                TurnManager.Instance.EndTurnLocal();
        }

        private void HandleHumanGiveUp(int loserPlayerID)
        {
            GameManager.Instance.ReturnToMainMenu();
        }
    }
}
