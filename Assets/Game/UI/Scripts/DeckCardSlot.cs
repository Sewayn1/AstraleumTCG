using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Astraleum.UI
{
    public enum DeckSlotState { Empty, Editing, Saved }

    /// <summary>
    /// Composant visuel d'un slot de deck.
    /// slotIndex (0-7) OBLIGATOIRE dans l'inspecteur.
    /// isSelectionMode : cocher sur les slots de Panel_DeckSelect.
    /// Toutes les données viennent de DeckSaveSystem — aucun PlayerPrefs local.
    /// </summary>
    public class DeckCardSlot : MonoBehaviour, IPointerClickHandler
    {
        [Header("Index — OBLIGATOIRE")]
        [Tooltip("0 à 7, identique entre Panel_DeckEditor et Panel_DeckSelect.")]
        public int slotIndex = -1;

        [Header("Mode")]
        [Tooltip("Cocher sur les slots de Panel_DeckSelect.")]
        public bool isSelectionMode = false;

        [Header("Références")]
        public TMP_Text slotNameText;
        public Image    slotBackground;

        [Header("Couleurs d'état")]
        public Color colorEmpty   = new Color(0.25f, 0.25f, 0.25f, 0.8f);
        public Color colorEditing = new Color(0.45f, 0.25f, 0.85f, 0.9f);

        // ── Données runtime ───────────────────────────────────────────
        public DeckSlotState State       { get; private set; } = DeckSlotState.Empty;
        public string        DeckName    { get; private set; } = "";
        public List<int>     CardNumbers { get; private set; } = new List<int>();

        // Couleur calculée une fois à la sauvegarde, stockée ici
        private int   _elementIndex = -1;
        private Color _savedColor;

        // ── Couleurs par élément ──────────────────────────────────────
        private static readonly Dictionary<Astraleum.Element, Color> ElementColors =
            new Dictionary<Astraleum.Element, Color>
            {
                { Astraleum.Element.Feu,      new Color(0.85f, 0.25f, 0.10f, 0.85f) },
                { Astraleum.Element.Eau,      new Color(0.10f, 0.45f, 0.85f, 0.85f) },
                { Astraleum.Element.Terre,    new Color(0.30f, 0.60f, 0.15f, 0.85f) },
                { Astraleum.Element.Air,      new Color(0.70f, 0.85f, 0.90f, 0.85f) },
                { Astraleum.Element.Lumiere,  new Color(0.95f, 0.85f, 0.25f, 0.85f) },
                { Astraleum.Element.Tenebres, new Color(0.30f, 0.10f, 0.45f, 0.85f) },
                { Astraleum.Element.Astral,   new Color(0.50f, 0.20f, 0.75f, 0.85f) },
            };

        // ── Init ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (slotNameText  == null) slotNameText  = GetComponentInChildren<TMP_Text>();
            if (slotBackground == null) slotBackground = GetComponent<Image>();
            _savedColor = colorEmpty;
            ApplyVisuals();
        }

        private void OnEnable()  => Astraleum.LocalizationManager.OnLanguageChanged += ApplyVisuals;
        private void OnDisable() => Astraleum.LocalizationManager.OnLanguageChanged -= ApplyVisuals;

        // ── API publique ──────────────────────────────────────────────

        /// <summary>Passe en mode édition (DeckEditor uniquement).</summary>
        public void StartEditing()
        {
            State = DeckSlotState.Editing;
            ApplyVisuals();
        }

        /// <summary>Met à jour le texte pendant l'édition.</summary>
        public void UpdateEditingDisplay(string deckName, int cardCount)
        {
            DeckName = deckName;
            if (slotNameText != null)
                slotNameText.text = string.IsNullOrEmpty(deckName)
                    ? $"Nouveau deck... ({cardCount}/5)"
                    : $"{deckName} ({cardCount}/5)";
        }

        /// <summary>Applique l'état Saved avec couleur d'élément.</summary>
        public void SaveDeck(string deckName, List<int> cardNumbers, int dominantElementIndex = -1)
        {
            DeckName      = deckName;
            CardNumbers   = new List<int>(cardNumbers);
            State         = DeckSlotState.Saved;
            _elementIndex = dominantElementIndex;
            _savedColor   = ResolveElementColor(dominantElementIndex);
            ApplyVisuals();
        }

        /// <summary>Charge les données depuis DeckSaveSystem (lecture seule).</summary>
        public void LoadFromSave(string deckName, List<int> cardNumbers, int dominantElementIndex = -1)
        {
            DeckName      = deckName;
            CardNumbers   = new List<int>(cardNumbers);
            State         = DeckSlotState.Saved;
            _elementIndex = dominantElementIndex;
            _savedColor   = ResolveElementColor(dominantElementIndex);
            ApplyVisuals();
        }

        /// <summary>Vide le slot.</summary>
        public void SetEmpty()
        {
            DeckName      = "";
            _elementIndex = -1;
            _savedColor   = colorEmpty;
            CardNumbers.Clear();
            State = DeckSlotState.Empty;
            ApplyVisuals();
        }

        /// <summary>Surbrillance pour sélection dans Panel_DeckSelect.</summary>
        public void SetSelectedForPlay(bool selected)
        {
            if (slotBackground == null) return;
            slotBackground.color = selected
                ? new Color(
                    Mathf.Min(_savedColor.r + 0.2f, 1f),
                    Mathf.Min(_savedColor.g + 0.2f, 1f),
                    Mathf.Min(_savedColor.b + 0.2f, 1f),
                    1f)
                : _savedColor;
        }

        // ── Clic ──────────────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isSelectionMode)
                DeckSelectPanel.Instance?.OnSlotClicked(this);
            else
                DeckEditorManager.Instance?.OnSlotClicked(this);
        }

        // ── Visuels ───────────────────────────────────────────────────

        private void ApplyVisuals()
        {
            switch (State)
            {
                case DeckSlotState.Empty:
                    if (slotNameText   != null) slotNameText.text    = Astraleum.LocalizationManager.Get("deck_slot_empty");
                    if (slotBackground != null) slotBackground.color = colorEmpty;
                    break;

                case DeckSlotState.Editing:
                    if (slotBackground != null) slotBackground.color = colorEditing;
                    break;

                case DeckSlotState.Saved:
                    if (slotNameText   != null) slotNameText.text    = $"{DeckName}  ({CardNumbers.Count}/5)";
                    if (slotBackground != null) slotBackground.color = _savedColor;
                    break;
            }
        }

        private Color ResolveElementColor(int elementIndex)
        {
            if (elementIndex < 0) return colorEmpty;
            var el = (Astraleum.Element)elementIndex;
            return ElementColors.TryGetValue(el, out var c) ? c : colorEmpty;
        }
    }
}
