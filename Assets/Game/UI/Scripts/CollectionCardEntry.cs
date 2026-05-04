using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Astraleum.UI
{
    /// <summary>
    /// À attacher sur CardPrefab2 utilisé dans la collection.
    /// Assigner cardFaceImage dans l'inspecteur du prefab.
    /// </summary>
    public class CollectionCardEntry : MonoBehaviour, IPointerClickHandler
    {
        private const float LOCKED_ALPHA = 0.35f;

        [Tooltip("L'Image affichant la face de la carte (à assigner dans le prefab).")]
        public Image cardFaceImage;

        private CanvasGroup canvasGroup;
        private CardData cardData;
        public CardData CardData => cardData;

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

            // ── Artwork ───────────────────────────────────────────────
            if (cardFaceImage != null && card.artwork != null)
                cardFaceImage.sprite = card.artwork;

            // ── HP / Compétences via CardInstance + CardVisualUpdater ──
            var cardInstance = GetComponent<CardInstance>();
            if (cardInstance != null)
                cardInstance.Initialize(card, slot: 0, playerID: 0);

            GetComponent<CardVisualUpdater>()?.UpdateVisuals();

            // ── Grisage si non possédée ───────────────────────────────
            canvasGroup.alpha          = owned ? 1f : LOCKED_ALPHA;
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = true; // raycast actif pour le clic
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (cardData == null) return;
            CardDetailPanel.Instance?.Show(cardData);
        }
    }
}
