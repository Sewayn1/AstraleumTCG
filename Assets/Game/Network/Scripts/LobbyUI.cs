#if MIRROR
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    /// <summary>
    /// Panel lobby dans la scène MainMenu.
    /// Le GO doit être ACTIF dans la scène (sauvegardé actif) — Awake le cache immédiatement.
    /// btnOuvrir (ex. Btn_DemoLan) peut être dans un autre panel — assigner dans l'inspecteur.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        public static LobbyUI Instance { get; private set; }

        [Header("Champs")]
        [SerializeField] private TMP_InputField inputIP;
        [SerializeField] private TMP_Text       lblStatus;
        [SerializeField] private TMP_Text       lblDeckName;

        [Header("Boutons")]
        [SerializeField] private Button btnHerberger;
        [SerializeField] private Button btnRejoindre;
        [SerializeField] private Button btnQuitter;
        [SerializeField] private Button btnOuvrir;     // Btn_DemoLan — peut être dans un autre panel
        [SerializeField] private Button btnDeckSelect;

        private List<int> _selectedDeck     = null;
        private string    _selectedDeckName = "";

        private void Awake()
        {
            Instance = this;

            btnHerberger?.onClick.AddListener(OnHostClicked);
            btnRejoindre?.onClick.AddListener(OnJoinClicked);
            btnQuitter?.onClick.AddListener(OnQuitClicked);
            btnOuvrir?.onClick.AddListener(OpenLobby);
            btnDeckSelect?.onClick.AddListener(OnDeckSelectClicked);

            gameObject.SetActive(false);
        }

        // ── API publique ─────────────────────────────────────────────────

        public void OpenLobby()
        {
            UpdateDeckLabel();
            SetButtonsInteractable(true);
            SetStatus("");
            gameObject.SetActive(true);
        }

        public void SetStatus(string message)
        {
            if (lblStatus != null)
                lblStatus.text = message;
        }

        // ── Deck select ───────────────────────────────────────────────────

        private void OnDeckSelectClicked()
        {
            var panel = Astraleum.UI.DeckSelectPanel.Instance;
            if (panel == null)
            {
                Debug.LogWarning("[LobbyUI] DeckSelectPanel.Instance est null.");
                return;
            }
            panel.ShowForLobby(OnDeckConfirmed);
        }

        private void OnDeckConfirmed(List<int> cardNumbers, string deckName)
        {
            _selectedDeck     = cardNumbers;
            _selectedDeckName = deckName;

            // Charge immédiatement dans DeckManager — persiste via DontDestroyOnLoad
            var dm = DeckManager.Instance;
            if (dm != null)
            {
                dm.ClearDeck();
                foreach (var num in cardNumbers)
                    dm.TryAddCard(num);
            }

            UpdateDeckLabel();
            SetStatus($"Deck « {deckName} » prêt.");
        }

        private void UpdateDeckLabel()
        {
            if (lblDeckName == null) return;
            lblDeckName.text = string.IsNullOrEmpty(_selectedDeckName)
                ? "Aucun deck sélectionné"
                : _selectedDeckName;
        }

        // ── Actions réseau ────────────────────────────────────────────────

        private void OnHostClicked()
        {
            if (_selectedDeck == null || _selectedDeck.Count == 0)
            { SetStatus("Sélectionnez un deck avant de continuer."); return; }

            // Transmet directement les numéros — bypass DeckManager.TryAddCard
            // qui peut échouer silencieusement si CardDatabase est absent (MainMenu).
            AstraleumNetworkManager.Instance.SetLocalDeck(string.Join(",", _selectedDeck));

            SetStatus("Hébergement en cours… En attente du joueur 2");
            SetButtonsInteractable(false);
            AstraleumNetworkManager.singleton.StartHost();
        }

        private void OnJoinClicked()
        {
            if (_selectedDeck == null || _selectedDeck.Count == 0)
            { SetStatus("Sélectionnez un deck avant de continuer."); return; }

            AstraleumNetworkManager.Instance.SetLocalDeck(string.Join(",", _selectedDeck));

            string ip = inputIP != null ? inputIP.text.Trim() : "localhost";
            if (string.IsNullOrEmpty(ip)) ip = "localhost";

            SetStatus($"Connexion à {ip}…");
            SetButtonsInteractable(false);

            AstraleumNetworkManager.singleton.networkAddress = ip;
            AstraleumNetworkManager.singleton.StartClient();
        }

        private void OnQuitClicked()
        {
            if (NetworkServer.active) AstraleumNetworkManager.singleton.StopHost();
            else if (NetworkClient.active) AstraleumNetworkManager.singleton.StopClient();

            _selectedDeck     = null;
            _selectedDeckName = "";
            SetButtonsInteractable(true);
            SetStatus("");
            gameObject.SetActive(false);
        }

        // ── Utilitaires ───────────────────────────────────────────────────

        private void SetButtonsInteractable(bool interactable)
        {
            if (btnHerberger  != null) btnHerberger.interactable  = interactable;
            if (btnRejoindre  != null) btnRejoindre.interactable  = interactable;
            if (btnDeckSelect != null) btnDeckSelect.interactable = interactable;
        }
    }
}
#endif
