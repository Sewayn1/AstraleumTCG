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
            // Contre un Boss, le joueur humain commence toujours (pas de tirage aléatoire).
            if (AI.GameModeContext.IsBossMatch)
                currentPlayerID = 0;
            // Joueur de départ aléatoire — décision prise par le serveur ou en local.
            // Le client (Player 1) ne roule pas : il attend le premier snapshot pour connaître
            // le vrai currentPlayerID et déclencher l'annonce correcte.
            else if (!NetworkBridge.IsActive || NetworkBridge.LocalPlayerID == 0)
                currentPlayerID = UnityEngine.Random.Range(0, 2);

            ResetTimer();

            if (NetworkBridge.IsActive && NetworkBridge.LocalPlayerID != 0) return;

            CombatUIManager.Instance?.UpdateTurnIndicator(currentPlayerID);
            TurnCounterUI.Instance?.SetTurn(1);
            TurnAudioManager.Instance?.PlayTurnStart(currentPlayerID);
            TurnAnnouncementManager.Instance?.Show(currentPlayerID);
            CombatLogManager.Instance?.AddEntry(
                "commence !", playerID: currentPlayerID);
        }

        private void Update()
        {
            // Raccourci clavier — même action que le clic sur Btn_EndTurn
            if (Input.GetKeyDown(KeyCode.Space))
                EndTurn();

            // Pas de limite de temps par tour contre un Boss — combat solo, pas d'adversaire
            // humain à faire attendre. Le timer est aussi masqué (voir BossGameController.Start()).
            if (AI.GameModeContext.IsBossMatch) return;

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

        private void ResolveIncantations(int playerID)
        {
            var cards = BoardManager.Instance?.GetAliveCards(playerID);
            if (cards == null) return;
            bool anyFired = false;

            foreach (var card in cards.ToList())
            {
                foreach (var incant in card.pendingIncantations.ToList())
                {
                    incant.turnsRemaining--;
                    if (incant.turnsRemaining > 0) continue;

                    CardInstance target = incant.targetPlayerID >= 0
                        ? BoardManager.Instance.GetCardAtSlot(incant.targetPlayerID, incant.targetSlotIndex)
                        : null;

                    // Cible morte entre le lancement et la résolution → incantation annulée
                    // (évite un NullReferenceException dans SkillExecutor qui bloquerait
                    // définitivement la fin de tour).
                    bool needsTarget = incant.skill.targetType == SkillTargetType.SingleEnemy
                        || incant.skill.targetType == SkillTargetType.SingleAlly
                        || incant.skill.targetType == SkillTargetType.AdjacentEnemies;

                    if (needsTarget && target == null)
                    {
                        CombatLogManager.Instance?.AddEntry(
                            $"{card.data.cardName} — incantation {incant.skill.skillName} annulée (cible perdue)",
                            playerID: card.ownerPlayerID);
                        card.pendingIncantations.Remove(incant);
                        continue;
                    }

                    SkillExecutor.Execute(card, incant.skill, target);
                    CombatManager.Instance?.SpawnImpactVFX(incant.skill, card, target);
                    card.pendingIncantations.Remove(incant);
                    anyFired = true;
                }
                card.GetComponent<CardVisualUpdater>()?.UpdateVisuals();
            }

            if (anyFired && BoardManager.Instance.CheckVictory(playerID))
                GameManager.Instance.EndGame(playerID);
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

                    // Recharge gelée : dégèle en fin de tour du joueur affecté
                    // (bloqué pendant exactement 1 tour complet, comme le Stun)
                    var cdLock = card.activeEffects.Find(e => e.type == EffectType.CooldownIncrease);
                    if (cdLock != null)
                    {
                        cdLock.remainingTurns--;
                        if (cdLock.remainingTurns <= 0)
                            card.activeEffects.Remove(cdLock);
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

            // ← OnTurnStart UNIQUEMENT sur les cartes du joueur actif
            // Chaque carte traite ses propres effets (DoT, HoT) au début de SON tour
            if (BoardManager.Instance != null)
                foreach (var card in BoardManager.Instance.GetAliveCards(currentPlayerID))
                    card.OnTurnStart();

            ResolveIncantations(currentPlayerID);

            StackManager.Instance?.ApplyTurnBonuses(currentPlayerID);
            OnTurnStart?.Invoke(currentPlayerID);
            TurnAudioManager.Instance?.PlayTurnStart(currentPlayerID);
            PassiveManager.Instance?.OnTurnStart(currentPlayerID);
            CombatUIManager.Instance?.UpdateActionDots();
            CombatUIManager.Instance?.UpdateTurnIndicator(currentPlayerID);
            TurnCounterUI.Instance?.IncrementTurn();
            TurnAnnouncementManager.Instance?.Show(currentPlayerID);
            CombatLogManager.Instance?.OnTurnChanged(currentPlayerID + 1);
        }
    }
}