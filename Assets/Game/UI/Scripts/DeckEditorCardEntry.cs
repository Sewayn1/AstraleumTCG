using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Astraleum.UI
{
    /// <summary>
    /// À attacher sur CardPrefabDeckEditor utilisé dans AllCardsZone du Panel_DeckEditor.
    /// Clic = ajouter/retirer la carte du deck en cours d'édition.
    /// </summary>
    public class DeckEditorCardEntry : MonoBehaviour, IPointerClickHandler
    {
        private const float IN_DECK_ALPHA  = 0.35f;
        private const float OUT_DECK_ALPHA = 1f;

        [Tooltip("L'Image affichant la face de la carte (à assigner dans le prefab).")]
        public Image cardFaceImage;

        [Tooltip("TMP_Text affichant le numéro d'ordre de sélection (1-5). À assigner dans le prefab.")]
        public TMP_Text orderLabel;

        private CanvasGroup canvasGroup;
        private CardData    cardData;
        public  CardData    CardData => cardData;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (cardFaceImage == null)
                cardFaceImage = GetComponent<Image>()
                             ?? GetComponentInChildren<Image>();
        }

        public void Setup(CardData card, bool owned)
        {
            cardData = card;

            // Artwork
            if (cardFaceImage != null && card.artwork != null)
                cardFaceImage.sprite = card.artwork;

            // HP / compétences via CardInstance + CardVisualUpdater
            var cardInstance = GetComponent<CardInstance>();
            if (cardInstance != null)
                cardInstance.Initialize(card, slot: 0, playerID: 0);

            GetComponent<CardVisualUpdater>()?.UpdateVisuals();

            // Grisage si non possédée
            canvasGroup.alpha          = owned ? OUT_DECK_ALPHA : IN_DECK_ALPHA;
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = true;
        }

        /// <summary>
        /// Appelé par DeckEditorManager pour refléter l'état du deck courant.
        /// orderNumber : 1-5 si la carte est dans le deck, 0 si absente.
        /// </summary>
        public void SetInDeck(bool inDeck, int orderNumber = 0)
        {
            canvasGroup.alpha = inDeck ? IN_DECK_ALPHA : OUT_DECK_ALPHA;

            if (orderLabel != null)
            {
                orderLabel.gameObject.SetActive(inDeck);
                if (inDeck) orderLabel.text = orderNumber.ToString();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (cardData == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                DeckEditorSkillPreview.Instance?.Show(cardData);
                return;
            }

            DeckEditorManager.Instance?.ToggleCardInDeck(cardData.cardNumber);
        }
    }
}
