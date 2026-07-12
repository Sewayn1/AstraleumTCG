using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Astraleum.UI
{
    /// <summary>
    /// Gestionnaire inline de deck dans Panel_DecksSlots.
    /// Affiche 8 slots ; clic → EditZone dropdown avec 5 CardSelectSlots.
    /// Cartes ajoutées gauche→droite depuis CardsScrollView.
    /// </summary>
    public class DeckSlotsManager : MonoBehaviour
    {
        public static DeckSlotsManager Instance;

        // true pendant qu'un slot est en cours d'édition
        public static bool IsEditing => Instance != null && Instance._isEditing;

        [Header("8 Deck Slots")]
        public DeckCardSlot[] deckSlots = new DeckCardSlot[8];

        [Header("EditZone — dropdown inline")]
        [Tooltip("Panel qui s'anime en dropdown. Doit avoir un CanvasGroup.")]
        public RectTransform editZone;
        [Tooltip("5 CardSelectSlots dans l'EditZone (ordre 0→4).")]
        public CardSelectSlot[] cardSelectSlots = new CardSelectSlot[5];
        public TMP_InputField deckNameInput;
        public Button btnSave;
        public Button btnDelete;
        public TMP_Text feedbackText;

        // ── État interne ──────────────────────────────────────────────
        private Dictionary<int, CardData> _cardLookup = new();
        private DeckCardSlot  _selectedSlot;
        private CardData[]    _editCards = new CardData[5];
        private bool          _isEditing;
        private bool          _selectedSlotWasSaved;
        private CanvasGroup   _editZoneCG;

        private const int MAX_SUPREME    = 1;
        private const int MAX_LEGENDAIRE = 1;

        // ── Init ──────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            BuildCardLookup();

            if (editZone != null)
            {
                _editZoneCG = editZone.GetComponent<CanvasGroup>();
                if (_editZoneCG == null) _editZoneCG = editZone.gameObject.AddComponent<CanvasGroup>();
                editZone.gameObject.SetActive(false);
            }

            if (btnSave   != null) btnSave.onClick.AddListener(OnBtnSave);
            if (btnDelete != null) btnDelete.onClick.AddListener(DeleteSelectedDeck);
            if (deckNameInput != null) deckNameInput.onValueChanged.AddListener(_ => UpdateSaveButton());
        }

        private void Start()
        {
            LoadSavedDecksIntoSlots();
        }

        private void OnEnable()
        {
            DeckSaveSystem.OnDecksChanged += LoadSavedDecksIntoSlots;
            LoadSavedDecksIntoSlots();
        }

        private void OnDisable()
        {
            DeckSaveSystem.OnDecksChanged -= LoadSavedDecksIntoSlots;
            if (_isEditing) CloseEditZoneImmediate();
        }

        private void BuildCardLookup()
        {
            _cardLookup.Clear();
            var cards = CardDatabase.LoadVisibleCards();
            foreach (var c in cards)
                if (!_cardLookup.ContainsKey(c.cardNumber))
                    _cardLookup[c.cardNumber] = c;
        }

        private CardData GetCard(int num) =>
            _cardLookup.TryGetValue(num, out var c) ? c : null;

        // ── Chargement sauvegardé ─────────────────────────────────────

        private void LoadSavedDecksIntoSlots()
        {
            if (DeckSaveSystem.Instance != null)
            {
                for (int i = 0; i < deckSlots.Length; i++)
                {
                    if (deckSlots[i] == null || deckSlots[i] == _selectedSlot) continue;
                    var saved = DeckSaveSystem.Instance.GetDeckBySlot(i);
                    if (saved != null && saved.cardNumbers?.Count > 0)
                        deckSlots[i].LoadFromSave(saved.deckName, saved.cardNumbers, saved.dominantElementIndex);
                    else
                        deckSlots[i].SetEmpty();
                }
            }
            UpdateSlotVisibility();
        }

        private void UpdateSlotVisibility()
        {
            int highestSaved = -1;
            for (int i = 0; i < deckSlots.Length; i++)
                if (deckSlots[i] != null && deckSlots[i].State == DeckSlotState.Saved)
                    highestSaved = i;

            // Tous les slots sauvegardés + 1 slot vide suivant, minimum 1
            int visibleCount = Mathf.Clamp(highestSaved + 2, 1, deckSlots.Length);

            for (int i = 0; i < deckSlots.Length; i++)
                if (deckSlots[i] != null)
                    deckSlots[i].gameObject.SetActive(i < visibleCount);
        }

        // ── Clic sur un deck slot ─────────────────────────────────────

        public void OnSlotClicked(DeckCardSlot slot)
        {
            if (slot == null) return;

            // Reclique même slot → fermer
            if (slot == _selectedSlot)
            {
                CloseEditZone(save: false);
                return;
            }

            if (_isEditing) CloseEditZoneImmediate();
            OpenEditZone(slot);
        }

        private void OpenEditZone(DeckCardSlot slot)
        {
            _selectedSlot = slot;
            _isEditing    = true;

            bool wasSaved = slot.State == DeckSlotState.Saved;
            _selectedSlotWasSaved = wasSaved;
            slot.StartEditing();

            _editCards = new CardData[5];
            if (wasSaved)
                for (int i = 0; i < Mathf.Min(slot.CardNumbers.Count, 5); i++)
                    _editCards[i] = GetCard(slot.CardNumbers[i]);

            if (deckNameInput != null)
                deckNameInput.text = wasSaved ? slot.DeckName : "";

            if (btnDelete != null) btnDelete.interactable = wasSaved;

            RefreshCardSelectSlots();
            UpdateSaveButton();
            RefreshCollectionHighlights();

            if (editZone != null)
            {
                PositionEditZoneBelow(slot);
                editZone.gameObject.SetActive(true);
                editZone.localScale = new Vector3(1f, 0f, 1f);
                if (_editZoneCG != null) _editZoneCG.alpha = 0f;

                var seq = DOTween.Sequence().SetUpdate(true);
                seq.Join(editZone.DOScaleY(1f, 0.22f).SetEase(Ease.OutBack));
                seq.Join(_editZoneCG != null
                    ? _editZoneCG.DOFade(1f, 0.18f)
                    : DOTween.To(() => 0f, _ => { }, 1f, 0.18f));
                if (deckNameInput != null)
                    seq.OnComplete(() => { deckNameInput.Select(); deckNameInput.ActivateInputField(); });
            }
            else if (deckNameInput != null)
            {
                deckNameInput.Select();
                deckNameInput.ActivateInputField();
            }

            ShowFeedback(wasSaved
                ? $"Édition · {slot.DeckName}"
                : "Nouveau deck · cliquez les cartes pour les ajouter", true);
        }

        private void PositionEditZoneBelow(DeckCardSlot slot)
        {
            if (editZone == null) return;
            var slotRT   = slot.GetComponent<RectTransform>();
            var parentRT = editZone.parent as RectTransform;
            if (slotRT == null || parentRT == null) return;

            editZone.anchorMin = new Vector2(0f, 1f);
            editZone.anchorMax = new Vector2(1f, 1f);
            editZone.pivot     = new Vector2(0.5f, 1f);

            Vector3[] corners = new Vector3[4];
            slotRT.GetWorldCorners(corners);
            Vector3 slotBottomWorld = (corners[0] + corners[3]) * 0.5f;

            Vector2 localPos   = parentRT.InverseTransformPoint(slotBottomWorld);
            float   parentTopY = parentRT.rect.yMax;
            float   targetY    = localPos.y - parentTopY;

            float editH    = editZone.rect.height > 10f ? editZone.rect.height : 228f;
            float clampedY = Mathf.Clamp(targetY, -(parentRT.rect.height - editH), 0f);

            editZone.anchoredPosition = new Vector2(0f, clampedY);
        }

        public void CloseEditZone(bool save)
        {
            if (!_isEditing) return;
            if (save) { SaveDeck(); return; }

            if (editZone != null)
            {
                var seq = DOTween.Sequence().SetUpdate(true);
                seq.Join(editZone.DOScaleY(0f, 0.15f).SetEase(Ease.InBack));
                if (_editZoneCG != null) seq.Join(_editZoneCG.DOFade(0f, 0.12f));
                seq.OnComplete(() => editZone.gameObject.SetActive(false));
            }

            RestoreSlotState();
            ClearCollectionHighlights();
            _isEditing   = false;
            _selectedSlot = null;
        }

        private void CloseEditZoneImmediate()
        {
            if (editZone != null)
            {
                editZone.DOKill();
                editZone.localScale = Vector3.one;
                if (_editZoneCG != null) _editZoneCG.alpha = 1f;
                editZone.gameObject.SetActive(false);
            }
            RestoreSlotState();
            ClearCollectionHighlights();
            _isEditing    = false;
            _selectedSlot = null;
        }

        private void RestoreSlotState()
        {
            if (_selectedSlot == null) return;
            var saved = DeckSaveSystem.Instance?.GetDeckBySlot(_selectedSlot.slotIndex);
            if (saved != null && saved.cardNumbers?.Count > 0)
                _selectedSlot.LoadFromSave(saved.deckName, saved.cardNumbers, saved.dominantElementIndex);
            else
                _selectedSlot.SetEmpty();
        }

        // ── Assignation depuis CardsScrollView (gauche→droite) ────────

        /// <summary>Appelé par CollectionCardEntry quand IsEditing == true.</summary>
        public void AssignCardToNextSlot(int cardNumber)
        {
            if (!_isEditing) return;
            var card = GetCard(cardNumber);
            if (card == null) return;

            // Guard carte non possédée
            if (Astraleum.PlayerCollection.Instance != null && !Astraleum.PlayerCollection.Instance.OwnsCard(cardNumber))
            {
                ShowFeedback("Carte non débloquée.", false);
                return;
            }

            // Guard doublon
            for (int i = 0; i < 5; i++)
                if (_editCards[i]?.cardNumber == cardNumber)
                {
                    ShowFeedback("Cette carte est déjà dans le deck.", false);
                    return;
                }

            // Guard rareté
            if (!CheckRarityAllowed(card)) return;

            // Slot libre suivant (gauche → droite)
            int target = -1;
            for (int i = 0; i < 5; i++)
                if (_editCards[i] == null) { target = i; break; }

            if (target < 0) { ShowFeedback("Deck plein (5/5).", false); return; }

            _editCards[target] = card;
            cardSelectSlots[target]?.SetCard(card);

            UpdateDeckSlotDisplay();
            UpdateSaveButton();
            RefreshCollectionHighlights();
            ShowFeedback($"{card.cardName}  →  Slot {target + 1}", true);
        }

        /// <summary>Appelé par CardSelectSlot (clic sur une carte déjà assignée) pour la retirer.</summary>
        public void RemoveCardAtSlot(int slotPosition)
        {
            if (!_isEditing || slotPosition < 0 || slotPosition >= 5) return;
            _editCards[slotPosition] = null;
            cardSelectSlots[slotPosition]?.SetEmpty();
            UpdateDeckSlotDisplay();
            UpdateSaveButton();
            RefreshCollectionHighlights();
        }

        // ── Sauvegarde ────────────────────────────────────────────────

        private void SaveDeck()
        {
            if (_selectedSlot == null) { ShowFeedback("Erreur : aucun slot sélectionné.", false); return; }
            if (!CanSave())            { ShowFeedback("Remplissez les 5 slots et entrez un nom.", false); return; }

            string name = deckNameInput.text.Trim();
            var cardNumbers = new List<int>();
            for (int i = 0; i < 5; i++) cardNumbers.Add(_editCards[i].cardNumber);

            int elementIndex = GetDominantElementIndex(cardNumbers);
            bool ok = DeckSaveSystem.Instance != null
                   && DeckSaveSystem.Instance.SaveDeck(name, cardNumbers, _selectedSlot.slotIndex, elementIndex);

            if (!ok) { ShowFeedback("Impossible de sauvegarder.", false); return; }

            _selectedSlot.SaveDeck(name, cardNumbers, elementIndex);
            UpdateSlotVisibility();

            if (DeckManager.Instance != null)
            {
                DeckManager.Instance.ClearDeck();
                foreach (var num in cardNumbers) DeckManager.Instance.TryAddCard(num);
            }

            if (editZone != null)
            {
                var seq = DOTween.Sequence().SetUpdate(true);
                seq.Join(editZone.DOScaleY(0f, 0.15f).SetEase(Ease.InBack));
                if (_editZoneCG != null) seq.Join(_editZoneCG.DOFade(0f, 0.12f));
                seq.OnComplete(() => editZone.gameObject.SetActive(false));
            }

            ShowFeedback($"Deck « {name} » sauvegardé !", true);
            _isEditing    = false;
            _selectedSlot = null;
        }

        public void DeleteSelectedDeck()
        {
            if (_selectedSlot == null || !_selectedSlotWasSaved) return;
            string name = _selectedSlot.DeckName;
            DeckSaveSystem.Instance?.DeleteDeck(name);
            _selectedSlot.SetEmpty();
            UpdateSlotVisibility();
            CloseEditZoneImmediate();
            ShowFeedback($"Deck « {name} » supprimé.", true);
        }

        public void OnBtnSave() => CloseEditZone(save: true);

        // ── Helpers ───────────────────────────────────────────────────

        private bool CanSave()
        {
            for (int i = 0; i < 5; i++) if (_editCards[i] == null) return false;
            return !string.IsNullOrWhiteSpace(deckNameInput?.text);
        }

        private void UpdateSaveButton()
        {
            if (btnSave != null) btnSave.interactable = CanSave();
        }

        private void RefreshCardSelectSlots()
        {
            for (int i = 0; i < 5; i++)
            {
                if (cardSelectSlots[i] == null) continue;
                if (_editCards[i] != null) cardSelectSlots[i].SetCard(_editCards[i]);
                else                       cardSelectSlots[i].SetEmpty();
                cardSelectSlots[i].SetSelected(false);
            }
        }

        private void UpdateDeckSlotDisplay()
        {
            if (_selectedSlot == null) return;
            int count = _editCards.Count(c => c != null);
            _selectedSlot.UpdateEditingDisplay(deckNameInput?.text ?? "", count);
        }

        private bool CheckRarityAllowed(CardData card)
        {
            if (card.rarity == CardRarity.Supreme)
            {
                int n = _editCards.Count(c => c?.rarity == CardRarity.Supreme);
                if (n >= MAX_SUPREME) { ShowFeedback("Maximum 1 carte Suprême par deck.", false); return false; }
            }
            if (card.rarity == CardRarity.Legendaire)
            {
                int n = _editCards.Count(c => c?.rarity == CardRarity.Legendaire);
                if (n >= MAX_LEGENDAIRE) { ShowFeedback("Maximum 1 carte Légendaire par deck.", false); return false; }
            }
            return true;
        }

        private int GetDominantElementIndex(List<int> cardNumbers)
        {
            var counts = new Dictionary<Element, int>();
            foreach (var num in cardNumbers)
            {
                var c = GetCard(num);
                if (c == null) continue;
                counts[c.element] = counts.TryGetValue(c.element, out int v) ? v + 1 : 1;
            }
            if (counts.Count == 0) return -1;
            return (int)counts.OrderByDescending(kv => kv.Value).First().Key;
        }

        private void ShowFeedback(string msg, bool success)
        {
            if (feedbackText == null) return;
            feedbackText.text  = msg;
            feedbackText.color = success
                ? new Color(0.4f, 0.9f, 0.4f)
                : new Color(1f,   0.4f, 0.4f);
        }

        private void RefreshCollectionHighlights()
        {
            var inDeck = new System.Collections.Generic.HashSet<int>();
            foreach (var c in _editCards)
                if (c != null) inDeck.Add(c.cardNumber);

            foreach (var entry in FindObjectsByType<CollectionCardEntry>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                entry.SetInDeck(inDeck.Contains(entry.CardData?.cardNumber ?? -1));
        }

        private void ClearCollectionHighlights()
        {
            foreach (var entry in FindObjectsByType<CollectionCardEntry>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                entry.SetInDeck(false);
        }
    }
}
