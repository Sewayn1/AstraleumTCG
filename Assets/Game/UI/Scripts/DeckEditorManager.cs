using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum.UI
{
    /// <summary>
    /// Gère Panel_DeckEditor.
    /// Structure attendue :
    ///   AllCardsZone/AllCardsScroll/Viewport/Content  → allCardsGrid
    ///   DeckZone/Deck_Slot_1 … Deck_Slot_8           → deckSlots[0..7]
    ///   DeckZone/DeckNameInput                        → deckNameInput
    ///   Btn_Save                                      → SaveCurrentDeck()
    ///   Btn_Reset                                     → ResetDeck()
    /// </summary>
    public class DeckEditorManager : MonoBehaviour
    {
        public static DeckEditorManager Instance;

        // ── AllCardsZone ──────────────────────────────────────────────
        [Header("AllCardsZone")]
        [Tooltip("Content du ScrollView (AllCardsScroll/Viewport/Content).")]
        public RectTransform allCardsGrid;
        [Tooltip("Prefab carte pour AllCardsZone (CollectionCardEntry).")]
        public GameObject allCardPrefab;

        [Header("Filtres AllCardsZone — optionnels")]
        public TMP_Dropdown filterRarity;
        public TMP_Dropdown filterElement;

        // ── DeckZone — 8 slots fixes ──────────────────────────────────
        [Header("DeckZone — 8 slots fixes")]
        [Tooltip("Assigner Deck_Slot_1 à Deck_Slot_8 dans l'ordre.")]
        public DeckCardSlot[] deckSlots = new DeckCardSlot[8];

        // ── Infos / Feedback ──────────────────────────────────────────
        [Header("Infos")]
        public TMP_Text deckCountText;
        [Tooltip("TMP_Text pour les messages d'erreur / confirmation.")]
        public TMP_Text feedbackText;

        // ── Sauvegarde ────────────────────────────────────────────────
        [Header("Sauvegarde")]
        public TMP_InputField deckNameInput;

        // ── État interne ──────────────────────────────────────────────
        private Dictionary<int, Astraleum.CardData> cardLookup = new();
        private List<GameObject>                    allCardObjs = new();
        private List<int>                           currentDeck = new();
        private DeckCardSlot                        activeSlot  = null;

        // Snapshot pris au début de chaque édition
        private bool         _snapshotWasSaved = false;  // le slot avait un deck avant édition
        private string       _snapshotName     = "";
        private List<int>    _snapshotCards    = new();
        private int          _snapshotElement  = -1;

        private int currentRarityFilter  = 0;
        private int currentElementFilter = 0;

        private const int MAX_DECK_SIZE  = 5;
        private const int MAX_SUPREME    = 1;
        private const int MAX_LEGENDAIRE = 1;

        // ── Init ──────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            BuildCardLookup();
        }

        private void OnEnable()
        {
            Astraleum.DeckSaveSystem.OnDecksChanged += LoadSavedDecksIntoSlots;
            PopulateFilterDropdowns();
            PopulateAllCards();
            LoadSavedDecksIntoSlots();
            RefreshDeckCountDisplay();
        }

        private void OnDisable()
        {
            Astraleum.DeckSaveSystem.OnDecksChanged -= LoadSavedDecksIntoSlots;
            CancelEditing(); // annule toute édition en cours à la fermeture du panel
        }

        private void OnApplicationQuit()
        {
            CancelEditing();
        }

        private void BuildCardLookup()
        {
            cardLookup.Clear();
            var cards = Astraleum.CardDatabase.Instance != null
                ? Astraleum.CardDatabase.Instance.GetAllCards()
                : Resources.LoadAll<Astraleum.CardData>("Cards").ToList();

            foreach (var c in cards)
                if (!cardLookup.ContainsKey(c.cardNumber))
                    cardLookup[c.cardNumber] = c;
        }

        private Astraleum.CardData GetCard(int num) =>
            cardLookup.TryGetValue(num, out var c) ? c : null;

        // ── Chargement des decks sauvegardés dans les slots ───────────

        private void LoadSavedDecksIntoSlots()
        {
            if (Astraleum.DeckSaveSystem.Instance == null) return;

            for (int i = 0; i < deckSlots.Length; i++)
            {
                if (deckSlots[i] == null) continue;
                if (deckSlots[i] == activeSlot) continue;

                var saved = Astraleum.DeckSaveSystem.Instance.GetDeckBySlot(i);
                if (saved != null && saved.cardNumbers != null && saved.cardNumbers.Count > 0)
                    deckSlots[i].LoadFromSave(saved.deckName, saved.cardNumbers, saved.dominantElementIndex);
                else
                    deckSlots[i].SetEmpty();
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

        private void RefreshAllCardsHighlight()
        {
            foreach (var go in allCardObjs)
            {
                var entry = go.GetComponent<DeckEditorCardEntry>();
                if (entry?.CardData == null) continue;

                int idx = currentDeck.IndexOf(entry.CardData.cardNumber);
                if (idx >= 0)
                    entry.SetInDeck(true, idx + 1);
                else
                    entry.SetInDeck(false);
            }
        }

        // ── Gestion des slots ─────────────────────────────────────────

        /// <summary>Appelé par DeckCardSlot.OnPointerClick.</summary>
        public void OnSlotClicked(DeckCardSlot slot)
        {
            if (slot == null) return;

            // Clic sur le slot déjà en cours d'édition → ne rien faire
            if (slot == activeSlot) return;

            // Si un autre slot est en cours d'édition, bloquer
            if (activeSlot != null)
            {
                ShowFeedback("Sauvegardez ou réinitialisez le deck actuel avant d'en éditer un autre.", false);
                return;
            }

            // Snapshot de l'état avant édition
            _snapshotWasSaved = slot.State == DeckSlotState.Saved;
            _snapshotName     = slot.DeckName;
            _snapshotCards    = new List<int>(slot.CardNumbers);
            var snapSaved     = Astraleum.DeckSaveSystem.Instance?.GetDeckBySlot(slot.slotIndex);
            _snapshotElement  = snapSaved?.dominantElementIndex ?? -1;

            // Passer en mode édition
            activeSlot = slot;
            slot.StartEditing();

            // Charger les cartes existantes du slot dans currentDeck
            currentDeck = new List<int>(slot.CardNumbers);

            // Pré-remplir le champ de nom si le slot est sauvegardé
            if (deckNameInput != null)
                deckNameInput.text = slot.DeckName;

            RefreshDeckCountDisplay();
            RefreshAllCardsHighlight();
            slot.UpdateEditingDisplay(slot.DeckName, currentDeck.Count);
            ShowFeedback(slot.State == DeckSlotState.Empty
                ? "Nouveau deck — ajoutez des cartes."
                : $"Édition de « {slot.DeckName} ».", true);
        }

        // ── Toggle carte ──────────────────────────────────────────────

        public void ToggleCardInDeck(int cardNumber)
        {
            if (activeSlot == null)
            {
                ShowFeedback("Sélectionnez un slot pour commencer.", false);
                return;
            }

            if (currentDeck.Contains(cardNumber))
            {
                // Retirer du deck
                currentDeck.Remove(cardNumber);
                var removedCard = GetCard(cardNumber);
                RefreshEditingDisplay();
                ShowFeedback($"{removedCard?.cardName ?? "Carte"} retirée.", true);
            }
            else
            {
                // Ajouter au deck
                if (currentDeck.Count >= MAX_DECK_SIZE)
                {
                    ShowFeedback("Deck complet — 5 cartes maximum.", false);
                    return;
                }

                var card = GetCard(cardNumber);
                if (card == null) return;

                if (card.rarity == Astraleum.CardRarity.Supreme &&
                    currentDeck.Count(n => GetCard(n)?.rarity == Astraleum.CardRarity.Supreme) >= MAX_SUPREME)
                {
                    ShowFeedback("Maximum 1 carte Suprême par deck.", false);
                    return;
                }

                if (card.rarity == Astraleum.CardRarity.Legendaire &&
                    currentDeck.Count(n => GetCard(n)?.rarity == Astraleum.CardRarity.Legendaire) >= MAX_LEGENDAIRE)
                {
                    ShowFeedback("Maximum 1 carte Légendaire par deck.", false);
                    return;
                }

                currentDeck.Add(cardNumber);
                RefreshEditingDisplay();
                ShowFeedback($"{card.cardName} ajoutée.", true);
            }

            RefreshAllCardsHighlight();
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
            var dominant = counts.OrderByDescending(kv => kv.Value).First().Key;
            return (int)dominant;
        }

        private void RefreshEditingDisplay()
        {
            string name = deckNameInput != null ? deckNameInput.text.Trim() : activeSlot?.DeckName ?? "";
            activeSlot?.UpdateEditingDisplay(name, currentDeck.Count);
            RefreshDeckCountDisplay();
        }

        private void RefreshDeckCountDisplay()
        {
            if (deckCountText != null)
                deckCountText.text = $"{currentDeck.Count} / {MAX_DECK_SIZE}";
        }

        // ── Sauvegarde ────────────────────────────────────────────────

        /// <summary>
        /// Annule l'édition en cours sans sauvegarder.
        /// Deck existant → restauré à son état d'avant édition.
        /// Nouveau deck → slot vidé.
        /// Appelé automatiquement à la fermeture du panel et au quit.
        /// </summary>
        public void CancelEditing()
        {
            if (activeSlot == null) return;

            if (_snapshotWasSaved)
            {
                // Restaure le deck à son état précédent
                activeSlot.LoadFromSave(_snapshotName, _snapshotCards, _snapshotElement);
            }
            else
            {
                // Nouveau deck jamais sauvegardé → vide le slot
                activeSlot.SetEmpty();
            }

            activeSlot = null;
            currentDeck.Clear();
            if (deckNameInput != null) deckNameInput.text = "";
            RefreshDeckCountDisplay();
            RefreshAllCardsHighlight();
        }

        public void SaveCurrentDeck()
        {
            if (activeSlot == null)
            {
                ShowFeedback("Aucun slot en cours d'édition.", false);
                return;
            }

            string name = deckNameInput != null ? deckNameInput.text.Trim() : activeSlot.DeckName;
            if (string.IsNullOrEmpty(name))
            {
                ShowFeedback("Entrez un nom pour le deck.", false);
                return;
            }
            if (currentDeck.Count == 0)
            {
                ShowFeedback("Le deck est vide.", false);
                return;
            }

            int slotIndex    = activeSlot.slotIndex;
            int elementIndex = GetDominantElementIndex(currentDeck);
            bool saved = Astraleum.DeckSaveSystem.Instance != null
                       && Astraleum.DeckSaveSystem.Instance.SaveDeck(name, currentDeck, slotIndex, elementIndex);

            if (!saved)
            {
                ShowFeedback("Impossible de sauvegarder.", false);
                return;
            }

            activeSlot.SaveDeck(name, currentDeck, elementIndex);

            if (Astraleum.DeckManager.Instance != null)
            {
                Astraleum.DeckManager.Instance.ClearDeck();
                foreach (var num in currentDeck)
                    Astraleum.DeckManager.Instance.TryAddCard(num);
            }

            ShowFeedback($"Deck « {name} » sauvegardé !", true);

            activeSlot        = null;
            _snapshotWasSaved = false;
            currentDeck.Clear();
            if (deckNameInput != null) deckNameInput.text = "";
            RefreshDeckCountDisplay();
            RefreshAllCardsHighlight();
        }

        /// <summary>Supprime le deck du slot actif (bouton Supprimer).</summary>
        public void DeleteSelectedDeck()
        {
            if (activeSlot == null)
            {
                ShowFeedback("Sélectionnez un slot à supprimer.", false);
                return;
            }

            string name = activeSlot.DeckName;

            // Supprime de la sauvegarde si le slot était enregistré
            if (!string.IsNullOrEmpty(name) && Astraleum.DeckSaveSystem.Instance != null)
                Astraleum.DeckSaveSystem.Instance.DeleteDeck(name);

            activeSlot.SetEmpty();
            activeSlot        = null;
            _snapshotWasSaved = false;
            currentDeck.Clear();
            if (deckNameInput != null) deckNameInput.text = "";
            RefreshDeckCountDisplay();
            RefreshAllCardsHighlight();

            ShowFeedback(string.IsNullOrEmpty(name)
                ? "Édition annulée."
                : $"Deck « {name} » supprimé.", true);
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

        // ── Feedback ──────────────────────────────────────────────────

        private bool ShowFeedback(string message, bool success)
        {
            if (feedbackText != null)
            {
                feedbackText.text  = message;
                feedbackText.color = success
                    ? new Color(0.4f, 0.9f, 0.4f)
                    : new Color(1f, 0.4f, 0.4f);
            }
            return success;
        }
    }

}
