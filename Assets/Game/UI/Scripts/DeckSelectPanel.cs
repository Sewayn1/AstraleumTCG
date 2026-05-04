using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Astraleum.UI
{
    /// <summary>
    /// Gère Panel_DeckSelect dans Panel_Play.
    /// Les Deck_Slot_1..8 sont les mêmes GameObjects (même slotIndex) que dans Panel_DeckEditor :
    /// ils lisent les mêmes PlayerPrefs → affichage identique automatiquement.
    /// Cocher "isSelectionMode" sur chaque DeckCardSlot de ce panel.
    /// </summary>
    public class DeckSelectPanel : MonoBehaviour
    {
        public static DeckSelectPanel Instance;

        [Header("Slots — mêmes slotIndex que Panel_DeckEditor")]
        [Tooltip("Assigner les 8 DeckCardSlot de Panel_DeckSelect (Deck_Slot_1 à Deck_Slot_8).")]
        public DeckCardSlot[] deckSlots = new DeckCardSlot[8];

        [Header("Bouton")]
        [Tooltip("Btn_start — 'Lancer la Recherche'. Inactif jusqu'à la sélection d'un deck.")]
        public Button btnStart;

        [Header("Feedback — optionnel")]
        public TMP_Text feedbackText;

        [Header("Scène de combat")]
        [Tooltip("Nom exact de la scène de combat à charger.")]
        public string combatSceneName = "Combat";

        // ── État interne ──────────────────────────────────────────────
        private DeckCardSlot selectedSlot = null;
        private System.Action<List<int>, string> lobbyCallback = null;

        // ── Init ──────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(false);
        }

        /// <summary>Appelé par Btn_Unranked dans Panel_Play.</summary>
        public void Show()
        {
            lobbyCallback = null;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Ouvre le panel en mode Lobby : le bouton Start appelle le callback
        /// et ferme le panel sans charger de scène.
        /// </summary>
        public void ShowForLobby(System.Action<List<int>, string> callback)
        {
            lobbyCallback = callback;
            gameObject.SetActive(true);
        }

        /// <summary>Appelé par Btn_Cancel ou Btn_Back si présent.</summary>
        public void Hide()
        {
            lobbyCallback = null;
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Astraleum.DeckSaveSystem.OnDecksChanged += RefreshAllSlots;

            if (selectedSlot != null)
            {
                selectedSlot.SetSelectedForPlay(false);
                selectedSlot = null;
            }
            SetStartInteractable(false);
            ClearFeedback();
            RefreshAllSlots();
        }

        private void OnDisable()
        {
            Astraleum.DeckSaveSystem.OnDecksChanged -= RefreshAllSlots;
        }

        private void RefreshAllSlots()
        {
            if (Astraleum.DeckSaveSystem.Instance == null) return;

            foreach (var slot in deckSlots)
            {
                if (slot == null || slot.slotIndex < 0) continue;

                var saved = Astraleum.DeckSaveSystem.Instance.GetDeckBySlot(slot.slotIndex);
                if (saved != null && saved.cardNumbers != null && saved.cardNumbers.Count > 0)
                    slot.LoadFromSave(saved.deckName, saved.cardNumbers, saved.dominantElementIndex);
                else
                    slot.SetEmpty();
            }
        }

        // ── Sélection slot ────────────────────────────────────────────

        /// <summary>Appelé par DeckCardSlot.OnPointerClick quand isSelectionMode = true.</summary>
        public void OnSlotClicked(DeckCardSlot slot)
        {
            if (slot == null) return;

            // Un slot vide ne peut pas être sélectionné
            if (slot.State != DeckSlotState.Saved)
            {
                ShowFeedback("Ce slot est vide. Créez un deck dans le Deck Editor.", false);
                return;
            }

            // Désélectionner le précédent
            if (selectedSlot != null && selectedSlot != slot)
                selectedSlot.SetSelectedForPlay(false);

            // Basculer la sélection sur le même slot
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
            ShowFeedback($"Deck « {slot.DeckName} » sélectionné.", true);

            // Mode Lobby : confirme immédiatement sans bouton Start séparé
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

        /// <summary>Appelé par Btn_start.</summary>
        public void StartGame()
        {
            if (selectedSlot == null)
            {
                ShowFeedback("Sélectionnez un deck.", false);
                return;
            }

            var cardNumbers = selectedSlot.CardNumbers;
            if (cardNumbers == null || cardNumbers.Count == 0)
            {
                ShowFeedback("Ce deck est vide.", false);
                return;
            }

            // Mode Lobby : confirmer la sélection et retourner au panel appelant
            if (lobbyCallback != null)
            {
                var cb   = lobbyCallback;
                var name = selectedSlot.DeckName;
                var nums = new List<int>(cardNumbers);
                Hide();
                cb.Invoke(nums, name);
                return;
            }

            // Mode normal (solo) : charge le deck et lance la scène Combat
            if (Astraleum.DeckManager.Instance != null)
            {
                Astraleum.DeckManager.Instance.ClearDeck();
                foreach (var cardNumber in cardNumbers)
                    Astraleum.DeckManager.Instance.TryAddCard(cardNumber);
            }
            else
            {
                Debug.LogWarning("[DeckSelectPanel] DeckManager.Instance est null — le deck ne sera pas chargé.");
            }

            bool sceneFound = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == combatSceneName) { sceneFound = true; break; }
            }

            if (!sceneFound)
            {
                Debug.LogError($"[DeckSelectPanel] Scène \"{combatSceneName}\" introuvable dans les Build Settings. " +
                               "Ajoute-la via File > Build Settings > Add Open Scenes.");
                ShowFeedback($"Erreur : scène \"{combatSceneName}\" non configurée.", false);
                return;
            }

            SceneManager.LoadScene(combatSceneName);
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
    }
}
