using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance;

        [Header("État du tour")]
        public int currentPlayerID = 0;
        public int actionsRemaining = 2;

        [Header("Timer")]
        public float turnDuration = 120f;
        public float currentTurnTime = 0f;
        public TMP_Text timerText;
        public Image timerBackground;

        public event Action<int> OnTurnStart;
        public event Action<int> OnTurnEnd;
        // public event Action<int> OnActionUsed; <--- A revoir ?

        private void Awake() => Instance = this;

        private void Start()
        {
            // Joueur de départ aléatoire — décision prise par le serveur ou en local
            if (!NetworkBridge.IsActive || NetworkBridge.LocalPlayerID == 0)
                currentPlayerID = UnityEngine.Random.Range(0, 2);

            ResetTimer();
            CombatUIManager.Instance?.UpdateTurnIndicator(currentPlayerID);
            TurnAudioManager.Instance?.PlayTurnStart(currentPlayerID);
            TurnAnnouncementManager.Instance?.Show(currentPlayerID);
            CombatLogManager.Instance?.AddEntry(
                $"Joueur {currentPlayerID + 1} commence !", playerID: currentPlayerID);
        }

        private void Update()
        {
            if (currentTurnTime <= 0) return;

            // En réseau : le client décrémente localement pour un affichage fluide.
            // Le serveur corrige la valeur à chaque snapshot (ApplySnapshot).
            if (NetworkBridge.IsActive && !NetworkBridge.IsServer)
            {
                currentTurnTime = Mathf.Max(0f, currentTurnTime - Time.deltaTime);
                UpdateTimerUI();
                return;
            }

            currentTurnTime -= Time.deltaTime;
            UpdateTimerUI();

            if (currentTurnTime <= 0)
            {
                currentTurnTime = 0;
                EndTurn();
            }
        }

        private void UpdateTimerUI()
        {
            if (timerText == null) return;

            int minutes = Mathf.FloorToInt(currentTurnTime / 60f);
            int seconds = Mathf.FloorToInt(currentTurnTime % 60f);
            timerText.text = $"{minutes}:{seconds:00}";

            if (currentTurnTime <= 30f)
                timerText.color = new Color(1f, 0.25f, 0.25f);      // Rouge
            else if (currentTurnTime <= 60f)
                timerText.color = new Color(1f, 0.9f, 0.1f);         // Jaune
            else
                timerText.color = Color.white;
        }

        public void ResetTimer()
        {
            currentTurnTime = turnDuration;
        }

        public bool CanAct(CardInstance card)
            => actionsRemaining > 0
            && card.IsReady
            && card.ownerPlayerID == currentPlayerID;

        public void UseAction()
        {
            actionsRemaining--;
            if (actionsRemaining < 0) actionsRemaining = 0;
            CombatUIManager.Instance?.UpdateActionDots();
        }

        /// <summary>
        /// Appelé par le bouton UI ou le timer.
        /// En réseau : toujours délégué via NetworkBridge (serveur inclus)
        /// pour garantir que BroadcastGameState() est appelé après EndTurnLocal().
        /// </summary>
        public void EndTurn()
        {
            if (NetworkBridge.IsActive)
            {
                if (NetworkBridge.LocalPlayerID != currentPlayerID) return;
                NetworkBridge.OnEndTurnRequested?.Invoke();
                return;
            }

            EndTurnLocal();
        }

        /// <summary>
        /// Exécution locale effective de la fin de tour (appelée par le serveur).
        /// </summary>
        public void EndTurnLocal()
        {
            // Bloquer la fin de tour pendant une animation d'attaque
            if (CombatManager.Instance != null && CombatManager.Instance.IsAnimating) return;

            int oldPlayerID = currentPlayerID;
            OnTurnEnd?.Invoke(currentPlayerID);

            // Fin du tour → décrémente le Stun des cartes du joueur qui vient de jouer
            if (BoardManager.Instance != null)
                foreach (var card in BoardManager.Instance.GetAliveCards(currentPlayerID))
                {
                    var stun = card.activeEffects.Find(e => e.type == EffectType.Stun);
                    if (stun != null && stun.remainingTurns != -1)
                    {
                        stun.remainingTurns--;
                        if (stun.remainingTurns <= 0)
                            card.activeEffects.Remove(stun);
                    }
                }

            StackManager.Instance?.OnTurnEnd(currentPlayerID);

            // Reset visuel de TOUTES les cartes
            if (BoardManager.Instance != null)
                for (int p = 0; p < 2; p++)
                    foreach (var card in BoardManager.Instance.GetAliveCards(p))
                    {
                        card.hasActedThisTurn = false;
                        card.bonusActionsRemaining = 0;
                        card.GetComponent<CardVisualUpdater>()?.UpdateVisuals();
                    }

            currentPlayerID = currentPlayerID == 0 ? 1 : 0;
            actionsRemaining = 2;

            StackManager.Instance?.RefreshPermanentStacks();

            ResetTimer();
            CombatUIManager.Instance?.ClearAllHighlights();
            CombatUIManager.Instance?.CancelSelection();

            // Applique le Poison Ténèbres de l'ancien joueur sur le nouveau AVANT son ProcessActiveEffects
            // (fix timing : la victime subit le poison dès son premier tour actif)
            StackManager.Instance?.ApplyPoisonToEnemies(oldPlayerID);

            // ← OnTurnStart UNIQUEMENT sur les cartes du joueur actif
            // Chaque carte traite ses propres effets (DoT, HoT) au début de SON tour
            if (BoardManager.Instance != null)
                foreach (var card in BoardManager.Instance.GetAliveCards(currentPlayerID))
                    card.OnTurnStart();

            StackManager.Instance?.ApplyTurnBonuses(currentPlayerID);
            OnTurnStart?.Invoke(currentPlayerID);
            TurnAudioManager.Instance?.PlayTurnStart(currentPlayerID);
            PassiveManager.Instance?.OnTurnStart(currentPlayerID);
            CombatUIManager.Instance?.UpdateActionDots();
            CombatUIManager.Instance?.UpdateTurnIndicator(currentPlayerID);
            TurnAnnouncementManager.Instance?.Show(currentPlayerID);
            CombatLogManager.Instance?.OnTurnChanged(currentPlayerID + 1);
        }
    }
}