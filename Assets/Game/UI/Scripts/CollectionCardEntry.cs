using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Astraleum.UI
{
    /// <summary>
    /// À attacher sur CardPrefab2 utilisé dans la collection.
    /// Assigner cardFaceImage dans l'inspecteur du prefab.
    /// </summary>
    public class CollectionCardEntry : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ITooltipContent
    {
        [Tooltip("L'Image affichant la face de la carte (à assigner dans le prefab).")]
        public Image cardFaceImage;

        [Tooltip("Matériau niveaux de gris (Astraleum/UI/Grayscale) appliqué à l'artwork des cartes non possédées.")]
        public Material grayscaleMaterial;

        private CanvasGroup canvasGroup;
        private CardData cardData;
        private CardZoomHandler _zoomHandler;
        private TooltipTrigger _unlockTooltip;
        public CardData CardData => cardData;

        [Header("Overlay sélection deck — auto-créé si null")]
        public Image selectedOverlay;

        private void Awake()
        {
            canvasGroup   = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _zoomHandler  = GetComponent<CardZoomHandler>();

            if (cardFaceImage == null)
                cardFaceImage = GetComponent<Image>()
                             ?? GetComponentInChildren<Image>();

            if (selectedOverlay == null)
            {
                var go = new GameObject("DeckSelectedOverlay",
                                        typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                selectedOverlay       = go.GetComponent<Image>();
                selectedOverlay.color = new Color(0.15f, 0.85f, 0.35f, 0.18f);
                go.SetActive(false);
            }

            // Bulle d'indication d'obtention — active uniquement sur les cartes non possédées (voir Setup).
            _unlockTooltip = GetComponent<TooltipTrigger>();
            if (_unlockTooltip == null)
                _unlockTooltip = gameObject.AddComponent<TooltipTrigger>();
            _unlockTooltip.triggerMode = TooltipTriggerMode.HoverDelay; // le clic droit ouvre déjà CardSkillPanelUI
            _unlockTooltip.anchor      = TooltipAnchor.AboveTarget;     // centré sur la carte, pas sur le curseur
            _unlockTooltip.hoverDelay  = 0.5f;
            _unlockTooltip.enabled     = false;
        }

        public void SetInDeck(bool inDeck)
        {
            if (selectedOverlay != null)
                selectedOverlay.gameObject.SetActive(inDeck);
        }

        public void Setup(CardData card, bool owned)
        {
            // Ensure Awake() has run (template is inactive when instantiated)
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            cardData = card;

            // ── Artwork ───────────────────────────────────────────────
            if (cardFaceImage != null && card.artwork != null)
                cardFaceImage.sprite = card.artwork;

            // ── HP / Compétences via CardInstance + CardVisualUpdater ──
            var cardInstance = GetComponent<CardInstance>();
            if (cardInstance != null)
                cardInstance.Initialize(card, slot: 0, playerID: 0);

            GetComponent<CardVisualUpdater>()?.UpdateVisuals();

            // ── Carte non possédée : artwork en niveaux de gris + bulle d'obtention ──
            if (cardFaceImage != null)
                cardFaceImage.material = owned ? null : grayscaleMaterial;

            canvasGroup.alpha          = 1f;
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = true; // raycast actif pour le clic

            if (_unlockTooltip != null)
                _unlockTooltip.enabled = !owned;
        }

        // ── ITooltipContent — bulle "Obtention : ..." sur les cartes verrouillées ──
        public string GetTooltipTitle() => LocalizationManager.Get("collection_unlock_title");
        public string GetTooltipBody()  => PlayerCollection.GetUnlockHint(cardData != null ? cardData.cardNumber : -1);

        public void OnPointerEnter(PointerEventData eventData) => _zoomHandler?.ZoomIn();
        public void OnPointerExit(PointerEventData eventData)  => _zoomHandler?.ZoomOut();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (cardData == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                CardSkillPanelUI.Instance?.Show(cardData, eventData.position);
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left) return;

            // Ferme le panel si ouvert lors d'un clic gauche
            CardSkillPanelUI.Instance?.Hide();

            // Mode deck editing inline : ajouter la carte au prochain slot libre
            if (DeckSlotsManager.IsEditing)
            {
                DeckSlotsManager.Instance.AssignCardToNextSlot(cardData.cardNumber);
                return;
            }

            CardDetailPanel.Instance?.Show(cardData);
        }
    }
}
