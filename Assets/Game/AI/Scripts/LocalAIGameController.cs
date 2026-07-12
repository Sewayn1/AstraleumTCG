using System.Collections;
using UnityEngine;

namespace Astraleum.AI
{
    /// <summary>
    /// Contrôleur du mode solo vs IA dans la scène Combat. Coexiste avec NetworkGameController
    /// (qui se désactive lui-même si GameModeContext.IsAIMatch) — voir NetworkGameController.Start().
    ///
    /// Réutilise NetworkBridge.IsActive (LocalPlayerID = 0, humain) SANS connexion SignalR réelle,
    /// uniquement pour hériter du filtrage UI déjà en place dans CardClickHandler/CardHoverHandler/
    /// CombatUIManager (le joueur ne peut ni cliquer ni contrôler les cartes de l'IA).
    /// </summary>
    public class LocalAIGameController : MonoBehaviour
    {
        public static LocalAIGameController Instance { get; private set; }

        private const int HUMAN_PLAYER_ID = 0;
        private const int AI_PLAYER_ID = 1;
        private const int MAX_ACTIONS_SAFETY = 4; // watchdog anti-boucle infinie

        private IAIController _aiController;

        private void Awake() => Instance = this;

        private void Start()
        {
            // GameModeContext.IsBossMatch est aussi IsAIMatch (Mode != PvP) mais géré par
            // BossGameController — celui-ci doit donc s'exclure explicitement.
            if (!GameModeContext.IsAIMatch || GameModeContext.IsBossMatch) { enabled = false; return; }

            NetworkBridge.LocalPlayerID = HUMAN_PLAYER_ID;
            NetworkBridge.OpponentPlayerName = GameModeContext.AIDisplayName;

            NetworkBridge.OnEndTurnRequested = () => TurnManager.Instance.EndTurnLocal();
            NetworkBridge.OnExecuteSkillRequested = (a, i, t) => CombatManager.Instance.ExecuteSkillLocal(a, i, t);
            NetworkBridge.OnGiveUpRequested = HandleHumanGiveUp;

            // Pas de client distant à synchroniser en mode IA.
            NetworkBridge.OnArrowShowRequested = (_, __) => { };
            NetworkBridge.OnArrowHideRequested = () => { };
            NetworkBridge.OnArrowTargetRequested = (_, __, ___, ____) => { };
            NetworkBridge.OnArrowTargetHideRequested = () => { };
            NetworkBridge.OnCardSelectedRequested = (_, __) => { };
            NetworkBridge.OnCardDeselectedRequested = () => { };

            _aiController = AIControllerFactory.Create(GameModeContext.Mode);

            BoardSpawner.Instance.SpawnAllCardsVsAI(GameModeContext.PlayerDeckNumbers, GameModeContext.AIDeckNumbers);

            TurnManager.Instance.OnTurnStart += OnTurnStart;

            // TurnManager.Start() ne déclenche pas OnTurnStart pour le tout premier tour
            // (il gère l'annonce directement) — vérification manuelle une frame plus tard,
            // une fois que TurnManager.Start() a fixé currentPlayerID (tirage aléatoire).
            StartCoroutine(CheckInitialAITurnNextFrame());
        }

        private IEnumerator CheckInitialAITurnNextFrame()
        {
            yield return null;
            if (TurnManager.Instance.currentPlayerID == AI_PLAYER_ID)
                StartCoroutine(RunAITurnCoroutine());
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.OnTurnStart -= OnTurnStart;
            NetworkBridge.Reset();
        }

        // Mode Bac à sable : Card_AITraining subit les dégâts normalement (comptés, non bloqués),
        // mais remonte à ses PV max à chaque début de tour (humain ou IA) pour rester un punching-ball.
        private void HealTrainingDummies()
        {
            if (BoardManager.Instance == null) return;
            foreach (var card in BoardManager.Instance.GetAliveCards(AI_PLAYER_ID))
                if (card.data != null && card.data.isTrainingDummy)
                    card.currentHP = card.EffectiveMaxHP;
        }

        private void OnTurnStart(int playerID)
        {
            HealTrainingDummies();

            // Le tour de l'IA doit toujours se dérouler (même en Sandbox, où
            // SandboxAIController.DecideNextAction retourne null immédiatement
            // et RunAITurnCoroutine termine le tour sans qu'aucune action ne soit jouée).
            if (playerID != AI_PLAYER_ID) return;
            StartCoroutine(RunAITurnCoroutine());
        }

        private IEnumerator RunAITurnCoroutine()
        {
            yield return new WaitForSeconds(0.6f); // délai UX, l'IA ne joue pas instantanément

            int safety = 0;
            while (TurnManager.Instance.currentPlayerID == AI_PLAYER_ID
                   && TurnManager.Instance.actionsRemaining > 0
                   && safety < MAX_ACTIONS_SAFETY)
            {
                var action = _aiController?.DecideNextAction(AI_PLAYER_ID);
                if (action == null) break;

                CombatManager.Instance.ExecuteSkillLocal(action.Attacker, action.SkillIndex, action.Target);
                yield return new WaitUntil(() => !CombatManager.Instance.IsAnimating);
                yield return new WaitForSeconds(0.4f);
                safety++;
            }

            // Filet de sécurité : le tour de l'IA doit toujours se terminer.
            // TurnManager.EndTurn() ne fonctionnerait pas ici (guard LocalPlayerID == currentPlayerID).
            if (TurnManager.Instance.currentPlayerID == AI_PLAYER_ID)
                TurnManager.Instance.EndTurnLocal();
        }

        // loserPlayerID est toujours HUMAN_PLAYER_ID (seul le joueur humain peut abandonner) —
        // retour direct au menu, sans panel de défaite (même comportement que le perdant en PvP).
        private void HandleHumanGiveUp(int loserPlayerID)
        {
            GameManager.Instance.ReturnToMainMenu();
        }
    }
}
