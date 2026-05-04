#if MIRROR
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Contrôleur réseau principal — scène Combat.
    ///
    /// Flux :
    ///   1. Awake → NetworkBridge initialisé
    ///   2. Start → lecture des decks pré-échangés dans AstraleumNetworkManager → spawn
    ///   3. Serveur → diffuse l'état initial après court délai
    /// </summary>
    public class NetworkGameController : MonoBehaviour
    {
        public static NetworkGameController Instance { get; private set; }

        private CardInstance _remoteHighlightedCard;
        private CardInstance _remoteBouncingCard;

        // ── Init ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;

            NetworkBridge.IsServer      = NetworkServer.active;
            NetworkBridge.LocalPlayerID = NetworkServer.active ? 0 : 1;

            Debug.Log($"[Net] Joueur local : J{NetworkBridge.LocalPlayerID + 1} | Serveur : {NetworkBridge.IsServer}");
        }

        private void Start()
        {
            // ── Délégués NetworkBridge ────────────────────────────────────
            NetworkBridge.OnEndTurnRequested         = RequestEndTurnInternal;
            NetworkBridge.OnExecuteSkillRequested    = RequestExecuteSkillInternal;
            NetworkBridge.OnArrowShowRequested       = (playerID, slot) => RequestArrowUpdateInternal(true,  playerID, slot);
            NetworkBridge.OnArrowHideRequested       = ()               => RequestArrowUpdateInternal(false, -1, -1);
            NetworkBridge.OnArrowTargetRequested     = (aP, aS, tP, tS) => RequestArrowTargetInternal(true,  aP, aS, tP, tS);
            NetworkBridge.OnArrowTargetHideRequested = ()                => RequestArrowTargetInternal(false, -1, -1, -1, -1);
            NetworkBridge.OnCardSelectedRequested    = (playerID, slot)  => RequestCardSelectedInternal(true,  playerID, slot);
            NetworkBridge.OnCardDeselectedRequested  = ()                => RequestCardSelectedInternal(false, -1, -1);
            NetworkBridge.OnGiveUpRequested          = RequestGiveUpInternal;

            // ── Handlers serveur ──────────────────────────────────────────
            if (NetworkServer.active)
            {
                NetworkServer.RegisterHandler<NetMsgExecuteSkill>(OnServerExecuteSkill);
                NetworkServer.RegisterHandler<NetMsgEndTurn>(OnServerEndTurn);
                NetworkServer.RegisterHandler<NetMsgArrowUpdate>(OnServerArrowUpdate);
                NetworkServer.RegisterHandler<NetMsgArrowTarget>(OnServerArrowTarget);
                NetworkServer.RegisterHandler<NetMsgCardSelected>(OnServerCardSelected);
                NetworkServer.RegisterHandler<NetMsgGiveUp>(OnServerGiveUp);
            }

            // ── Handlers client ───────────────────────────────────────────
            NetworkClient.RegisterHandler<NetMsgGameState>(OnClientReceiveGameState);
            NetworkClient.RegisterHandler<NetMsgArrowUpdate>(OnClientArrowUpdate);
            NetworkClient.RegisterHandler<NetMsgArrowTarget>(OnClientArrowTarget);
            NetworkClient.RegisterHandler<NetMsgCardSelected>(OnClientCardSelected);
            NetworkClient.RegisterHandler<NetMsgGiveUp>(OnClientGiveUp);

            if (CombatManager.Instance != null)
                CombatManager.Instance.OnActionComplete += OnActionComplete;

            SpawnBoardFromNetworkData();

            // Perspective basée sur localID : chaque client voit ses propres cartes en bas (player1Slots).
            // Le spawn est déjà perspective-correct → seul l'affichage des stacks a besoin d'être remappé.
            StackDisplayManager.Instance?.ApplyNetworkPerspective(NetworkBridge.LocalPlayerID);
        }

        private void OnDestroy()
        {
            if (NetworkServer.active)
            {
                NetworkServer.UnregisterHandler<NetMsgExecuteSkill>();
                NetworkServer.UnregisterHandler<NetMsgEndTurn>();
                NetworkServer.UnregisterHandler<NetMsgArrowUpdate>();
                NetworkServer.UnregisterHandler<NetMsgArrowTarget>();
                NetworkServer.UnregisterHandler<NetMsgCardSelected>();
                NetworkServer.UnregisterHandler<NetMsgGiveUp>();
            }
            if (NetworkClient.isConnected)
            {
                NetworkClient.UnregisterHandler<NetMsgGameState>();
                NetworkClient.UnregisterHandler<NetMsgArrowUpdate>();
                NetworkClient.UnregisterHandler<NetMsgArrowTarget>();
                NetworkClient.UnregisterHandler<NetMsgCardSelected>();
                NetworkClient.UnregisterHandler<NetMsgGiveUp>();
            }

            if (CombatManager.Instance != null)
                CombatManager.Instance.OnActionComplete -= OnActionComplete;

            _remoteHighlightedCard?.GetComponent<CardTargetHighlight>()?.DeactivateHighlight();
            _remoteBouncingCard?.GetComponent<CardTargetHighlight>()?.DeactivateBounce();
            _remoteHighlightedCard = null;
            _remoteBouncingCard    = null;
            NetworkBridge.Reset();
        }

        // ── Spawn du board depuis les decks pré-échangés ─────────────────

        private void SpawnBoardFromNetworkData()
        {
            var nm = AstraleumNetworkManager.Instance;
            if (nm == null)
            {
                Debug.LogError("[Net] AstraleumNetworkManager introuvable !");
                return;
            }

            Debug.Log($"[Net] Decks — local: \"{nm.localDeckCsv}\" | adversaire: \"{nm.opponentDeckCsv}\"");

            List<int> p1Numbers, p2Numbers;

            if (NetworkBridge.LocalPlayerID == 0)
            {
                p1Numbers = ParseCsv(nm.localDeckCsv);
                p2Numbers = ParseCsv(nm.opponentDeckCsv);
            }
            else
            {
                p1Numbers = ParseCsv(nm.opponentDeckCsv);
                p2Numbers = ParseCsv(nm.localDeckCsv);
            }

            if (p1Numbers.Count == 0 || p2Numbers.Count == 0)
            {
                Debug.LogError($"[Net] Deck invalide — J1:{p1Numbers.Count} J2:{p2Numbers.Count}. Vérifiez que SetLocalDeck() a été appelé avant StartHost/StartClient.");
                return;
            }

            Debug.Log($"[Net] Spawn board — J1:{p1Numbers.Count} cartes | J2:{p2Numbers.Count} cartes | LocalID:{NetworkBridge.LocalPlayerID}");
            BoardSpawner.Instance?.SpawnAllCardsNetwork(p1Numbers, p2Numbers);

            if (NetworkBridge.IsServer)
                StartCoroutine(BroadcastAfterDelay(0.2f));
        }

        // ── Arrow attaquant (highlight de la carte qui attaque) ───────────

        private void RequestArrowUpdateInternal(bool isShowing, int attackerPlayerID, int attackerSlot)
        {
            var msg = new NetMsgArrowUpdate
            {
                isShowing        = isShowing,
                attackerPlayerID = attackerPlayerID,
                attackerSlot     = attackerSlot,
            };

            if (NetworkBridge.IsServer)
                NetworkServer.SendToAll(msg);
            else
                NetworkClient.Send(msg);
        }

        private void OnServerArrowUpdate(NetworkConnectionToClient conn, NetMsgArrowUpdate msg)
        {
            NetworkServer.SendToAll(msg);
        }

        private void OnClientArrowUpdate(NetMsgArrowUpdate msg)
        {
            Debug.Log($"[Net] Arrow reçu — isShowing:{msg.isShowing} attackerP:{msg.attackerPlayerID} slot:{msg.attackerSlot} | localID:{NetworkBridge.LocalPlayerID}");

            if (msg.isShowing && msg.attackerPlayerID == NetworkBridge.LocalPlayerID) return;

            _remoteHighlightedCard?.GetComponent<CardTargetHighlight>()?.DeactivateHighlight();
            _remoteHighlightedCard = null;

            if (msg.isShowing)
            {
                var card = BoardManager.Instance?.GetCardAtSlot(msg.attackerPlayerID, msg.attackerSlot);
                if (card == null)
                {
                    Debug.LogWarning($"[Net] Arrow : carte introuvable — P{msg.attackerPlayerID} slot {msg.attackerSlot}");
                    return;
                }
                card.GetComponent<CardTargetHighlight>()?.ActivateHighlight(HighlightType.Attack);
                _remoteHighlightedCard = card;
                Debug.Log($"[Net] Arrow highlight activé sur {card.data?.cardName}");
            }
        }

        // ── Arrow cible (flèche statique vers la carte survolée) ─────────

        private void RequestArrowTargetInternal(bool isShowing, int aP, int aS, int tP, int tS)
        {
            var msg = new NetMsgArrowTarget
            {
                isShowing        = isShowing,
                attackerPlayerID = aP,
                attackerSlot     = aS,
                targetPlayerID   = tP,
                targetSlot       = tS,
            };

            if (NetworkBridge.IsServer)
                NetworkServer.SendToAll(msg);
            else
                NetworkClient.Send(msg);
        }

        private void OnServerArrowTarget(NetworkConnectionToClient conn, NetMsgArrowTarget msg)
        {
            NetworkServer.SendToAll(msg);
        }

        private void OnClientArrowTarget(NetMsgArrowTarget msg)
        {
            // Ne pas appliquer si c'est notre propre survol
            if (msg.isShowing && msg.attackerPlayerID == NetworkBridge.LocalPlayerID) return;

            if (!msg.isShowing)
            {
                TargetingArrow.Instance?.HideStatic();
                return;
            }

            var attacker = BoardManager.Instance?.GetCardAtSlot(msg.attackerPlayerID, msg.attackerSlot);
            var target   = BoardManager.Instance?.GetCardAtSlot(msg.targetPlayerID,   msg.targetSlot);
            if (attacker == null || target == null) return;

            var attackerRT = attacker.GetComponent<RectTransform>();
            var targetRT   = target.GetComponent<RectTransform>();
            if (attackerRT == null || targetRT == null) return;

            Color arrowCol = (target.ownerPlayerID != msg.attackerPlayerID)
                ? new Color(0.9f, 0.2f, 0.2f, 0.85f)
                : new Color(0.2f, 0.85f, 0.35f, 0.85f);
            TargetingArrow.Instance?.ShowStatic(attackerRT, targetRT, arrowCol);
        }

        // ── Sélection de carte (bounce visuel côté adversaire) ────────────

        private void RequestCardSelectedInternal(bool isSelected, int playerID, int slotIndex)
        {
            var msg = new NetMsgCardSelected
            {
                isSelected = isSelected,
                playerID   = playerID,
                slotIndex  = slotIndex,
            };

            if (NetworkBridge.IsServer)
                NetworkServer.SendToAll(msg);
            else
                NetworkClient.Send(msg);
        }

        private void OnServerCardSelected(NetworkConnectionToClient conn, NetMsgCardSelected msg)
        {
            NetworkServer.SendToAll(msg);
        }

        private void OnClientCardSelected(NetMsgCardSelected msg)
        {
            // Ne pas appliquer si c'est notre propre sélection
            if (msg.playerID == NetworkBridge.LocalPlayerID) return;

            _remoteBouncingCard?.GetComponent<CardTargetHighlight>()?.DeactivateBounce();
            _remoteBouncingCard = null;

            if (msg.isSelected)
            {
                var card = BoardManager.Instance?.GetCardAtSlot(msg.playerID, msg.slotIndex);
                if (card == null) return;
                card.GetComponent<CardTargetHighlight>()?.ActivateBounce();
                _remoteBouncingCard = card;
                Debug.Log($"[Net] Bounce activé sur {card.data?.cardName} (P{msg.playerID})");
            }
        }

        // ── Abandon ───────────────────────────────────────────────────────

        private void RequestGiveUpInternal(int loserPlayerID)
        {
            var msg = new NetMsgGiveUp { loserPlayerID = loserPlayerID };
            if (NetworkBridge.IsServer)
                NetworkServer.SendToAll(msg); // diffuse à tous les clients (y compris l'hôte)
            else
                NetworkClient.Send(msg);      // envoie au serveur pour relay
        }

        private void OnServerGiveUp(NetworkConnectionToClient conn, NetMsgGiveUp msg)
        {
            NetworkServer.SendToAll(msg);
        }

        private void OnClientGiveUp(NetMsgGiveUp msg)
        {
            if (NetworkBridge.LocalPlayerID == msg.loserPlayerID)
                StartCoroutine(ReturnToMainMenuDelayed());
            else
                EndGameHandler.Instance?.ShowGiveUpResult(msg.loserPlayerID);
        }

        private IEnumerator ReturnToMainMenuDelayed()
        {
            yield return new WaitForSeconds(1f);
            GameManager.Instance?.ReturnToMainMenu();
        }

        // ── Délégués NetworkBridge ────────────────────────────────────────

        private void RequestEndTurnInternal()
        {
            if (NetworkBridge.IsServer)
            {
                TurnManager.Instance.EndTurnLocal();
                BroadcastGameState();
            }
            else
            {
                NetworkClient.Send(new NetMsgEndTurn());
            }
        }

        private void RequestExecuteSkillInternal(CardInstance attacker, int skillIndex, CardInstance target)
        {
            if (NetworkBridge.IsServer)
            {
                CombatManager.Instance.ExecuteSkill(attacker, skillIndex, target);
            }
            else
            {
                NetworkClient.Send(new NetMsgExecuteSkill
                {
                    attackerPlayerID = attacker.ownerPlayerID,
                    attackerSlot     = attacker.slotIndex,
                    skillIndex       = skillIndex,
                    targetPlayerID   = target?.ownerPlayerID ?? -1,
                    targetSlot       = target?.slotIndex     ?? -1,
                });
            }
        }

        // ── Handlers serveur ─────────────────────────────────────────────

        private void OnServerExecuteSkill(NetworkConnectionToClient conn, NetMsgExecuteSkill msg)
        {
            var attacker = BoardManager.Instance?.GetCardAtSlot(msg.attackerPlayerID, msg.attackerSlot);
            if (attacker == null) return;

            CardInstance target = null;
            if (msg.targetPlayerID >= 0)
                target = BoardManager.Instance?.GetCardAtSlot(msg.targetPlayerID, msg.targetSlot);

            CombatManager.Instance.ExecuteSkill(attacker, msg.skillIndex, target);
        }

        private void OnServerEndTurn(NetworkConnectionToClient conn, NetMsgEndTurn msg)
        {
            TurnManager.Instance.EndTurnLocal();
            BroadcastGameState();
        }

        // ── Callback fin d'action ─────────────────────────────────────────

        private void OnActionComplete()
        {
            if (NetworkBridge.IsServer)
                BroadcastGameState();
        }

        // ── Diffusion de l'état ───────────────────────────────────────────

        public void BroadcastGameState()
        {
            if (!NetworkBridge.IsServer) return;
            var json = JsonUtility.ToJson(BuildSnapshot());
            NetworkServer.SendToAll(new NetMsgGameState { stateJson = json });
        }

        private IEnumerator BroadcastAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            BroadcastGameState();
        }

        private GameStateSnapshot BuildSnapshot()
        {
            var elements = (Element[])System.Enum.GetValues(typeof(Element));
            var snap = new GameStateSnapshot
            {
                currentPlayerID  = TurnManager.Instance.currentPlayerID,
                actionsRemaining = TurnManager.Instance.actionsRemaining,
                timerRemaining   = TurnManager.Instance.currentTurnTime,
                stacksP0         = new int[elements.Length],
                stacksP1         = new int[elements.Length],
            };

            for (int i = 0; i < elements.Length; i++)
            {
                snap.stacksP0[i] = StackManager.Instance?.GetStacks(0, elements[i]) ?? 0;
                snap.stacksP1[i] = StackManager.Instance?.GetStacks(1, elements[i]) ?? 0;
            }

            if (BoardManager.Instance != null)
                foreach (var card in BoardManager.Instance.GetAllCards())
                {
                    if (card == null) continue;
                    snap.cards.Add(new CardStateSnapshot
                    {
                        playerID         = card.ownerPlayerID,
                        slotIndex        = card.slotIndex,
                        currentHP        = card.currentHP,
                        currentArmor     = card.currentArmor,
                        isAlive          = card.IsAlive && card.gameObject.activeSelf,
                        hasActedThisTurn = card.hasActedThisTurn,
                        effects          = new List<ActiveEffect>(card.activeEffects),
                    });
                }

            return snap;
        }

        // ── Handler client ────────────────────────────────────────────────

        private void OnClientReceiveGameState(NetMsgGameState msg)
        {
            var snap = JsonUtility.FromJson<GameStateSnapshot>(msg.stateJson);
            if (snap == null) return;
            ApplySnapshot(snap);
        }

        private void ApplySnapshot(GameStateSnapshot snap)
        {
            int prevPlayerID = TurnManager.Instance?.currentPlayerID ?? 0;

            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.currentPlayerID  = snap.currentPlayerID;
                TurnManager.Instance.actionsRemaining = snap.actionsRemaining;
                TurnManager.Instance.currentTurnTime  = snap.timerRemaining;
                CombatUIManager.Instance?.UpdateActionDots();
                CombatUIManager.Instance?.UpdateTurnIndicator(snap.currentPlayerID);
            }

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

            if (BoardManager.Instance == null) return;
            foreach (var cs in snap.cards)
            {
                var card = BoardManager.Instance.GetCardAtSlot(cs.playerID, cs.slotIndex);
                if (card == null) continue;

                bool wasAlive = card.IsAlive && card.gameObject.activeSelf;

                if (!NetworkBridge.IsServer)
                {
                    int hpDelta = cs.currentHP - card.currentHP;
                    if (hpDelta < 0)
                        card.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(-hpDelta);
                    else if (hpDelta > 0)
                        card.GetComponent<CombatPopupHandler>()?.ShowHealPopup(hpDelta);
                }

                card.currentHP        = cs.currentHP;
                card.currentArmor     = cs.currentArmor;
                card.hasActedThisTurn = cs.hasActedThisTurn;
                card.activeEffects    = cs.effects ?? new List<ActiveEffect>();

                if (!cs.isAlive && wasAlive)
                    BoardManager.Instance.DestroyCard(card);

                card.GetComponent<CardVisualUpdater>()?.UpdateVisuals();
            }

            // ── Transition de tour ────────────────────────────────────────
            if (prevPlayerID != snap.currentPlayerID)
            {
                CombatUIManager.Instance?.ClearAllHighlights();
                CombatUIManager.Instance?.CancelSelection();
                _remoteHighlightedCard?.GetComponent<CardTargetHighlight>()?.DeactivateHighlight();
                _remoteHighlightedCard = null;
                _remoteBouncingCard?.GetComponent<CardTargetHighlight>()?.DeactivateBounce();
                _remoteBouncingCard = null;
                TargetingArrow.Instance?.HideStatic();
                TurnAudioManager.Instance?.PlayTurnStart(snap.currentPlayerID);
                CombatLogManager.Instance?.OnTurnChanged(snap.currentPlayerID + 1);
            }
        }

        // ── Utilitaire ────────────────────────────────────────────────────

        private static List<int> ParseCsv(string csv)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(csv)) return result;
            foreach (var s in csv.Split(','))
                if (int.TryParse(s.Trim(), out int n))
                    result.Add(n);
            return result;
        }
    }
}
#endif
