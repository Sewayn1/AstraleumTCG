using System.Collections;
using System.Collections.Generic;
using Astraleum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Astraleum
{
    public class LobbyUI : MonoBehaviour
    {
        public static LobbyUI Instance { get; private set; }

        [Header("Labels")]
        [SerializeField] private TMP_Text lblStatus;
        [SerializeField] private TMP_Text lblDeckName;

        [Header("Boutons")]
        [SerializeField] private Button btnSearch;
        [SerializeField] private Button btnQuitter;
        [SerializeField] private Button btnOuvrir;
        [SerializeField] private Button btnDeckSelect;

        [Header("Lancement")]
        [SerializeField] private LaunchPanel launchPanel;

        [Header("Annulation adversaire")]
        [SerializeField] private CancelledGamePanel panelCancelledGame;

        private List<int> _selectedDeck     = null;
        private string    _selectedDeckName = "";
        private int       _createdRoomId    = -1;
        private bool      _waitingForGame   = false;

        private const float SEARCH_TIMEOUT = 120f;

        // Token unique par processus — évite le self-join quand deux builds tournent sur la même machine
        private static readonly string SessionToken = System.Guid.NewGuid().ToString();

        private void Awake()
        {
            Instance = this;
            btnSearch?.onClick.AddListener(OnSearchClicked);
            btnQuitter?.onClick.AddListener(OnQuitClicked);
            btnOuvrir?.onClick.AddListener(OpenLobby);
            btnDeckSelect?.onClick.AddListener(OnDeckSelectClicked);
            gameObject.SetActive(false);
        }

        // ── API publique ──────────────────────────────────────────────────

        public void OpenLobby()
        {
            UpdateDeckLabel();
            SetButtonsInteractable(true);
            SetStatus("");
            gameObject.SetActive(true);
        }

        public void SetStatus(string message)
        {
            if (lblStatus != null) lblStatus.text = message;
        }

        public void OnGameCancelled()
        {
            CancelSearch(LocalizationManager.Get("ui_lobby_cancelled"));
        }

        public async void OnCancelCountdown()
        {
            _waitingForGame = false;
            StopAllCoroutines();
            launchPanel?.Hide();
            SetButtonsInteractable(true);

            var client = SignalRGameClient.Instance;
            if (client != null)
            {
                client.OnGameSetup     -= OnGameSetupReceived;
                client.OnGameCancelled -= OnGameCancelledReceived;

                if (client.IsConnected)
                    try { await client.CancelGame(); } catch { }

                _ = client.DisconnectAsync();
            }

            int id = _createdRoomId;
            _createdRoomId = -1;
            if (id >= 0)
            {
                var runner = SignalRGameClient.Instance as MonoBehaviour;
                if (runner != null) runner.StartCoroutine(DeleteRoom(id));
                else if (gameObject.activeInHierarchy) StartCoroutine(DeleteRoom(id));
            }

            SetStatus("Recherche annulée.");
        }

        // ── Deck select ───────────────────────────────────────────────────

        private void OnDeckSelectClicked()
        {
            var panel = UI.DeckSelectPanel.Instance;
            if (panel == null) { Debug.LogWarning("[LobbyUI] DeckSelectPanel.Instance est null."); return; }
            panel.ShowForLobby(OnDeckConfirmed);
        }

        private void OnDeckConfirmed(List<int> cardNumbers, string deckName)
        {
            _selectedDeck     = cardNumbers;
            _selectedDeckName = deckName;

            var dm = DeckManager.Instance;
            if (dm != null)
            {
                dm.ClearDeck();
                foreach (var num in cardNumbers) dm.TryAddCard(num);
            }

            UpdateDeckLabel();
            SetStatus(LocalizationManager.Get("ui_lobby_deck_ready", deckName));
        }

        private void UpdateDeckLabel()
        {
            if (lblDeckName == null) return;
            lblDeckName.text = string.IsNullOrEmpty(_selectedDeckName)
                ? LocalizationManager.Get("ui_lobby_no_deck_selected")
                : _selectedDeckName;
        }

        // ── Recherche de partie ───────────────────────────────────────────

        private async void OnSearchClicked()
        {
            if (!ValidateDeck()) return;
            SetButtonsInteractable(false);
            SetStatus(LocalizationManager.Get("ui_lobby_connecting"));
            _waitingForGame = true;

            var client = SignalRGameClient.Instance;
            if (client == null) { CancelSearch("SignalRGameClient introuvable !"); return; }

            client.OnGameSetup     += OnGameSetupReceived;
            client.OnGameCancelled += OnGameCancelledReceived;

            try
            {
                await client.ConnectAsync(NetworkConfig.Data.signalRUrl);
            }
            catch (System.Exception ex)
            {
                client.OnGameSetup     -= OnGameSetupReceived;
                client.OnGameCancelled -= OnGameCancelledReceived;
                CancelSearch("Connexion échouée : " + ex.Message);
                return;
            }

            SetStatus(LocalizationManager.Get("ui_lobby_searching"));
            StartCoroutine(FindOrCreateRoom());
            StartCoroutine(SearchTimeoutRoutine());
        }

        private void OnQuitClicked()
        {
            CancelSearch("");
            gameObject.SetActive(false);
        }

        // ── Timeout ───────────────────────────────────────────────────────

        private IEnumerator SearchTimeoutRoutine()
        {
            yield return new WaitForSeconds(SEARCH_TIMEOUT);
            if (_waitingForGame)
                CancelSearch(LocalizationManager.Get("ui_lobby_timeout"));
        }

        public void CancelSearch(string message)
        {
            _waitingForGame = false;
            StopAllCoroutines();
            launchPanel?.Hide();

            var client = SignalRGameClient.Instance;
            if (client != null)
            {
                client.OnGameSetup     -= OnGameSetupReceived;
                client.OnGameCancelled -= OnGameCancelledReceived;
                _ = client.DisconnectAsync();
            }

            int id = _createdRoomId;
            _createdRoomId = -1;
            // Panel peut être inactif ici — déléguer à SignalRGameClient (DontDestroyOnLoad)
            if (id >= 0)
            {
                var runner = SignalRGameClient.Instance as MonoBehaviour;
                if (runner != null) runner.StartCoroutine(DeleteRoom(id));
                else if (gameObject.activeInHierarchy) StartCoroutine(DeleteRoom(id));
            }

            SetButtonsInteractable(true);
            if (!string.IsNullOrEmpty(message)) SetStatus(message);
        }

        // ── Réception GameSetup (SignalR) ─────────────────────────────────

        private void OnGameSetupReceived(GameSetupMessage msg)
        {
            if (!_waitingForGame) return;
            _waitingForGame = false;

            // La salle HTTP n'est plus nécessaire (le serveur gère la session SignalR)
            int id = _createdRoomId;
            _createdRoomId = -1;
            if (id >= 0) StartCoroutine(DeleteRoom(id));

            SetStatus(LocalizationManager.Get("ui_lobby_found"));
            launchPanel?.Show();
        }

        private void OnGameCancelledReceived()
        {
            CancelSearch("");
            gameObject.SetActive(false);
            panelCancelledGame?.Show();
        }

        // ── Matchmaking HTTP ──────────────────────────────────────────────

        private IEnumerator FindOrCreateRoom()
        {
            // Vérification initiale que le serveur répond
            UnityWebRequest listReq = UnityWebRequest.Get(NetworkConfig.Data.matchmakingUrl + "/rooms");
            yield return listReq.SendWebRequest();

            if (listReq.result != UnityWebRequest.Result.Success)
            {
                CancelSearch("Impossible de contacter le serveur :\n" + listReq.error);
                listReq.Dispose();
                yield break;
            }

            string          json    = "{\"rooms\":" + listReq.downloadHandler.text + "}";
            RoomListWrapper wrapper = JsonUtility.FromJson<RoomListWrapper>(json);
            listReq.Dispose();

            if (wrapper?.rooms != null && wrapper.rooms.Length > 0)
            {
                RoomData target = System.Array.Find(wrapper.rooms, r => r.sessionToken != SessionToken);
                if (target != null)
                {
                    string joinUrl  = NetworkConfig.Data.matchmakingUrl + "/rooms/" + target.id + "/join";
                    byte[] joinBody = System.Text.Encoding.UTF8.GetBytes(
                        "{\"sessionToken\":\"" + SessionToken + "\"}");
                    UnityWebRequest joinReq = new UnityWebRequest(joinUrl, "POST");
                    joinReq.uploadHandler   = new UploadHandlerRaw(joinBody);
                    joinReq.downloadHandler = new DownloadHandlerBuffer();
                    joinReq.SetRequestHeader("Content-Type", "application/json");
                    yield return joinReq.SendWebRequest();
                    bool ok = joinReq.result == UnityWebRequest.Result.Success;
                    joinReq.Dispose();

                    if (ok)
                    {
                        SetStatus(LocalizationManager.Get("ui_lobby_found_connecting"));
                        SendJoinGame(target.id.ToString());
                        yield break;
                    }
                }
            }

            yield return StartCoroutine(CreateRoomAndWait());
        }

        private IEnumerator CreateRoomAndWait()
        {
            string playerName = PlayerPrefs.GetString("PlayerName", "Joueur");
            byte[] bodyBytes  = System.Text.Encoding.UTF8.GetBytes(
                "{\"playerName\":\"" + playerName + "\",\"sessionToken\":\"" + SessionToken + "\"}");

            UnityWebRequest req = new UnityWebRequest(NetworkConfig.Data.matchmakingUrl + "/rooms", "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                CancelSearch("Impossible de créer la partie :\n" + req.error);
                req.Dispose();
                yield break;
            }

            RoomData room = JsonUtility.FromJson<RoomData>(req.downloadHandler.text);
            req.Dispose();

            _createdRoomId = room.id;
            SetStatus(LocalizationManager.Get("ui_lobby_waiting"));
            SendJoinGame(room.id.ToString());

            // Retry toutes les 12s — couvre la race condition où les deux ont créé une room en même temps
            while (_waitingForGame)
            {
                yield return new WaitForSeconds(12f);
                if (!_waitingForGame) yield break;
                yield return StartCoroutine(TryJoinExistingRoom());
            }
        }

        // Tente de rejoindre une room existante (autre que la nôtre). Retourne true si rejoint.
        private IEnumerator TryJoinExistingRoom()
        {
            UnityWebRequest listReq = UnityWebRequest.Get(NetworkConfig.Data.matchmakingUrl + "/rooms");
            yield return listReq.SendWebRequest();
            if (listReq.result != UnityWebRequest.Result.Success) { listReq.Dispose(); yield break; }

            string json = "{\"rooms\":" + listReq.downloadHandler.text + "}";
            RoomListWrapper wrapper = JsonUtility.FromJson<RoomListWrapper>(json);
            listReq.Dispose();

            if (wrapper?.rooms == null) yield break;
            RoomData target = System.Array.Find(wrapper.rooms,
                r => r.sessionToken != SessionToken && r.id != _createdRoomId);
            if (target == null) yield break;

            string joinUrl = NetworkConfig.Data.matchmakingUrl + "/rooms/" + target.id + "/join";
            byte[] body    = System.Text.Encoding.UTF8.GetBytes("{\"sessionToken\":\"" + SessionToken + "\"}");
            UnityWebRequest joinReq = new UnityWebRequest(joinUrl, "POST");
            joinReq.uploadHandler   = new UploadHandlerRaw(body);
            joinReq.downloadHandler = new DownloadHandlerBuffer();
            joinReq.SetRequestHeader("Content-Type", "application/json");
            yield return joinReq.SendWebRequest();

            bool ok = joinReq.result == UnityWebRequest.Result.Success;
            joinReq.Dispose();

            if (ok)
            {
                SetStatus(LocalizationManager.Get("ui_lobby_found_connecting"));
                SendJoinGame(target.id.ToString());
            }
        }

        private void SendJoinGame(string roomId)
        {
            string playerName = PlayerPrefs.GetString("PlayerName", "Joueur");
            _ = SignalRGameClient.Instance?.JoinGame(roomId, playerName, _selectedDeck);
        }

        private IEnumerator DeleteRoom(int id)
        {
            UnityWebRequest req = UnityWebRequest.Delete(
                NetworkConfig.Data.matchmakingUrl + "/rooms/" + id);
            yield return req.SendWebRequest();
            req.Dispose();
        }

        // ── Utilitaires ───────────────────────────────────────────────────

        private bool ValidateDeck()
        {
            if (_selectedDeck == null || _selectedDeck.Count == 0)
            {
                SetStatus(LocalizationManager.Get("ui_lobby_no_deck"));
                return false;
            }
            return true;
        }

        private void SetButtonsInteractable(bool v)
        {
            if (btnSearch     != null) btnSearch.interactable     = v;
            if (btnDeckSelect != null) btnDeckSelect.interactable = v;
        }

        // ── JSON helpers ──────────────────────────────────────────────────

        [System.Serializable]
        private class RoomData
        {
            public int    id;
            public string sessionToken;
        }

        [System.Serializable]
        private class RoomListWrapper
        {
            public RoomData[] rooms;
        }
    }
}
