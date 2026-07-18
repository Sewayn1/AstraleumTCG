using System.Collections;
using System.Linq;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Contrôleur du mode Boss Thalyra dans la scène Combat. Script parallèle à
    /// BossGameController (Voragoth) / VaelthorGameController — ne les modifie jamais.
    /// Structure mono-carte comme BossGameController (pas d'adds, contrairement à Vaelthor).
    /// skillThree ("Déferlement") est une compétence normale, choisie par le scorer Focus/Chaos
    /// partagé comme skillOne/Two — plus de force-cast. La véritable "Grande Marée" est une 4e
    /// capacité qui ne vit PAS dans CardData (limité à 3 compétences) : elle se déclenche
    /// automatiquement à chaque changement de marée si Thalyra est sous grandeMareeHpThreshold,
    /// pilotée entièrement ici (dégâts + VFX manuels, hors du pipeline CardSkill classique).
    /// </summary>
    public class ThalyraGameController : MonoBehaviour
    {
        public static ThalyraGameController Instance { get; private set; }

        private const int HUMAN_PLAYER_ID = 0;
        private const int BOSS_PLAYER_ID = 1;
        private const int BOSS_SLOT_INDEX = 2; // "slot 3" (1-indexé) = index 2, même emplacement que Voragoth
        private const int MAX_ACTIONS_SAFETY = 4; // watchdog anti-boucle infinie
        private const int REWARD_CARD_NUMBER = 57;

        [Header("Défaite de Thalyra — séquence spectaculaire (Piloto Studio uniquement)")]
        public GameObject[] defeatVFXPrefabs;
        public float defeatVFXScale = 2f;
        public float defeatMusicFadeDuration = 3f;

        [Header("Apparition du Boss — VFX de spawn (Piloto Studio, CardSpawn_x)")]
        public GameObject spawnVFXPrefab;
        public float spawnVFXDuration = 1.5f;

        [Header("Grande Marée — 4e capacité, pas une CardSkill (déclenchée automatiquement)")]
        [Tooltip("Dégâts infligés à CHAQUE carte adverse à chaque changement de marée, tant que Thalyra est sous grandeMareeHpThreshold.")]
        public int grandeMareeDamage = 30;
        [Range(0f, 1f)]
        [Tooltip("Seuil de PV (% des PV Max de Thalyra) sous lequel Grande Marée se déclenche à CHAQUE changement de marée (Haute→Basse ET Basse→Haute).")]
        public float grandeMareeHpThreshold = 0.40f;
        [Tooltip("VFX joué sur chaque carte du joueur touchée par Grande Marée.")]
        public GameObject grandeMareeVFXPrefab;

        private CardInstance thalyraCard;
        private bool bossDefeatSequenceStarted;

        private void Awake() => Instance = this;

        private void Start()
        {
            if (!AI.GameModeContext.IsBossMatch || AI.GameModeContext.BossID != 2) { enabled = false; return; }

            NetworkBridge.LocalPlayerID = HUMAN_PLAYER_ID;
            NetworkBridge.OpponentPlayerName = "Thalyra";

            NetworkBridge.OnEndTurnRequested = () => TurnManager.Instance.EndTurnLocal();
            NetworkBridge.OnExecuteSkillRequested = (a, i, t) => CombatManager.Instance.ExecuteSkillLocal(a, i, t);
            NetworkBridge.OnGiveUpRequested = HandleHumanGiveUp;

            NetworkBridge.OnArrowShowRequested = (_, __) => { };
            NetworkBridge.OnArrowHideRequested = () => { };
            NetworkBridge.OnArrowTargetRequested = (_, __, ___, ____) => { };
            NetworkBridge.OnArrowTargetHideRequested = () => { };
            NetworkBridge.OnCardSelectedRequested = (_, __) => { };
            NetworkBridge.OnCardDeselectedRequested = () => { };

            BoardSpawner.Instance.SpawnBossEncounter(AI.GameModeContext.PlayerDeckNumbers,
                                                      AI.GameModeContext.BossEncounterData);

            thalyraCard = BoardManager.Instance.GetCardAtSlot(BOSS_PLAYER_ID, BOSS_SLOT_INDEX);
            CanvasGroup bossCanvasGroup = null;
            if (thalyraCard != null)
            {
                ThalyraPhaseController.Instance?.RegisterThalyra(thalyraCard);
                // showPhaseMarkers:false — Bind() par défaut dessinerait les seuils de PHASE de
                // Voragoth (BossPhaseController.Instance reste non-null, son GameObject coexiste
                // dans la scène même désactivé) : non pertinents pour Thalyra, qui n'a pas de
                // seuils de transition de phase (seulement un seuil d'escalade à 33% PV, concept
                // différent). Même précaution que VaelthorGameController.
                BossHealthBar.Instance?.Bind(thalyraCard, thalyraCard.data.maxHP, showPhaseMarkers: false);
                ThalyraTideIndicator.Instance?.Bind();

                bossCanvasGroup = thalyraCard.gameObject.GetComponent<CanvasGroup>();
                if (bossCanvasGroup == null) bossCanvasGroup = thalyraCard.gameObject.AddComponent<CanvasGroup>();
                bossCanvasGroup.alpha = 0f;
            }
            else
                Debug.LogError("[ThalyraGameController] Carte Thalyra introuvable au slot 3 après spawn !");

            if (EndGameHandler.Instance != null)
                EndGameHandler.Instance.suppressBossVictory = true;

            CombatManager.Instance?.GetComponent<AudioSource>()?.Stop();

            if (TurnManager.Instance != null && TurnManager.Instance.timerBackground != null)
                TurnManager.Instance.timerBackground.gameObject.SetActive(false);

            TurnManager.Instance.OnTurnStart += OnTurnStart;

            if (ThalyraPhaseController.Instance != null)
                ThalyraPhaseController.Instance.OnTideChanged += HandleTideChangedForGrandeMaree;

            StartCoroutine(PlayBossSpawnSequence(bossCanvasGroup));
        }

        private IEnumerator PlayBossSpawnSequence(CanvasGroup bossCanvasGroup)
        {
            var vfxHandler = thalyraCard != null ? thalyraCard.GetComponent<CardVFXHandler>() : null;
            if (vfxHandler != null && spawnVFXPrefab != null)
                vfxHandler.SpawnVFX(spawnVFXPrefab, spawnVFXDuration + 1f);

            if (bossCanvasGroup != null)
                bossCanvasGroup.alpha = 1f;

            yield return new WaitForSeconds(spawnVFXDuration);

            AudioThalyraManager.Instance?.StartCombatMusic();
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.OnTurnStart -= OnTurnStart;
            if (ThalyraPhaseController.Instance != null)
                ThalyraPhaseController.Instance.OnTideChanged -= HandleTideChangedForGrandeMaree;
            NetworkBridge.Reset();
        }

        // ── Grande Marée — 4e capacité, déclenchée à CHAQUE changement de marée (les deux sens)
        // tant que Thalyra est sous grandeMareeHpThreshold. Ne passe pas par CardSkill/SkillExecutor
        // (CardData est limité à 3 compétences) — dégâts et VFX appliqués manuellement ici, calqué
        // sur le patron déjà utilisé pour les séquences de spawn/défaite (VFX hors pipeline normal).
        private void HandleTideChangedForGrandeMaree(ThalyraTideState newState)
        {
            if (thalyraCard == null || !thalyraCard.IsAlive) return;
            float hpPercent = thalyraCard.data.maxHP > 0 ? (float)thalyraCard.currentHP / thalyraCard.data.maxHP : 0f;
            if (hpPercent > grandeMareeHpThreshold) return;

            TriggerGrandeMaree();
        }

        private void TriggerGrandeMaree()
        {
            CombatLogManager.Instance?.AddEntry("Thalyra déclenche Grande Marée !", playerID: thalyraCard.ownerPlayerID);

            foreach (var target in BoardManager.Instance.GetAliveCards(HUMAN_PLAYER_ID).ToList())
            {
                int actualDmg = target.TakeDamage(grandeMareeDamage);
                target.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(actualDmg);
                if (grandeMareeVFXPrefab != null)
                    target.GetComponent<CardVFXHandler>()?.SpawnVFX(grandeMareeVFXPrefab, 1.5f);
                CombatLogManager.Instance?.AddEntry(
                    $"{target.data.cardName} -{actualDmg} PV (Grande Marée)", playerID: thalyraCard.ownerPlayerID);
            }

            if (BoardManager.Instance.CheckVictory(thalyraCard.ownerPlayerID))
                GameManager.Instance.EndGame(thalyraCard.ownerPlayerID);
        }

        private void Update()
        {
            if (bossDefeatSequenceStarted || thalyraCard == null || thalyraCard.IsAlive) return;
            bossDefeatSequenceStarted = true;
            StartCoroutine(PlayBossDefeatSequence());
        }

        private IEnumerator PlayBossDefeatSequence()
        {
            ThalyraTideIndicator.Instance?.Hide();

            var vfxHandler = thalyraCard.GetComponent<CardVFXHandler>();
            if (vfxHandler != null && defeatVFXPrefabs != null)
            {
                foreach (var prefab in defeatVFXPrefabs)
                {
                    if (prefab == null) continue;
                    var go = vfxHandler.SpawnVFX(prefab, defeatMusicFadeDuration + 1f);
                    if (go != null) go.transform.localScale *= defeatVFXScale;
                }
            }

            if (AudioThalyraManager.Instance != null)
                yield return StartCoroutine(AudioThalyraManager.Instance.FadeOutAndStop(defeatMusicFadeDuration));
            else
                yield return new WaitForSeconds(defeatMusicFadeDuration);

            bool newlyUnlocked = PlayerCollection.Instance != null &&
                                  PlayerCollection.Instance.UnlockRewardCard(REWARD_CARD_NUMBER);

            if (EndGameHandler.Instance != null)
            {
                if (newlyUnlocked)
                    EndGameHandler.Instance.pendingUnlockMessage =
                        LocalizationManager.Get("endgame_card_unlocked", "Thalyra - Souveraine des Marées");

                EndGameHandler.Instance.suppressBossVictory = false;
                EndGameHandler.Instance.ShowEndGame(HUMAN_PLAYER_ID);
            }
        }

        private void OnTurnStart(int playerID)
        {
            if (playerID != BOSS_PLAYER_ID) return;
            StartCoroutine(RunThalyraTurnCoroutine());
        }

        private IEnumerator RunThalyraTurnCoroutine()
        {
            yield return new WaitForSeconds(0.6f); // délai UX, même cadence que les 2 autres Boss

            var boss = BoardManager.Instance.GetCardAtSlot(BOSS_PLAYER_ID, BOSS_SLOT_INDEX);
            int safety = 0;
            // skillOne/Two/Three sont maintenant toutes des compétences normales, choisies par le
            // scorer Focus/Chaos partagé (AI.BossAIController) exactement comme pour Voragoth —
            // même boucle mono-carte, plus de branche de force-cast.
            while (boss != null && boss.IsAlive && boss.IsReady
                   && TurnManager.Instance.currentPlayerID == BOSS_PLAYER_ID
                   && TurnManager.Instance.actionsRemaining > 0
                   && safety < MAX_ACTIONS_SAFETY)
            {
                float focusChance = ThalyraPhaseController.Instance != null ? ThalyraPhaseController.Instance.FocusChance : 0.6f;
                var action = AI.BossAIController.DecideAction(boss, HUMAN_PLAYER_ID, focusChance);
                if (action == null) break;

                CombatManager.Instance.ExecuteSkillLocal(boss, action.Value.SkillIndex, action.Value.Target);
                yield return new WaitUntil(() => !CombatManager.Instance.IsAnimating);
                yield return new WaitForSeconds(0.4f);
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
