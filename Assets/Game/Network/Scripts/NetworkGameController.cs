using System.Collections;
using System.Collections.Generic;
using Astraleum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Astraleum
{
    /// <summary>
    /// Contrôleur réseau principal — scène Combat.
    /// Se branche sur SignalRGameClient (DontDestroyOnLoad) et applique les snapshots serveur.
    /// </summary>
    public class NetworkGameController : MonoBehaviour
    {
        public static NetworkGameController Instance { get; private set; }

        private CardInstance _remoteHighlightedCard;
        private CardInstance _remoteBouncingCard;
        private bool         _firstSnapshotApplied;

        private const float SYNC_TIMEOUT_SECONDS = 10f;
        private Coroutine   _syncTimeoutCoroutine;

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (AI.GameModeContext.IsAIMatch) { enabled = false; return; }

            // Délégués NetworkBridge → envoi SignalR
            NetworkBridge.OnEndTurnRequested         = SendEndTurn;
            NetworkBridge.OnExecuteSkillRequested    = SendExecuteSkill;
            NetworkBridge.OnArrowShowRequested       = (pID, slot) => SendArrow(true,  pID, slot, -1, -1);
            NetworkBridge.OnArrowHideRequested       = ()           => SendArrow(false, -1, -1,  -1, -1);
            NetworkBridge.OnArrowTargetRequested     = (aP, aS, tP, tS) => SendArrow(true,  aP, aS, tP, tS);
            NetworkBridge.OnArrowTargetHideRequested = ()                => SendArrow(false, -1, -1, -1, -1);
            NetworkBridge.OnCardSelectedRequested    = (pID, slot) => { /* visuel uniquement */ };
            NetworkBridge.OnCardDeselectedRequested  = ()          => { };
            NetworkBridge.OnGiveUpRequested          = playerID => { _ = SignalRGameClient.Instance?.GiveUp(); };

            // Defensive init : si TargetingArrow a été sauvegardé inactif, son Awake n'a jamais tournée
            if (TargetingArrow.Instance == null)
            {
                var arrow = FindFirstObjectByType<TargetingArrow>(FindObjectsInactive.Include);
                if (arrow != null)
                {
                    arrow.gameObject.SetActive(true);
                    Debug.LogWarning("[Net] TargetingArrow forcé actif — GO sauvegardé inactif dans la scène.");
                }
            }

            var client = SignalRGameClient.Instance;
            if (client == null) { Debug.LogError("[Net] SignalRGameClient introuvable !"); return; }

            client.OnStateUpdate    += ApplySnapshot;
            client.OnGameCancelled  += OnGameCancelledHandler;
            client.OnActionError    += OnActionError;
            client.OnArrowUpdate    += OnArrowUpdateHandler;
            client.OnSkillExecuted  += OnSkillExecutedHandler;
            client.OnIncantationResolved += OnIncantationResolvedHandler;
            client.OnCombatLog     += OnCombatLogHandler;

            // Applique le GameSetup bufferisé avant le chargement de scène
            var setup = client.LastSetup;
            if (setup != null)
                OnGameSetup(setup);
        }

        private void OnDestroy()
        {
            var client = SignalRGameClient.Instance;
            if (client != null)
            {
                client.OnStateUpdate   -= ApplySnapshot;
                client.OnGameCancelled -= OnGameCancelledHandler;
                client.OnActionError   -= OnActionError;
                client.OnArrowUpdate   -= OnArrowUpdateHandler;
                client.OnSkillExecuted -= OnSkillExecutedHandler;
                client.OnIncantationResolved -= OnIncantationResolvedHandler;
                client.OnCombatLog     -= OnCombatLogHandler;
            }
            _remoteHighlightedCard?.GetComponent<CardTargetHighlight>()?.DeactivateHighlight();
            _remoteBouncingCard?.GetComponent<CardTargetHighlight>()?.DeactivateBounce();
            NetworkBridge.Reset();
        }

        // ── Game Setup ────────────────────────────────────────────────────

        private void OnGameSetup(GameSetupMessage msg)
        {
            NetworkBridge.LocalPlayerID      = msg.LocalPlayerID;
            NetworkBridge.IsServer           = false;
            NetworkBridge.LocalPlayerName    = msg.LocalPlayerName;
            NetworkBridge.OpponentPlayerName = msg.OpponentPlayerName;

            Debug.Log($"[Net] GameSetup — J{msg.LocalPlayerID} ({msg.LocalPlayerName}) vs {msg.OpponentPlayerName} | {msg.LocalDeckCardNumbers?.Count} vs {msg.OpponentDeckCardNumbers?.Count} cartes");

            SetPlayerNameLabels(msg.LocalPlayerName, msg.OpponentPlayerName);

            // Local = bas (player1Slots), adverse = haut (player2Slots), perspective invariante
            BoardSpawner.Instance?.SpawnAllCardsNetwork(msg.LocalDeckCardNumbers, msg.OpponentDeckCardNumbers);
            StackDisplayManager.Instance?.ApplyNetworkPerspective(msg.LocalPlayerID);

            // Applique le snapshot initial bufferisé EN PREMIER
            // → _firstSnapshotApplied = true avant StartSyncTimeout si snapshot déjà disponible
            var snap = SignalRGameClient.Instance?.LastSnapshot;
            if (snap != null)
                ApplySnapshot(snap);

            StartSyncTimeout();
        }

        private static void SetPlayerNameLabels(string localName, string opponentName)
        {
            var p1 = GameObject.Find("P1_Name")?.GetComponent<TMP_Text>();
            var p2 = GameObject.Find("P2_Name")?.GetComponent<TMP_Text>();
            if (p1 != null) p1.text = localName;
            if (p2 != null) p2.text = opponentName;
        }

        // ── Handlers SignalR ──────────────────────────────────────────────

        private void OnGameCancelledHandler()
        {
            Debug.Log("[Net] Partie annulée par le serveur.");
            if (LobbyUI.Instance != null)
                LobbyUI.Instance.OnGameCancelled();
            else
            {
                GameManager.ShowLeaveGameNotice = true;
                StartCoroutine(ReturnToMainMenuDelayed(0.5f));
            }
        }

        private void OnActionError(string error)
        {
            Debug.LogWarning($"[Net] ActionError : {error}");
        }

        private void OnSkillExecutedHandler(SkillExecutedEvent evt)
        {
            var attacker = BoardManager.Instance?.GetCardAtSlot(evt.AttackerPlayerID, evt.AttackerSlot);
            if (attacker == null) return;

            var skill = evt.SkillIndex == 0 ? attacker.data?.skillOne : attacker.data?.skillTwo;
            if (skill == null) return;

            var target = (evt.TargetPlayerID >= 0 && evt.TargetSlot >= 0)
                ? BoardManager.Instance?.GetCardAtSlot(evt.TargetPlayerID, evt.TargetSlot)
                : null;

            CombatManager.Instance?.PlaySkillVFXOnly(attacker, skill, target);
        }

        /// <summary>Une incantation en attente vient de se résoudre côté serveur : joue le VFX
        /// d'impact différé (le VFX de lancement a déjà joué via SkillExecuted au cast).</summary>
        private void OnIncantationResolvedHandler(IncantationResolvedEvent evt)
        {
            var attacker = BoardManager.Instance?.GetCardAtSlot(evt.AttackerPlayerID, evt.AttackerSlot);
            if (attacker == null) return;

            var skill = evt.SkillIndex == 0 ? attacker.data?.skillOne : attacker.data?.skillTwo;
            if (skill == null) return;

            var target = (evt.TargetPlayerID >= 0 && evt.TargetSlot >= 0)
                ? BoardManager.Instance?.GetCardAtSlot(evt.TargetPlayerID, evt.TargetSlot)
                : null;

            CombatManager.Instance?.SpawnImpactVFX(skill, attacker, target);
        }

        /// <summary>Reçoit le texte du log de combat généré côté serveur (seule source de vérité —
        /// le client ne reconstruit jamais ces messages) et les affiche tels quels.</summary>
        private void OnCombatLogHandler(List<CombatLogEntry> entries)
        {
            if (entries == null) return;
            foreach (var entry in entries)
                CombatLogManager.Instance?.AddEntry(entry.Text, entry.IsDeathEntry, entry.PlayerID);
        }

        private void OnArrowUpdateHandler(ArrowUpdateAction action)
        {
            // Propre mise à jour — le serveur envoie uniquement aux autres, guard de sécurité
            if (action.IsShowing && action.AttackerPlayerID == NetworkBridge.LocalPlayerID) return;

            // Nettoyage de l'état précédent
            _remoteHighlightedCard?.GetComponent<CardTargetHighlight>()?.DeactivateHighlight();
            _remoteHighlightedCard = null;
            _remoteBouncingCard?.GetComponent<CardTargetHighlight>()?.DeactivateBounce();
            _remoteBouncingCard = null;
            TargetingArrow.Instance?.HideStatic();

            if (!action.IsShowing) return;

            // Highlight sur la carte attaquante adverse
            var attackerCard = BoardManager.Instance?.GetCardAtSlot(action.AttackerPlayerID, action.AttackerSlot);
            if (attackerCard != null)
            {
                attackerCard.GetComponent<CardTargetHighlight>()?.ActivateHighlight(HighlightType.Attack);
                _remoteHighlightedCard = attackerCard;
            }

            // Cible connue : bounce sur la carte ciblée + flèche statique
            if (action.TargetPlayerID >= 0 && action.TargetSlot >= 0)
            {
                var targetCard = BoardManager.Instance?.GetCardAtSlot(action.TargetPlayerID, action.TargetSlot);
                if (targetCard != null)
                {
                    targetCard.GetComponent<CardTargetHighlight>()?.ActivateBounce();
                    _remoteBouncingCard = targetCard;
                }

                if (attackerCard != null && targetCard != null)
                {
                    var attackerRT = attackerCard.GetComponent<RectTransform>();
                    var targetRT   = targetCard.GetComponent<RectTransform>();
                    Color color    = TargetingArrow.Instance != null
                        ? TargetingArrow.Instance.arrowColor
                        : new Color(1f, 0.85f, 0.3f, 0.85f);
                    TargetingArrow.Instance?.ShowStatic(attackerRT, targetRT, color);
                }
            }
        }

        // ── Envoi (Unity → Serveur) ───────────────────────────────────────

        private void SendExecuteSkill(CardInstance attacker, int skillIndex, CardInstance target)
        {
            _ = SignalRGameClient.Instance?.ExecuteSkill(new ExecuteSkillAction
            {
                AttackerPlayerID = attacker.ownerPlayerID,
                AttackerSlot     = attacker.slotIndex,
                SkillIndex       = skillIndex,
                TargetPlayerID   = target?.ownerPlayerID ?? -1,
                TargetSlot       = target?.slotIndex     ?? -1,
            });
        }

        private void SendEndTurn()
        {
            _ = SignalRGameClient.Instance?.EndTurn();
        }

        private void SendArrow(bool show, int aP, int aS, int tP, int tS)
        {
            _ = SignalRGameClient.Instance?.UpdateArrow(new ArrowUpdateAction
            {
                IsShowing        = show,
                AttackerPlayerID = aP,
                AttackerSlot     = aS,
                TargetPlayerID   = tP,
                TargetSlot       = tS,
            });
        }

        // ── Synchronisation pré-combat ────────────────────────────────────

        private void StartSyncTimeout()
        {
            if (_syncTimeoutCoroutine != null) StopCoroutine(_syncTimeoutCoroutine);
            _syncTimeoutCoroutine = StartCoroutine(SyncTimeoutRoutine());
        }

        private IEnumerator SyncTimeoutRoutine()
        {
            float elapsed = 0f;
            while (!_firstSnapshotApplied && elapsed < SYNC_TIMEOUT_SECONDS)
            {
                if (SignalRGameClient.Instance != null && !SignalRGameClient.Instance.IsConnected)
                    break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _syncTimeoutCoroutine = null;

            if (!_firstSnapshotApplied)
            {
                Debug.LogWarning("[Net] Synchronisation pré-combat échouée (timeout ou déconnexion) — annulation.");
                _ = SignalRGameClient.Instance?.CancelGame();
                yield return new WaitForSecondsRealtime(1.5f);
                SceneManager.LoadScene("MainMenu");
            }
        }

        private IEnumerator ReturnToMainMenuDelayed(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            SceneManager.LoadScene("MainMenu");
        }

        // ── Application du snapshot (Serveur → Unity) ────────────────────

        private void ApplySnapshot(GameStateSnapshot snap)
        {
            if (TurnManager.Instance == null || BoardManager.Instance == null) return;

            int prevPlayerID = TurnManager.Instance.currentPlayerID;

            TurnManager.Instance.currentPlayerID  = snap.currentPlayerID;
            TurnManager.Instance.actionsRemaining = snap.actionsRemaining;
            TurnManager.Instance.currentTurnTime  = snap.timerRemaining;
            CombatUIManager.Instance?.UpdateActionDots();
            CombatUIManager.Instance?.UpdateTurnIndicator(snap.currentPlayerID);

            if (StackManager.Instance != null)
            {
                var elements = (Element[])System.Enum.GetValues(typeof(Element));
                for (int i = 0; i < elements.Length; i++)
                {
                    if (snap.stacksP0 != null && i < snap.stacksP0.Length)
                        StackManager.Instance.SetStacks(0, elements[i], snap.stacksP0[i]);
                    if (snap.stacksP1 != null && i < snap.stacksP1.Length)
                        StackManager.Instance.SetStacks(1, elements[i], snap.stacksP1[i]);
                }
            }

            if (snap.winner >= 0)
            {
                GameManager.Instance?.EndGame(snap.winner);
                return;
            }

            foreach (var cs in snap.cards)
            {
                var card = BoardManager.Instance.GetCardAtSlot(cs.playerID, cs.slotIndex);
                if (card == null) continue;

                bool wasAlive = card.IsAlive && card.gameObject.activeSelf;

                int hpDelta = cs.currentHP - card.currentHP;
                if (hpDelta < 0)
                    card.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(-hpDelta);
                else if (hpDelta > 0)
                    card.GetComponent<CombatPopupHandler>()?.ShowHealPopup(hpDelta);

                card.currentHP                 = cs.currentHP;
                card.hasActedThisTurn          = cs.hasActedThisTurn;
                card.skill1Cooldown            = cs.skill1Cooldown;
                card.skill2Cooldown            = cs.skill2Cooldown;
                card.passiveStackCount         = cs.passiveStackCount;
                card.bonusActionsRemaining     = cs.bonusActionsRemaining;
                card.activeEffects             = ConvertEffects(cs.effects);
                card.conditionalPassiveEffects = ConvertConditionalEffects(cs.conditionalPassiveEffects);
                card.pendingIncantations       = ConvertPendingIncantations(card, cs.pendingIncantations);

                if (!cs.isAlive && wasAlive)
                    BoardManager.Instance.DestroyCard(card);

                card.GetComponent<CardVisualUpdater>()?.UpdateVisuals();
            }

            bool isFirstSnapshot = !_firstSnapshotApplied;
            bool turnChanged     = isFirstSnapshot || prevPlayerID != snap.currentPlayerID;
            _firstSnapshotApplied = true;

            if (isFirstSnapshot && _syncTimeoutCoroutine != null)
            {
                StopCoroutine(_syncTimeoutCoroutine);
                _syncTimeoutCoroutine = null;
            }

            if (turnChanged)
            {
                CombatUIManager.Instance?.ClearAllHighlights();
                CombatUIManager.Instance?.CancelSelection();
                _remoteHighlightedCard?.GetComponent<CardTargetHighlight>()?.DeactivateHighlight();
                _remoteHighlightedCard = null;
                _remoteBouncingCard?.GetComponent<CardTargetHighlight>()?.DeactivateBounce();
                _remoteBouncingCard = null;
                TargetingArrow.Instance?.HideStatic();
                TurnAudioManager.Instance?.PlayTurnStart(snap.currentPlayerID);
                if (isFirstSnapshot)
                {
                    CombatLogManager.Instance?.AddEntry(
                        "commence !", playerID: snap.currentPlayerID);
                    TurnCounterUI.Instance?.SetTurn(1);
                }
                else
                {
                    CombatLogManager.Instance?.OnTurnChanged(snap.currentPlayerID + 1);
                    TurnCounterUI.Instance?.IncrementTurn();
                }
                TurnAnnouncementManager.Instance?.Show(snap.currentPlayerID);
            }
        }

        // ── Convertisseurs de types (Astraleum.Core → Astraleum) ──────────

        private static List<ActiveEffect> ConvertEffects(List<Astraleum.Core.ActiveEffect> src)
        {
            var list = new List<ActiveEffect>(src?.Count ?? 0);
            if (src == null) return list;
            foreach (var e in src)
                list.Add(new ActiveEffect
                {
                    type             = (EffectType)(int)e.type,
                    value            = e.value,
                    remainingTurns   = e.remainingTurns,
                    sourceName       = e.sourceName,
                    sourceSkillName  = e.sourceSkillName,
                    passiveTriggerID = e.sourcePassiveTrigger.HasValue ? (int)e.sourcePassiveTrigger.Value : -1,
                    passiveElementID = (int)e.sourceElement,
                });
            return list;
        }

        private static List<CardInstance.ConditionalPassiveEffect> ConvertConditionalEffects(
            List<Astraleum.Core.ConditionalPassiveEffect> src)
        {
            var list = new List<CardInstance.ConditionalPassiveEffect>(src?.Count ?? 0);
            if (src == null) return list;
            foreach (var e in src)
                list.Add(new CardInstance.ConditionalPassiveEffect
                {
                    type              = (Astraleum.EffectType)(int)e.type,
                    value             = e.value,
                    trigger           = (Astraleum.PassiveTrigger)(int)e.trigger,
                    requiredThreshold = e.requiredThreshold,
                    triggerElement    = (Astraleum.Element)(int)e.triggerElement,
                    effectTarget      = (Astraleum.EffectTarget)(int)e.effectTarget,
                    ownerPlayerID     = e.ownerPlayerID,
                    sourceName        = e.sourceName,
                });
            return list;
        }

        private static List<PendingIncantation> ConvertPendingIncantations(
            CardInstance card, List<Astraleum.Core.PendingIncantationSnapshot> src)
        {
            var list = new List<PendingIncantation>(src?.Count ?? 0);
            if (src == null || card.data == null) return list;
            foreach (var p in src)
                list.Add(new PendingIncantation
                {
                    skill           = p.skillIndex == 0 ? card.data.skillOne : card.data.skillTwo,
                    skillIndex      = p.skillIndex,
                    targetPlayerID  = p.targetPlayerID,
                    targetSlotIndex = p.targetSlotIndex,
                    turnsRemaining  = p.turnsRemaining,
                });
            return list;
        }
    }
}
