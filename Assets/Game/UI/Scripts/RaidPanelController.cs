using System.Collections.Generic;
using Astraleum.AI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Astraleum.UI
{
    /// <summary>
    /// À attacher sur Panel_Decks (enfant de Panel_Raid). Gère la sélection de deck et le
    /// lancement d'un combat Boss (Voragoth). Volontairement séparé de DeckSelectPanel
    /// (matchmaking PvP/IA, singleton statique) : Panel_Raid a sa propre instance de
    /// deck-slots, et partager DeckSelectPanel.Instance entre deux panels actifs dans la même
    /// scène créerait une collision (le plus récemment activé écrase la référence de l'autre).
    /// </summary>
    public class RaidPanelController : MonoBehaviour
    {
        public static RaidPanelController Instance;

        [Header("Slots — mêmes slotIndex que Panel_DeckEditor")]
        public DeckCardSlot[] deckSlots = new DeckCardSlot[8];

        [Header("Lancement")]
        [Tooltip("Btn_Search — actif après sélection d'un deck.")]
        public Button btnLaunch;

        [Header("Boss")]
        [Tooltip("CardData Phase 1 du Boss sélectionné (Voragoth — seul Boss disponible pour l'instant).")]
        public CardData bossEncounterData;

        [Header("Feedback — optionnel")]
        public TMP_Text feedbackText;

        private DeckCardSlot selectedSlot;

        private void Awake()
        {
            Instance = this;
            btnLaunch?.onClick.AddListener(LaunchRaid);
        }

        private void OnEnable()
        {
            DeckSaveSystem.OnDecksChanged += RefreshAllSlots;

            if (selectedSlot != null)
            {
                selectedSlot.SetSelectedForPlay(false);
                selectedSlot = null;
            }
            SetLaunchInteractable(false);
            ClearFeedback();
            RefreshAllSlots();
        }

        private void OnDisable()
        {
            DeckSaveSystem.OnDecksChanged -= RefreshAllSlots;
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
                SetLaunchInteractable(false);
                ClearFeedback();
                return;
            }

            selectedSlot = slot;
            selectedSlot.SetSelectedForPlay(true);
            SetLaunchInteractable(true);
            ShowFeedback(string.Format(LocalizationManager.Get("ui_deck_selected"), slot.DeckName), true);
        }

        private void LaunchRaid()
        {
            if (bossEncounterData == null)
            {
                ShowFeedback(LocalizationManager.Get("ui_raid_boss_not_configured"), false);
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

            GameModeContext.Mode = GameMode.Boss;
            GameModeContext.PlayerDeckNumbers = new List<int>(cardNumbers);
            GameModeContext.BossEncounterData = bossEncounterData;
            GameModeContext.AIDisplayName = bossEncounterData.cardName;

            if (DeckManager.Instance != null)
            {
                DeckManager.Instance.ClearDeck();
                foreach (var n in cardNumbers)
                    DeckManager.Instance.TryAddCard(n);
            }

            SceneManager.LoadScene("Combat");
        }

        private void SetLaunchInteractable(bool interactable)
        {
            if (btnLaunch != null) btnLaunch.interactable = interactable;
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
