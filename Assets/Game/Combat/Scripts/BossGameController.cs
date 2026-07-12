using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Contrôleur du mode Boss (Voragoth) dans la scène Combat. Coexiste avec NetworkGameController
    /// (se désactive lui-même si GameModeContext.IsAIMatch, ce qui inclut IsBossMatch) et
    /// LocalAIGameController (s'exclut explicitement si GameModeContext.IsBossMatch — voir son Start()).
    ///
    /// Réutilise NetworkBridge.IsActive (LocalPlayerID = 0, humain) SANS connexion SignalR réelle,
    /// même principe que LocalAIGameController.
    /// </summary>
    public class BossGameController : MonoBehaviour
    {
        public static BossGameController Instance { get; private set; }

        private const int HUMAN_PLAYER_ID = 0;
        private const int BOSS_PLAYER_ID = 1;
        private const int BOSS_SLOT_INDEX = 2; // "slot 3" (1-indexé) = index 2
        private const int MAX_ACTIONS_SAFETY = 4; // watchdog anti-boucle infinie

        [Header("Défaite du Boss — séquence spectaculaire (Piloto Studio uniquement)")]
        [Tooltip("VFX joués sur Voragoth à sa destruction, avant l'affichage de Victoire.")]
        public GameObject[] defeatVFXPrefabs;
        [Tooltip("Multiplicateur de taille appliqué aux VFX de défaite (spectacle accru).")]
        public float defeatVFXScale = 2f;
        [Tooltip("Durée du fondu sortant de la musique en cours avant l'affichage de Victoire (secondes).")]
        public float defeatMusicFadeDuration = 3f;

        [Header("Apparition du Boss — VFX de spawn (Piloto Studio, CardSpawn_x)")]
        [Tooltip("VFX joué à l'apparition de Voragoth en tout début de combat. La carte est masquée jusqu'à ce moment puis révélée pendant le VFX.")]
        public GameObject spawnVFXPrefab;
        [Tooltip("Durée du VFX de spawn (secondes) avant que la musique de combat ne démarre.")]
        public float spawnVFXDuration = 1.5f;

        private CardInstance bossCard;
        private bool bossDefeatSequenceStarted;

        private void Awake() => Instance = this;

        private void Start()
        {
            if (!AI.GameModeContext.IsBossMatch) { enabled = false; return; }

            NetworkBridge.LocalPlayerID = HUMAN_PLAYER_ID;
            NetworkBridge.OpponentPlayerName = "Voragoth";

            NetworkBridge.OnEndTurnRequested = () => TurnManager.Instance.EndTurnLocal();
            NetworkBridge.OnExecuteSkillRequested = (a, i, t) => CombatManager.Instance.ExecuteSkillLocal(a, i, t);
            NetworkBridge.OnGiveUpRequested = HandleHumanGiveUp;

            // Pas de client distant à synchroniser en mode Boss.
            NetworkBridge.OnArrowShowRequested = (_, __) => { };
            NetworkBridge.OnArrowHideRequested = () => { };
            NetworkBridge.OnArrowTargetRequested = (_, __, ___, ____) => { };
            NetworkBridge.OnArrowTargetHideRequested = () => { };
            NetworkBridge.OnCardSelectedRequested = (_, __) => { };
            NetworkBridge.OnCardDeselectedRequested = () => { };

            BoardSpawner.Instance.SpawnBossEncounter(AI.GameModeContext.PlayerDeckNumbers,
                                                      AI.GameModeContext.BossEncounterData);

            bossCard = BoardManager.Instance.GetCardAtSlot(BOSS_PLAYER_ID, BOSS_SLOT_INDEX);
            CanvasGroup bossCanvasGroup = null;
            if (bossCard != null)
            {
                BossPhaseController.Instance?.RegisterBoss(bossCard);
                BossHealthBar.Instance?.Bind(bossCard, bossCard.data.maxHP);

                // Masquée jusqu'au VFX de spawn — révélée dans PlayBossSpawnSequence().
                bossCanvasGroup = bossCard.gameObject.GetComponent<CanvasGroup>();
                if (bossCanvasGroup == null) bossCanvasGroup = bossCard.gameObject.AddComponent<CanvasGroup>();
                bossCanvasGroup.alpha = 0f;
            }
            else
                Debug.LogError("[BossGameController] Carte Boss introuvable au slot 3 après spawn !");

            // Retient l'affichage automatique de Victoire — la séquence de défaite spectaculaire
            // (VFX + fondu musique) doit jouer avant. Jamais appliqué à une défaite du joueur.
            if (EndGameHandler.Instance != null)
                EndGameHandler.Instance.suppressBossVictory = true;

            // Coupe la musique d'ambiance de CombatManager — on entend sinon les deux musiques
            // se superposer pendant les transitions de phase.
            CombatManager.Instance?.GetComponent<AudioSource>()?.Stop();

            // Pas de timer de tour contre un Boss (combat solo, pas de rush nécessaire)
            if (TurnManager.Instance != null && TurnManager.Instance.timerBackground != null)
                TurnManager.Instance.timerBackground.gameObject.SetActive(false);

            TurnManager.Instance.OnTurnStart += OnTurnStart;

            StartCoroutine(PlayBossSpawnSequence(bossCanvasGroup));
        }

        // Carte masquée dès le spawn (alpha 0 posée dans Start()) → VFX CardSpawn_x joué à sa
        // position → révélée pendant le VFX → musique de combat démarrée seulement une fois le
        // VFX terminé (pas de superposition avec l'apparition).
        private System.Collections.IEnumerator PlayBossSpawnSequence(CanvasGroup bossCanvasGroup)
        {
            var vfxHandler = bossCard != null ? bossCard.GetComponent<CardVFXHandler>() : null;
            if (vfxHandler != null && spawnVFXPrefab != null)
                vfxHandler.SpawnVFX(spawnVFXPrefab, spawnVFXDuration + 1f);

            if (bossCanvasGroup != null)
                bossCanvasGroup.alpha = 1f;

            yield return new WaitForSeconds(spawnVFXDuration);

            AudioBossManager.Instance?.StartCombatMusic();
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.OnTurnStart -= OnTurnStart;
            NetworkBridge.Reset();
        }

        // Détection en temps réel de la mort de Voragoth, quel que soit le tour en cours au moment
        // du coup fatal (le joueur peut le tuer à n'importe quel moment de son tour) — même schéma
        // de watchdog que EndGameHandler.LateUpdate.
        private void Update()
        {
            if (bossDefeatSequenceStarted || bossCard == null || bossCard.IsAlive) return;
            bossDefeatSequenceStarted = true;
            StartCoroutine(PlayBossDefeatSequence());
        }

        private System.Collections.IEnumerator PlayBossDefeatSequence()
        {
            var vfxHandler = bossCard.GetComponent<CardVFXHandler>();
            if (vfxHandler != null && defeatVFXPrefabs != null)
            {
                foreach (var prefab in defeatVFXPrefabs)
                {
                    if (prefab == null) continue;
                    var go = vfxHandler.SpawnVFX(prefab, defeatMusicFadeDuration + 1f);
                    if (go != null) go.transform.localScale *= defeatVFXScale;
                }
            }

            if (AudioBossManager.Instance != null)
                yield return StartCoroutine(AudioBossManager.Instance.FadeOutAndStop(defeatMusicFadeDuration));
            else
                yield return new WaitForSeconds(defeatMusicFadeDuration);

            // Déblocage de la carte de récompense (Card_048) — persisté, un seul message affiché
            // la toute première fois (les victoires suivantes contre Voragoth n'affichent rien de plus).
            bool newlyUnlocked = PlayerCollection.Instance != null && PlayerCollection.Instance.UnlockRewardCard(48);

            if (EndGameHandler.Instance != null)
            {
                if (newlyUnlocked)
                    EndGameHandler.Instance.pendingUnlockMessage = LocalizationManager.Get("endgame_card_unlocked", "Voragoth - Dernière Calamité");

                EndGameHandler.Instance.suppressBossVictory = false;
                EndGameHandler.Instance.ShowEndGame(HUMAN_PLAYER_ID);
            }
        }

        private void OnTurnStart(int playerID)
        {
            if (playerID != BOSS_PLAYER_ID) return;
            StartCoroutine(RunBossTurnCoroutine());
        }

        private System.Collections.IEnumerator RunBossTurnCoroutine()
        {
            // Une transition de phase vient de se produire : Voragoth ne joue pas ce tour (il se
            // "transforme" au lieu d'agir) — on attend la fin de la présentation (écran révélé)
            // puis on rend directement la main au joueur, sans passer par la boucle d'action.
            if (BossPhaseController.Instance != null && BossPhaseController.Instance.JustTransitioned)
            {
                while (BossPhaseController.Instance.IsTransitioning)
                    yield return null;

                BossPhaseController.Instance.ConsumeJustTransitioned();

                if (TurnManager.Instance.currentPlayerID == BOSS_PLAYER_ID)
                    TurnManager.Instance.EndTurnLocal();

                yield break;
            }

            yield return new WaitForSeconds(0.6f); // délai UX, Voragoth ne joue pas instantanément

            var bossCard = BoardManager.Instance.GetCardAtSlot(BOSS_PLAYER_ID, BOSS_SLOT_INDEX);
            int safety = 0;
            // bossCard.IsReady gate = (!hasActedThisTurn || bonusActionsRemaining>0) : limite Voragoth
            // à 1 action en Phase 1 (pas de BonusAction) et 2 en Phase 2/3 (BonusAction du passif).
            while (bossCard != null && bossCard.IsAlive && bossCard.IsReady
                   && TurnManager.Instance.currentPlayerID == BOSS_PLAYER_ID
                   && TurnManager.Instance.actionsRemaining > 0
                   && safety < MAX_ACTIONS_SAFETY)
            {
                float focusChance = BossPhaseController.Instance != null ? BossPhaseController.Instance.FocusChance : 0.5f;
                var action = AI.BossAIController.DecideAction(bossCard, HUMAN_PLAYER_ID, focusChance);
                if (action == null) break;

                CombatManager.Instance.ExecuteSkillLocal(bossCard, action.Value.SkillIndex, action.Value.Target);
                yield return new WaitUntil(() => !CombatManager.Instance.IsAnimating);
                yield return new WaitForSeconds(0.4f);
                safety++;
            }

            // Filet de sécurité : le tour de Voragoth doit toujours se terminer.
            if (TurnManager.Instance.currentPlayerID == BOSS_PLAYER_ID)
                TurnManager.Instance.EndTurnLocal();
        }

        // loserPlayerID est toujours HUMAN_PLAYER_ID (seul le joueur humain peut abandonner).
        private void HandleHumanGiveUp(int loserPlayerID)
        {
            GameManager.Instance.ReturnToMainMenu();
        }
    }
}
