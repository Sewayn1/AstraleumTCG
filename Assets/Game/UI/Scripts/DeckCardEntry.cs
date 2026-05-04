using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Astraleum.UI
{
    /// <summary>
    /// Représente une carte dans la DeckZone.
    /// Clic sur la carte = retirer du deck.
    /// </summary>
    public class DeckCardEntry : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Image affichant la face de la carte.")]
        public Image cardFaceImage;
        [Tooltip("Texte affiché sur le bouton de suppression (optionnel).")]
        public TMP_Text removeLabel;

        private CardData cardData;

        private void Awake()
        {
            if (cardFaceImage == null)
                cardFaceImage = GetComponent<Image>()
                             ?? GetComponentInChildren<Image>();
        }

        public void Setup(CardData card)
        {
            cardData = card;

            if (cardFaceImage != null && card.artwork != null)
                cardFaceImage.sprite = card.artwork;

            // HP / compétences via CardInstance si le prefab en a un
            var cardInstance = GetComponent<CardInstance>();
            if (cardInstance != null)
                cardInstance.Initialize(card, slot: 0, playerID: 0);

            GetComponent<CardVisualUpdater>()?.UpdateVisuals();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (cardData == null) return;
            DeckEditorManager.Instance?.ToggleCardInDeck(cardData.cardNumber);
        }
    }
}
