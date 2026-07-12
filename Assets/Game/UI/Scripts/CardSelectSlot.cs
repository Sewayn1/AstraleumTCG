using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Astraleum.UI
{
    public class CardSelectSlot : MonoBehaviour, IPointerClickHandler
    {
        [Header("Position dans le deck (0-4)")]
        public int slotPosition = 0;

        [Header("Références")]
        public Image    background;
        public Image    cardArtwork;
        public TMP_Text slotLabel;

        [Header("Couleurs")]
        public Color colorEmpty    = new Color(0.20f, 0.20f, 0.20f, 0.85f);
        public Color colorFilled   = new Color(0.25f, 0.55f, 0.25f, 0.90f);
        public Color colorSelected = new Color(0.45f, 0.25f, 0.85f, 0.95f);

        private CardData _assignedCard;
        private bool     _isSelected;

        private void Awake()
        {
            if (background == null) background = GetComponent<Image>();
            if (slotLabel  == null) slotLabel  = GetComponentInChildren<TMP_Text>();
            SetEmpty();
        }

        public CardData GetCard() => _assignedCard;

        public void SetCard(CardData card)
        {
            _assignedCard = card;

            if (cardArtwork != null)
            {
                cardArtwork.sprite = card.artwork;
                cardArtwork.gameObject.SetActive(card.artwork != null);
                if (background != null)
                    background.color = _isSelected ? colorSelected : colorFilled;
            }
            else if (background != null)
            {
                background.sprite = card.artwork;
                background.color  = card.artwork != null ? Color.white
                    : (_isSelected ? colorSelected : colorFilled);
            }

            if (slotLabel != null)
                slotLabel.gameObject.SetActive(false);
        }

        public void SetEmpty()
        {
            _assignedCard = null;

            if (cardArtwork != null)
                cardArtwork.gameObject.SetActive(false);
            else if (background != null)
                background.sprite = null;

            if (slotLabel != null)
            {
                slotLabel.gameObject.SetActive(true);
                slotLabel.text = $"Slot {slotPosition + 1}";
            }

            if (background != null)
                background.color = _isSelected ? colorSelected : colorEmpty;
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (background == null) return;
            if (selected)
                background.color = colorSelected;
            else
                background.color = _assignedCard != null ? colorFilled : colorEmpty;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Mode DeckSlotsManager (inline) : clic sur carte assignée = la retirer
            if (DeckSlotsManager.IsEditing)
            {
                if (_assignedCard != null)
                    DeckSlotsManager.Instance.RemoveCardAtSlot(slotPosition);
                return;
            }
            // Mode DeckEditorManager (ancien panel séparé)
            DeckEditorManager.Instance?.OnCardSelectSlotClicked(slotPosition);
        }
    }
}
