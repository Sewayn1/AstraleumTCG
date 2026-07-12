using System.Collections;
using System.Collections.Generic;
using Astraleum;
using Astraleum.AI;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Astraleum.UI
{
    public class DeckSelectPanel : MonoBehaviour
    {
        public static DeckSelectPanel Instance;

        [Header("Slots — mêmes slotIndex que Panel_DeckEditor")]
        [Tooltip("Assigner les 8 DeckCardSlot de Panel_DeckSelect (Deck_Slot_1 à Deck_Slot_8).")]
        public DeckCardSlot[] deckSlots = new DeckCardSlot[8];

        [Header("Bouton")]
        [Tooltip("Btn_Search — actif après sélection d'un deck.")]
        public Button btnStart;

        [Header("Mode PvP — annule la sélection IA en cours")]
        [Tooltip("Btn_Normal (\"Partie Normale\") — revient au matchmaking standard.")]
        public Button btnNormal;

        [Header("Mode vs IA — indépendant du matchmaking PvP")]
        [Tooltip("Btn_TrainingInfinite (\"Tester mon Deck\") — adversaire immortel, ne joue jamais.")]
        public Button btnSandbox;
        public Button btnEasyAI;
        public Button btnMediumAI;
        public Button btnHardAI;
        [Tooltip("DropdownToggle de Btn_Training — replié avec la même animation quand on revient en mode Normal.")]
        public DropdownToggle trainingDropdown;
        [Tooltip("GamemodeTitle — affiche le mode sélectionné (PvP normal ou IA).")]
        public LocalizedText gamemodeTitleText;
        [Tooltip("Text (TMP) enfant de Btn_Search — bascule sur \"Affronter une IA\" quand un mode IA est sélectionné.")]
        public LocalizedText searchButtonLabel;

        [Header("Réseau")]
        [Tooltip("Label d'état de la recherche dans Panel_Play.")]
        public TMP_Text searchLabel;
        [Tooltip("Panel_Launch avec le compte à rebours.")]
        public LaunchPanel launchPanel;

        [Header("Feedback — optionnel")]
        public TMP_Text feedbackText;

        // ── État interne ──────────────────────────────────────────────
        private DeckCardSlot selectedSlot = null;
        private System.Action<List<int>, string> lobbyCallback = null;
        private GameMode? pendingAIMode = null;

        // ── Réseau ────────────────────────────────────────────────────
        private static readonly string SessionToken = System.Guid.NewGuid().ToString();
        private int       _createdRoomId  = -1;
        private bool      _waitingForGame = false;
        private bool      _pendingSearch  = false;
        public  bool      IsLaunching     { get; private set; }
        private Coroutine _delayCoroutine = null;
        private const float SEARCH_TIMEOUT = 120f;

        // ── Init ──────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            btnStart?.onClick.AddListener(StartGame);

            btnNormal?.onClick.AddListener(SelectNormalMode);

            btnSandbox?.onClick.AddListener(() => SelectAIMode(GameMode.AISandbox));
            btnEasyAI?.onClick.AddListener(() => SelectAIMode(GameMode.AIEasy));
            btnMediumAI?.onClick.AddListener(() => SelectAIMode(GameMode.AIMedium));
            btnHardAI?.onClick.AddListener(() => SelectAIMode(GameMode.AIHard));
        }

        // ── Sélection du mode IA — met à jour le titre + le bouton "Chercher",
        // le lancement réel se fait au clic sur Btn_Search (StartGame()) ──────
        public void SelectAIMode(GameMode mode)
        {
            pendingAIMode = mode;

            string gamemodeKey = mode switch
            {
                GameMode.AISandbox => "ui_gamemode_sandbox",
                GameMode.AIEasy    => "ui_gamemode_easyai",
                GameMode.AIMedium  => "ui_gamemode_mediumai",
                GameMode.AIHard    => "ui_gamemode_hardai",
                _ => "ui_gamemode_normal",
            };
            gamemodeTitleText?.SetKey(gamemodeKey);
            searchButtonLabel?.SetKey("ui_btn_fight_ai");
        }

        // ── Retour au matchmaking PvP standard (Btn_Normal) — annule un mode IA en attente ──
        public void SelectNormalMode() => ResetGameModeSelection();

        private void ResetGameModeSelection()
        {
            pendingAIMode = null;
            gamemodeTitleText?.SetKey("ui_gamemode_normal");
            searchButtonLabel?.SetKey("ui_btn_search");
            trainingDropdown?.ForceCollapse();
        }

        public void ResetSelection()
        {
            lobbyCallback = null;
            if (selectedSlot != null)
            {
                selectedSlot.SetSelectedForPlay(false);
                selectedSlot = null;
            }
            SetStartInteractable(false);
            ClearFeedback();
            ResetGameModeSelection();
        }

        public void ShowForLobby(System.Action<List<int>, string> callback)
        {
            lobbyCallback = callback;
        }

        public void Hide()
        {
            lobbyCallback = null;
        }

        private void OnEnable()
        {
            DeckSaveSystem.OnDecksChanged += RefreshAllSlots;

            if (selectedSlot != null)
            {
                selectedSlot.SetSelectedForPlay(false);
                selectedSlot = null;
            }
            SetStartInteractable(false);
            ClearFeedback();
            ResetGameModeSelection();
            RefreshAllSlots();
        }

        private void OnDisable()
        {
            DeckSaveSystem.OnDecksChanged -= RefreshAllSlots;
        }

        private void OnDestroy()
        {
            // Désabonnement propre — évite NullReferenceException si GameCancelled arrive après destruction
            var client = SignalRGameClient.Instance;
            if (client != null)
            {
                client.OnGameSetup     -= OnGameSetupReceived;
                client.OnGameCancelled -= OnGameCancelledReceived;
            }
        }

        private void RefreshAllSlots()
        {
            if (DeckSaveSystem.Instance == null) return;

            foreach (var slot in deckSlots)
            {
                if (slot == null || slot.slotIndex < 0) continue;

                var saved = DeckSaveSystem.Instance.GetDeckBySlot(slot.slotIndex);
                if (saved != null && saved.cardNumbers != null && saved.cardNumbers.Count > 0)
                    slot.LoadFromSave(saved.deckName, saved.cardNumbers, saved.dominantElementIndex);
                else
                    slot.SetEmpty();
            }
        }

        // ── Sélection slot ────────────────────────────────────────────

        public void OnSlotClicked(DeckCardSlot slot)
        {
            if (slot == null) return;

            if (slot.State != DeckSlotState.Saved)
            {
                ShowFeedback(LocalizationManager.Get("ui_deck_slot_empty_hint"), false);
                return;
            }

            if (selectedSlot != null && selectedSlot != slot)
                selectedSlot.SetSelectedForPlay(false);

            if (selectedSlot == slot)
            {
                selectedSlot.SetSelectedForPlay(false);
                selectedSlot = null;
                SetStartInteractable(false);
                ClearFeedback();
                return;
            }

            selectedSlot = slot;
            selectedSlot.SetSelectedForPlay(true);
            SetStartInteractable(true);
            ShowFeedback(string.Format(LocalizationManager.Get("ui_deck_selected"), slot.DeckName), true);

            if (lobbyCallback != null)
            {
                var cb   = lobbyCallback;
                var name = slot.DeckName;
                var nums = new List<int>(slot.CardNumbers);
                Hide();
                cb.Invoke(nums, name);
            }
        }

        // ── Lancement ─────────────────────────────────────────────────

        public void StartGame()
        {
            if (pendingAIMode.HasValue)
            {
                StartGameVsAI(pendingAIMode.Value);
                return;
            }

            if (_waitingForGame || _pendingSearch)
            {
                CancelAll();
                return;
            }

            if (selectedSlot == null)
            {
                ShowFeedback(LocalizationManager.Get("ui_select_deck_prompt"), false);
                return;
            }

            var cardNumbers = selectedSlot.CardNumbers;
            if (cardNumbers == null || cardNumbers.Count == 0)
            {
                ShowFeedback(LocalizationManager.Get("ui_deck_empty_error"), false);
                return;
            }

            // Mode Lobby (callback externe)
            if (lobbyCallback != null)
            {
                var cb   = lobbyCallback;
                var name = selectedSlot.DeckName;
                var nums = new List<int>(cardNumbers);
                Hide();
                cb.Invoke(nums, name);
                return;
            }

            // Charger le deck dans DeckManager
            if (DeckManager.Instance != null)
            {
                DeckManager.Instance.ClearDeck();
                foreach (var n in cardNumbers)
                    DeckManager.Instance.TryAddCard(n);
            }

            // Délai anti-clic accidentel puis recherche réseau
            _pendingSearch  = true;
            _delayCoroutine = StartCoroutine(SearchDelayRoutine(
                new List<int>(cardNumbers), selectedSlot.DeckName));
        }

        // ── Mode solo vs IA — indépendant du flux matchmaking PvP ci-dessus ──

        public void StartGameVsAI(GameMode mode)
        {
            if (selectedSlot == null)
            {
                ShowFeedback(LocalizationManager.Get("ui_select_deck_prompt"), false);
                return;
            }

            var cardNumbers = selectedSlot.CardNumbers;
            if (cardNumbers == null || cardNumbers.Count == 0)
            {
                ShowFeedback(LocalizationManager.Get("ui_deck_empty_error"), false);
                return;
            }

            GameModeContext.Mode = mode;
            GameModeContext.PlayerDeckNumbers = new List<int>(cardNumbers);
            GameModeContext.AIDeckNumbers = AIDeckBuilder.BuildDeck(mode, GameModeContext.PlayerDeckNumbers);
            GameModeContext.AIDisplayName = mode switch
            {
                GameMode.AISandbox => "Bac à sable",
                GameMode.AIEasy    => "IA Facile",
                GameMode.AIMedium  => "IA Moyenne",
                GameMode.AIHard    => "IA Difficile",
                _ => "IA",
            };

            if (DeckManager.Instance != null)
            {
                DeckManager.Instance.ClearDeck();
                foreach (var n in cardNumbers)
                    DeckManager.Instance.TryAddCard(n);
            }

            SceneManager.LoadScene("Combat");
        }

        private IEnumerator SearchDelayRoutine(List<int> cardNumbers, string deckName)
        {
            for (int i = 3; i >= 1; i--)
            {
                SetNetworkStatus(string.Format(LocalizationManager.Get("ui_lobby_launch_countdown"), i));
                yield return new WaitForSeconds(1f);
            }
            _pendingSearch  = false;
            _delayCoroutine = null;
            StartNetworkSearch(cardNumbers, deckName);
        }

        public void CancelAll()
        {
            if (_pendingSearch)
            {
                _pendingSearch = false;
                if (_delayCoroutine != null)
                {
                    StopCoroutine(_delayCoroutine);
                    _delayCoroutine = null;
                }
                SetStartInteractable(selectedSlot != null);
                SetNetworkStatus(LocalizationManager.Get("ui_lobby_search_cancelled"));
                return;
            }
            OnCancelCountdown();
        }

        // ── Recherche réseau ──────────────────────────────────────────

        private async void StartNetworkSearch(List<int> cardNumbers, string deckName)
        {
            _waitingForGame = true;
            SetStartInteractable(false);
            SetNetworkStatus(LocalizationManager.Get("ui_lobby_connecting"));

            var client = SignalRGameClient.Instance;
            if (client == null)
            {
                ResetSearch("SignalRGameClient introuvable.");
                return;
            }

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
                ResetSearch("Connexion échouée : " + ex.Message);
                return;
            }

            SetNetworkStatus(LocalizationManager.Get("ui_lobby_searching"));
            StartCoroutine(FindOrCreateRoom(cardNumbers, deckName));
            StartCoroutine(SearchTimeoutRoutine());
        }

        public void OnCancelCountdown()
        {
            _waitingForGame = false;
            IsLaunching     = false;
            StopAllCoroutines();
            launchPanel?.Hide();
            SetStartInteractable(selectedSlot != null);

            var client = SignalRGameClient.Instance;
            if (client != null)
            {
                client.OnGameSetup     -= OnGameSetupReceived;
                client.OnGameCancelled -= OnGameCancelledReceived;
                if (client.IsConnected)
                    try { _ = client.CancelGame(); } catch { }
                _ = client.DisconnectAsync();
            }

            int id = _createdRoomId;
            _createdRoomId = -1;
            if (id >= 0) StartCoroutine(DeleteRoom(id));

            SetNetworkStatus("Recherche annulée.");
        }

        private void ResetSearch(string message)
        {
            _waitingForGame = false;
            IsLaunching     = false;
            StopAllCoroutines();
            launchPanel?.Hide();

            var client = SignalRGameClient.Instance;
            if (client != null)
            {
                client.OnGameSetup     -= OnGameSetupReceived;
                client.OnGameCancelled -= OnGameCancelledReceived;
                // Ne PAS déconnecter ici : si GameCancelled arrive pendant le chargement du combat,
                // appeler DisconnectAsync() tuerait la connexion active en pleine partie.
                // ConnectAsync() se charge de disposer l'ancienne connexion au prochain lancement.
            }

            int id = _createdRoomId;
            _createdRoomId = -1;
            if (id >= 0) StartCoroutine(DeleteRoom(id));

            SetStartInteractable(selectedSlot != null);
            if (!string.IsNullOrEmpty(message)) SetNetworkStatus(message);
        }

        private void OnGameSetupReceived(Astraleum.Core.GameSetupMessage msg)
        {
            if (!_waitingForGame) return;
            _waitingForGame = false;
            IsLaunching     = true;

            int id = _createdRoomId;
            _createdRoomId = -1;
            if (id >= 0) StartCoroutine(DeleteRoom(id));

            SetNetworkStatus(LocalizationManager.Get("ui_lobby_found"));
            launchPanel?.Show(() => OnCancelCountdown());
        }

        private void OnGameCancelledReceived()
        {
            ResetSearch(LocalizationManager.Get("ui_lobby_cancelled"));
        }

        private IEnumerator SearchTimeoutRoutine()
        {
            yield return new WaitForSeconds(SEARCH_TIMEOUT);
            if (_waitingForGame)
                ResetSearch(LocalizationManager.Get("ui_lobby_timeout"));
        }

        // ── Matchmaking HTTP ──────────────────────────────────────────

        private IEnumerator FindOrCreateRoom(List<int> cardNumbers, string deckName)
        {
            UnityWebRequest listReq = UnityWebRequest.Get(NetworkConfig.Data.matchmakingUrl + "/rooms");
            yield return listReq.SendWebRequest();

            if (listReq.result != UnityWebRequest.Result.Success)
            {
                ResetSearch("Impossible de contacter le serveur :\n" + listReq.error);
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
                        SetNetworkStatus(LocalizationManager.Get("ui_lobby_found_connecting"));
                        SendJoinGame(target.id.ToString(), cardNumbers, deckName);
                        yield break;
                    }
                }
            }

            yield return StartCoroutine(CreateRoomAndWait(cardNumbers, deckName));
        }

        private IEnumerator CreateRoomAndWait(List<int> cardNumbers, string deckName)
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
                ResetSearch("Impossible de créer la partie :\n" + req.error);
                req.Dispose();
                yield break;
            }

            RoomData room = JsonUtility.FromJson<RoomData>(req.downloadHandler.text);
            req.Dispose();

            _createdRoomId = room.id;
            SetNetworkStatus(LocalizationManager.Get("ui_lobby_waiting"));
            SendJoinGame(room.id.ToString(), cardNumbers, deckName);

            while (_waitingForGame)
            {
                yield return new WaitForSeconds(12f);
                if (!_waitingForGame) yield break;
                yield return StartCoroutine(TryJoinExistingRoom(cardNumbers, deckName));
            }
        }

        private IEnumerator TryJoinExistingRoom(List<int> cardNumbers, string deckName)
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
                SetNetworkStatus(LocalizationManager.Get("ui_lobby_found_connecting"));
                SendJoinGame(target.id.ToString(), cardNumbers, deckName);
            }
        }

        private void SendJoinGame(string roomId, List<int> cardNumbers, string deckName)
        {
            string playerName = PlayerPrefs.GetString("PlayerName", "Joueur");
            _ = SignalRGameClient.Instance?.JoinGame(roomId, playerName, cardNumbers);
        }

        private IEnumerator DeleteRoom(int id)
        {
            UnityWebRequest req = UnityWebRequest.Delete(
                NetworkConfig.Data.matchmakingUrl + "/rooms/" + id);
            yield return req.SendWebRequest();
            req.Dispose();
        }

        private void SetNetworkStatus(string msg)
        {
            if (searchLabel != null) searchLabel.text = msg;
        }

        // ── Utilitaires ───────────────────────────────────────────────

        private void SetStartInteractable(bool interactable)
        {
            if (btnStart != null)
                btnStart.interactable = interactable;
        }

        private void ShowFeedback(string message, bool success)
        {
            if (feedbackText == null) return;
            feedbackText.text  = message;
            feedbackText.color = success
                ? new Color(0.4f, 0.9f, 0.4f)
                : new Color(1f, 0.4f, 0.4f);
        }

        private void ClearFeedback()
        {
            if (feedbackText != null) feedbackText.text = "";
        }

        // ── JSON helpers ──────────────────────────────────────────────

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
