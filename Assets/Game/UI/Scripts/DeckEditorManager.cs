using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum.UI
{
    public class DeckEditorManager : MonoBehaviour
    {
        public static DeckEditorManager Instance;

        // ── DeckZone ──────────────────────────────────────────────────
        [Header("DeckZone — 8 slots")]
        public DeckCardSlot[] deckSlots = new DeckCardSlot[8];
        public Button         btnCreate;
        public Button         btnDelete;

        // ── Panel_CardSelect ──────────────────────────────────────────
        [Header("Panel_CardSelect")]
        public GameObject       panelCardSelect;
        public CardSelectSlot[] cardSelectSlots = new CardSelectSlot[5];
        public TMP_InputField   deckNameInput;
        public Button           btnSave;
        public TMP_Text         feedbackTextCardSelect;

        // ── AllCardsZone ──────────────────────────────────────────────
        [Header("AllCardsZone")]
        public RectTransform allCardsGrid;
        public GameObject    allCardPrefab;
        public TMP_Dropdown  filterRarity;
        public TMP_Dropdown  filterElement;

        // ── Feedback DeckZone ─────────────────────────────────────────
        [Header("Feedback DeckZone")]
        public TMP_Text feedbackText;

        // ── État interne ──────────────────────────────────────────────
        private Dictionary<int, Astraleum.CardData> cardLookup = new();
        private List<GameObject>                    allCardObjs = new();
        private int currentRarityFilter  = 0;
        private int currentElementFilter = 0;

        private DeckCardSlot         _selectedDeckSlot     = null;
        private Astraleum.CardData[] _editCards            = new Astraleum.CardData[5];
        private int                  _activeCardSelectSlot = -1;

        private const int MAX_DECK_SIZE  = 5;
        private const int MAX_SUPREME    = 1;
        private const int MAX_LEGENDAIRE = 1;

        // ── Init ──────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            if (panelCardSelect != null) panelCardSelect.SetActive(false);
            BuildCardLookup();
            if (btnCreate != null) btnCreate.interactable = false;
            if (btnDelete != null) btnDelete.interactable = false;
            if (btnSave   != null) btnSave.interactable   = false;
        }

        private void OnEnable()
        {
            Astraleum.DeckSaveSystem.OnDecksChanged += LoadSavedDecksIntoSlots;
            PopulateFilterDropdowns();
            LoadSavedDecksIntoSlots();
        }

        private void OnDisable()
        {
            Astraleum.DeckSaveSystem.OnDecksChanged -= LoadSavedDecksIntoSlots;
            if (panelCardSelect != null && panelCardSelect.activeSelf)
                CloseCardSelect(false);
            DeselectDeckSlot();
        }

        private void BuildCardLookup()
        {
            cardLookup.Clear();
            var cards = Astraleum.CardDatabase.LoadVisibleCards();
            foreach (var c in cards)
                if (!cardLookup.ContainsKey(c.cardNumber))
                    cardLookup[c.cardNumber] = c;
        }

        private Astraleum.CardData GetCard(int num) =>
            cardLookup.TryGetValue(num, out var c) ? c : null;

        // ── Chargement des decks sauvegardés ──────────────────────────

        private void LoadSavedDecksIntoSlots()
        {
            if (Astraleum.DeckSaveSystem.Instance == null) return;
            for (int i = 0; i < deckSlots.Length; i++)
            {
                if (deckSlots[i] == null) continue;
                if (deckSlots[i] == _selectedDeckSlot) continue;

                var saved = Astraleum.DeckSaveSystem.Instance.GetDeckBySlot(i);
                if (saved != null && saved.cardNumbers != null && saved.cardNumbers.Count > 0)
                    deckSlots[i].LoadFromSave(saved.deckName, saved.cardNumbers, saved.dominantElementIndex);
                else
                    deckSlots[i].SetEmpty();
            }
        }

        // ── DeckZone — sélection slot ─────────────────────────────────

        public void OnSlotClicked(DeckCardSlot slot)
        {
            if (slot == null) return;

            if (slot == _selectedDeckSlot)
            {
                DeselectDeckSlot();
                return;
            }

            DeselectDeckSlot();

            _selectedDeckSlot = slot;
            slot.StartEditing();

            if (btnCreate != null) btnCreate.interactable = true;
            if (btnDelete != null) btnDelete.interactable = slot.State == DeckSlotState.Saved;

            ShowFeedback(feedbackText,
                slot.State == DeckSlotState.Empty
                    ? "Slot sélectionné. Cliquez Créer pour composer un deck."
                    : $"Deck « {slot.DeckName} » sélectionné.",
                true);
        }

        private void DeselectDeckSlot()
        {
            if (_selectedDeckSlot == null) return;

            if (_selectedDeckSlot.State == DeckSlotState.Empty || _selectedDeckSlot.State == DeckSlotState.Editing)
            {
                var saved = Astraleum.DeckSaveSystem.Instance?.GetDeckBySlot(_selectedDeckSlot.slotIndex);
                if (saved != null && saved.cardNumbers != null && saved.cardNumbers.Count > 0)
                    _selectedDeckSlot.LoadFromSave(saved.deckName, saved.cardNumbers, saved.dominantElementIndex);
                else
                    _selectedDeckSlot.SetEmpty();
            }

            _selectedDeckSlot = null;
            if (btnCreate != null) btnCreate.interactable = false;
            if (btnDelete != null) btnDelete.interactable = false;
        }

        // ── Panel_CardSelect — ouverture / fermeture ──────────────────

        public void OpenCardSelect()
        {
            if (_selectedDeckSlot == null)
            {
                ShowFeedback(feedbackText, "Sélectionnez un slot avant de créer un deck.", false);
                return;
            }
            if (panelCardSelect != null && panelCardSelect.activeSelf) return;

            _editCards = new Astraleum.CardData[5];

            if (_selectedDeckSlot.State == DeckSlotState.Saved)
            {
                var existing = _selectedDeckSlot.CardNumbers;
                for (int i = 0; i < Mathf.Min(existing.Count, 5); i++)
                    _editCards[i] = GetCard(existing[i]);
                if (deckNameInput != null) deckNameInput.text = _selectedDeckSlot.DeckName;
            }
            else
            {
                if (deckNameInput != null) deckNameInput.text = "";
            }

            _activeCardSelectSlot = FindFirstEmptySlot();

            if (panelCardSelect != null) panelCardSelect.SetActive(true);
            PopulateAllCards();
            RefreshCardSelectSlots();
            RefreshAllCardsHighlight();
            UpdateSaveButtonState();
        }

        public void OnBtnBackCardSelect() => CloseCardSelect(false);

        private void CloseCardSelect(bool save)
        {
            if (panelCardSelect != null) panelCardSelect.SetActive(false);
            if (!save)
                ShowFeedback(feedbackText, "Édition annulée.", true);
            ResetEditState();
        }

        private void ResetEditState()
        {
            _editCards = new Astraleum.CardData[5];
            _activeCardSelectSlot = -1;
            RefreshCardSelectSlots();
            RefreshAllCardsHighlight();
        }

        // ── SlotsZone ─────────────────────────────────────────────────

        public void OnCardSelectSlotClicked(int slotPosition)
        {
            if (slotPosition < 0 || slotPosition >= 5) return;

            if (_activeCardSelectSlot >= 0 && _activeCardSelectSlot < 5)
                cardSelectSlots[_activeCardSelectSlot]?.SetSelected(false);

            _activeCardSelectSlot = slotPosition;
            cardSelectSlots[_activeCardSelectSlot]?.SetSelected(true);

            ShowFeedback(feedbackTextCardSelect, $"Slot {slotPosition + 1} actif.", true);
        }

        // ── AllCardsZone — assignation carte ──────────────────────────

        public void AssignCardToActiveSelectSlot(int cardNumber)
        {
            if (panelCardSelect == null || !panelCardSelect.activeSelf) return;
            if (_activeCardSelectSlot < 0) return;

            var card = GetCard(cardNumber);
            if (card == null) return;

            // Guard carte non possédée
            if (Astraleum.PlayerCollection.Instance != null && !Astraleum.PlayerCollection.Instance.OwnsCard(cardNumber))
            {
                ShowFeedback(feedbackTextCardSelect, "Carte non débloquée.", false);
                return;
            }

            // Guard doublon
            for (int i = 0; i < 5; i++)
            {
                if (i == _activeCardSelectSlot) continue;
                if (_editCards[i]?.cardNumber == cardNumber)
                {
                    ShowFeedback(feedbackTextCardSelect, "Cette carte est déjà dans le deck.", false);
                    return;
                }
            }

            // Guard rareté (exclure le slot courant du comptage)
            if (card.rarity == Astraleum.CardRarity.Supreme)
            {
                int count = 0;
                for (int i = 0; i < 5; i++)
                {
                    if (i == _activeCardSelectSlot) continue;
                    if (_editCards[i]?.rarity == Astraleum.CardRarity.Supreme) count++;
                }
                if (count >= MAX_SUPREME)
                {
                    ShowFeedback(feedbackTextCardSelect, "Maximum 1 carte Suprême par deck.", false);
                    return;
                }
            }

            if (card.rarity == Astraleum.CardRarity.Legendaire)
            {
                int count = 0;
                for (int i = 0; i < 5; i++)
                {
                    if (i == _activeCardSelectSlot) continue;
                    if (_editCards[i]?.rarity == Astraleum.CardRarity.Legendaire) count++;
                }
                if (count >= MAX_LEGENDAIRE)
                {
                    ShowFeedback(feedbackTextCardSelect, "Maximum 1 carte Légendaire par deck.", false);
                    return;
                }
            }

            _editCards[_activeCardSelectSlot] = card;
            cardSelectSlots[_activeCardSelectSlot]?.SetCard(card);

            ShowFeedback(feedbackTextCardSelect,
                $"{card.cardName} assignée au Slot {_activeCardSelectSlot + 1}.", true);

            RefreshAllCardsHighlight();
            UpdateSaveButtonState();
        }

        // ── Sauvegarde ────────────────────────────────────────────────

        public void SaveCurrentDeck()
        {
            if (_selectedDeckSlot == null)
            {
                ShowFeedback(feedbackTextCardSelect, "Erreur : aucun slot de deck sélectionné.", false);
                return;
            }

            if (!CanSave())
            {
                ShowFeedback(feedbackTextCardSelect, "Remplissez les 5 slots et entrez un nom.", false);
                return;
            }

            string name = deckNameInput.text.Trim();
            var cardNumbers = new List<int>();
            for (int i = 0; i < 5; i++)
                cardNumbers.Add(_editCards[i].cardNumber);

            int slotIndex    = _selectedDeckSlot.slotIndex;
            int elementIndex = GetDominantElementIndex(cardNumbers);

            bool saved = Astraleum.DeckSaveSystem.Instance != null
                      && Astraleum.DeckSaveSystem.Instance.SaveDeck(name, cardNumbers, slotIndex, elementIndex);

            if (!saved)
            {
                ShowFeedback(feedbackTextCardSelect, "Impossible de sauvegarder.", false);
                return;
            }

            _selectedDeckSlot.SaveDeck(name, cardNumbers, elementIndex);

            if (Astraleum.DeckManager.Instance != null)
            {
                Astraleum.DeckManager.Instance.ClearDeck();
                foreach (var num in cardNumbers)
                    Astraleum.DeckManager.Instance.TryAddCard(num);
            }

            ShowFeedback(feedbackText, $"Deck « {name} » sauvegardé !", true);

            CloseCardSelect(true);
            _selectedDeckSlot = null;
            if (btnCreate != null) btnCreate.interactable = false;
            if (btnDelete != null) btnDelete.interactable = false;
        }

        public void DeleteSelectedDeck()
        {
            if (_selectedDeckSlot == null)
            {
                ShowFeedback(feedbackText, "Sélectionnez un slot à supprimer.", false);
                return;
            }

            string name = _selectedDeckSlot.DeckName;
            if (!string.IsNullOrEmpty(name) && Astraleum.DeckSaveSystem.Instance != null)
                Astraleum.DeckSaveSystem.Instance.DeleteDeck(name);

            _selectedDeckSlot.SetEmpty();
            _selectedDeckSlot = null;
            if (btnCreate != null) btnCreate.interactable = false;
            if (btnDelete != null) btnDelete.interactable = false;

            ShowFeedback(feedbackText,
                string.IsNullOrEmpty(name) ? "Slot vidé." : $"Deck « {name} » supprimé.", true);
        }

        private bool CanSave()
        {
            if (_editCards == null) return false;
            for (int i = 0; i < 5; i++)
                if (_editCards[i] == null) return false;
            return !string.IsNullOrWhiteSpace(deckNameInput?.text);
        }

        private void UpdateSaveButtonState()
        {
            if (btnSave != null) btnSave.interactable = CanSave();
        }

        public void OnDeckNameInputChanged(string _) => UpdateSaveButtonState();

        // ── Affichage ─────────────────────────────────────────────────

        private void RefreshCardSelectSlots()
        {
            for (int i = 0; i < 5; i++)
            {
                if (cardSelectSlots[i] == null) continue;
                if (_editCards[i] != null)
                    cardSelectSlots[i].SetCard(_editCards[i]);
                else
                    cardSelectSlots[i].SetEmpty();
                cardSelectSlots[i].SetSelected(i == _activeCardSelectSlot);
            }
        }

        private void RefreshAllCardsHighlight()
        {
            foreach (var go in allCardObjs)
            {
                var entry = go.GetComponent<DeckEditorCardEntry>();
                if (entry?.CardData == null) continue;

                int assignedSlot = -1;
                for (int i = 0; i < 5; i++)
                {
                    if (_editCards[i]?.cardNumber == entry.CardData.cardNumber)
                    {
                        assignedSlot = i;
                        break;
                    }
                }

                if (assignedSlot >= 0)
                    entry.SetInDeck(true, assignedSlot + 1);
                else
                    entry.SetInDeck(false);
            }
        }

        // ── AllCardsZone ──────────────────────────────────────────────

        private void PopulateAllCards()
        {
            foreach (var go in allCardObjs)
                if (go != null) Destroy(go);
            allCardObjs.Clear();

            if (allCardsGrid == null || allCardPrefab == null) return;

            var sorted = cardLookup.Values
                .Where(c => PassesFilter(c))
                .OrderBy(c => c.cardNumber)
                .ToList();

            foreach (var card in sorted)
            {
                bool owned = Astraleum.PlayerCollection.Instance == null
                          || Astraleum.PlayerCollection.Instance.OwnsCard(card.cardNumber);

                var go    = Instantiate(allCardPrefab, allCardsGrid);
                var entry = go.GetComponent<DeckEditorCardEntry>();
                entry?.Setup(card, owned);
                allCardObjs.Add(go);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(allCardsGrid);
            RefreshAllCardsHighlight();
        }

        // ── Filtres ───────────────────────────────────────────────────

        private bool PassesFilter(Astraleum.CardData card)
        {
            if (currentRarityFilter != 0)
            {
                var target = (Astraleum.CardRarity)(currentRarityFilter - 1);
                if (card.rarity != target) return false;
            }
            if (currentElementFilter != 0)
            {
                var target = (Astraleum.Element)(currentElementFilter - 1);
                if (card.element != target) return false;
            }
            return true;
        }

        private void PopulateFilterDropdowns()
        {
            if (filterRarity != null)
            {
                filterRarity.ClearOptions();
                var opts = new List<TMP_Dropdown.OptionData>
                    { new TMP_Dropdown.OptionData("Toutes raretés") };
                foreach (Astraleum.CardRarity r in System.Enum.GetValues(typeof(Astraleum.CardRarity)))
                    opts.Add(new TMP_Dropdown.OptionData(r.ToString()));
                filterRarity.AddOptions(opts);
                filterRarity.value = 0;
                filterRarity.onValueChanged.RemoveAllListeners();
                filterRarity.onValueChanged.AddListener(v =>
                {
                    currentRarityFilter = v;
                    PopulateAllCards();
                });
            }

            if (filterElement != null)
            {
                filterElement.ClearOptions();
                var opts = new List<TMP_Dropdown.OptionData>
                    { new TMP_Dropdown.OptionData("Tous éléments") };
                foreach (Astraleum.Element e in System.Enum.GetValues(typeof(Astraleum.Element)))
                    opts.Add(new TMP_Dropdown.OptionData(e.ToString()));
                filterElement.AddOptions(opts);
                filterElement.value = 0;
                filterElement.onValueChanged.RemoveAllListeners();
                filterElement.onValueChanged.AddListener(v =>
                {
                    currentElementFilter = v;
                    PopulateAllCards();
                });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private int FindFirstEmptySlot()
        {
            for (int i = 0; i < 5; i++)
                if (_editCards[i] == null) return i;
            return 0;
        }

        private int GetDominantElementIndex(List<int> cardNumbers)
        {
            var counts = new Dictionary<Astraleum.Element, int>();
            foreach (var num in cardNumbers)
            {
                var card = GetCard(num);
                if (card == null) continue;
                counts[card.element] = counts.TryGetValue(card.element, out int v) ? v + 1 : 1;
            }
            if (counts.Count == 0) return -1;
            return (int)counts.OrderByDescending(kv => kv.Value).First().Key;
        }

        private void ShowFeedback(TMP_Text target, string message, bool success)
        {
            if (target == null) return;
            target.text  = message;
            target.color = success
                ? new Color(0.4f, 0.9f, 0.4f)
                : new Color(1f, 0.4f, 0.4f);
        }
    }
}
