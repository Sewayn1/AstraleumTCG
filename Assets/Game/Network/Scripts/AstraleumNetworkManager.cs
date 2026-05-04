#if MIRROR
using System.Collections.Generic;
using Mirror;
using kcp2k;
using UnityEngine;

namespace Astraleum
{
    [RequireComponent(typeof(KcpTransport))]
    public class AstraleumNetworkManager : NetworkManager
    {
        public static AstraleumNetworkManager Instance { get; private set; }

        private const string COMBAT_SCENE = "Combat";

        private readonly List<NetworkConnectionToClient> remoteConnections
            = new List<NetworkConnectionToClient>();

        // Deck du joueur distant, rempli avant que Combat se charge
        public string opponentDeckCsv { get; private set; } = "";
        // Deck local — défini par LobbyUI avant StartHost/StartClient
        public string localDeckCsv    { get; private set; } = "";

        // ── API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Appelé par LobbyUI avant StartHost/StartClient pour transmettre
        /// les numéros de cartes du joueur local directement (sans passer
        /// par DeckManager qui peut échouer silencieusement en MainMenu).
        /// </summary>
        public void SetLocalDeck(string deckCsv)
        {
            localDeckCsv = deckCsv;
            Debug.Log($"[Net] Deck local défini : {deckCsv}");
        }

        // ── Awake ─────────────────────────────────────────────────────────

        public override void Awake()
        {
            base.Awake();
            Instance = this;
            onlineScene = "";
            autoCreatePlayer = false;
        }

        // ── Serveur ───────────────────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();
            remoteConnections.Clear();
            opponentDeckCsv = "";

            NetworkServer.RegisterHandler<NetMsgDeckInfo>(OnServerReceiveDeckInfo);
            Debug.Log("[Net] Serveur démarré — en attente du 2e joueur…");
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);

            if (conn is LocalConnectionToClient) return;

            remoteConnections.Add(conn);
            Debug.Log($"[Net] Client distant connecté ({remoteConnections.Count}/1 requis)");

            // Envoie le deck du serveur (P1) au client qui vient de se connecter
            conn.Send(new NetMsgDeckInfo
            {
                playerID       = 0,
                cardNumbersCsv = localDeckCsv,
            });
        }

        private void OnServerReceiveDeckInfo(NetworkConnectionToClient conn, NetMsgDeckInfo msg)
        {
            // Le serveur reçoit le deck du client (P2)
            opponentDeckCsv = msg.cardNumbersCsv;
            Debug.Log($"[Net] Deck J2 reçu ({msg.cardNumbersCsv}) — lancement de Combat");

            ServerChangeScene(COMBAT_SCENE);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            remoteConnections.Remove(conn);
            Debug.Log("[Net] Un client s'est déconnecté.");
        }

        // ── Client ────────────────────────────────────────────────────────

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<NetMsgDeckInfo>(OnClientReceiveP1Deck);
        }

        private void OnClientReceiveP1Deck(NetMsgDeckInfo msg)
        {
            // Le client reçoit le deck du serveur (P1), stocke-le comme adversaire
            opponentDeckCsv = msg.cardNumbersCsv;
            Debug.Log($"[Net] Deck J1 reçu ({msg.cardNumbersCsv}) — envoi du deck J2");

            // Répond avec son propre deck
            NetworkClient.Send(new NetMsgDeckInfo
            {
                playerID       = 1,
                cardNumbersCsv = localDeckCsv,
            });
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            LobbyUI.Instance?.SetStatus("Connecté ! En attente du démarrage…");
            Debug.Log("[Net] Connecté au serveur.");
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            LobbyUI.Instance?.SetStatus("Déconnecté.");
            Debug.Log("[Net] Déconnecté du serveur.");
        }

        // Pas d'objets joueur — court-circuite le système par défaut
        public override void OnServerAddPlayer(NetworkConnectionToClient conn) { }
    }
}
#endif
