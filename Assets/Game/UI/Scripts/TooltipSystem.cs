using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    public enum TooltipAnchor { NearCursor, RightEdge, LeftEdge, AboveTarget, RightOfCursor }

    public class TooltipSystem : MonoBehaviour
    {
        public static TooltipSystem Instance { get; private set; }

        [Header("Références")]
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private TMP_Text       titleText;
        [SerializeField] private GameObject     titleRow;
        [SerializeField] private TMP_Text       bodyText;

        [Header("Positionnement")]
        [SerializeField] private float cursorOffsetX    = 15f;
        [SerializeField] private float cursorOffsetY    = -10f;
        [SerializeField] private float edgeMargin       = 10f;

        [Header("Typographie")]
        [SerializeField] private float defaultBodyFontSize = 10f;

        private TooltipAnchor _currentAnchor;
        private bool          _followCursor;
        private Canvas        _rootCanvas;
        // Vrai tant qu'un tooltip "épinglé" (PassiveTooltipManager, déclenché par un clic carte) doit
        // rester affiché indépendamment du survol — évite qu'un OnPointerExit de survol (ex. déclenché
        // par l'animation de zoom de la carte qui fait sortir le curseur de ses bornes juste après le
        // clic) referme silencieusement un tooltip qui vient tout juste de s'afficher. Seul un Hide
        // explicite en force=true, ou un nouveau ShowAtTarget(pinned:true), peut le remplacer.
        private bool           _pinned;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            var c = GetComponentInParent<Canvas>();
            _rootCanvas = c != null ? c.rootCanvas : null;
            tooltipPanel.gameObject.SetActive(false);

            // Assigner le sprite asset une fois pour que les tags <sprite> fonctionnent
            if (bodyText != null)
            {
                var sa = UnityEngine.Resources.Load<TMPro.TMP_SpriteAsset>("TMP_Icons/AstralanIcons");
                if (sa != null) bodyText.spriteAsset = sa;
            }
        }

        // Affiche le tooltip près du curseur (ou sur un bord). bodyFontSize=0 → taille par défaut.
        public void Show(string title, string body, TooltipAnchor anchor = TooltipAnchor.NearCursor, float bodyFontSize = 0f)
        {
            if (string.IsNullOrEmpty(body)) return;
            if (_pinned) return; // un survol ne doit jamais voler l'affichage à un tooltip épinglé

            SetContent(title, body, bodyFontSize);

            _currentAnchor = anchor;
            _followCursor  = anchor == TooltipAnchor.NearCursor || anchor == TooltipAnchor.RightOfCursor;

            tooltipPanel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);
            ApplyPosition();
        }

        // Affiche le tooltip centré sur un RectTransform cible (ex : carte).
        // pinned=true (PassiveTooltipManager uniquement) : le tooltip résiste aux Hide()/Show() non
        // forcés déclenchés par un survol (voir _pinned) jusqu'à un Hide(force:true) explicite.
        public void ShowAtTarget(string title, string body, RectTransform target, float bodyFontSize = 0f, bool pinned = false)
        {
            if (string.IsNullOrEmpty(body) || target == null) return;
            if (_pinned && !pinned) return; // un survol ne doit jamais voler l'affichage à un tooltip épinglé

            SetContent(title, body, bodyFontSize);

            _currentAnchor = TooltipAnchor.AboveTarget;
            _followCursor  = false;
            _pinned        = pinned;

            tooltipPanel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);
            PositionAtTarget(target);
        }

        // force=true : ferme même un tooltip épinglé (fermeture délibérée — clic ailleurs, annulation).
        // force=false (défaut, utilisé par tout Hide() déclenché par un survol) : no-op tant qu'un
        // tooltip épinglé est affiché, pour ne pas l'effacer accidentellement.
        public void Hide(bool force = false)
        {
            if (_pinned && !force) return;
            _pinned       = false;
            _followCursor = false;
            tooltipPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_followCursor || !tooltipPanel.gameObject.activeSelf) return;

            // Garde-fou : si le curseur système a quitté la zone de jeu (autre fenêtre/écran, focus perdu)
            // sans déclencher OnPointerExit sur la source du tooltip, Input.mousePosition part hors des
            // bornes de l'écran et le calcul se fait clamper dans un coin — on cache plutôt que d'afficher n'importe où.
            Vector3 mouse = Input.mousePosition;
            if (mouse.x < 0f || mouse.x > Screen.width || mouse.y < 0f || mouse.y > Screen.height)
            {
                Hide();
                return;
            }

            PositionNearCursor(allowFlip: _currentAnchor == TooltipAnchor.NearCursor);
        }

        // ── Contenu ───────────────────────────────────────────────────────

        private void SetContent(string title, string body, float bodyFontSize)
        {
            bool hasTitle = !string.IsNullOrEmpty(title);
            if (titleText != null) titleText.text = hasTitle ? title : string.Empty;
            if (titleRow  != null) titleRow.SetActive(hasTitle);
            bodyText.text     = body;
            bodyText.fontSize = bodyFontSize > 0f ? bodyFontSize : defaultBodyFontSize;
        }

        // ── Positionnement ────────────────────────────────────────────────

        private void ApplyPosition()
        {
            switch (_currentAnchor)
            {
                case TooltipAnchor.NearCursor:
                case TooltipAnchor.AboveTarget:
                    PositionNearCursor(allowFlip: true);
                    break;
                case TooltipAnchor.RightOfCursor:
                    PositionNearCursor(allowFlip: false);
                    break;
                case TooltipAnchor.RightEdge:
                    PositionAtEdge(1f, 1f, -edgeMargin);
                    break;
                case TooltipAnchor.LeftEdge:
                    PositionAtEdge(0f, 0f, edgeMargin);
                    break;
            }
        }

        // allowFlip=false : reste toujours à droite du curseur (clampé aux bords), ne bascule jamais à gauche.
        private void PositionNearCursor(bool allowFlip)
        {
            if (_rootCanvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_rootCanvas.transform,
                Input.mousePosition,
                _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera,
                out Vector2 cursor
            );

            Vector2 size       = tooltipPanel.rect.size;
            Rect    canvasRect = ((RectTransform)_rootCanvas.transform).rect;

            float ox = cursorOffsetX;
            float oy = cursorOffsetY;

            if (allowFlip && cursor.x + ox + size.x > canvasRect.xMax - edgeMargin)
                ox = -cursorOffsetX - size.x;

            float finalX = Mathf.Clamp(cursor.x + ox,
                canvasRect.xMin + edgeMargin,
                canvasRect.xMax - edgeMargin - size.x);

            if (cursor.y + oy - size.y < canvasRect.yMin + edgeMargin)
                oy = Mathf.Abs(cursorOffsetY) + size.y;

            float finalY = Mathf.Clamp(cursor.y + oy,
                canvasRect.yMin + edgeMargin + size.y,
                canvasRect.yMax - edgeMargin);

            tooltipPanel.anchorMin        = new Vector2(0f, 1f);
            tooltipPanel.anchorMax        = new Vector2(0f, 1f);
            tooltipPanel.pivot            = new Vector2(0f, 1f);
            // anchorMin/Max/pivot = (0,1) → l'ancre est le coin haut-gauche du parent, donc anchoredPosition
            // est un OFFSET depuis ce coin, pas une coordonnée absolue centrée comme finalX/finalY (issues de
            // ScreenPointToLocalPointInRectangle, en espace centré sur le pivot du canvas). Conversion nécessaire :
            tooltipPanel.anchoredPosition = new Vector2(finalX - canvasRect.xMin, finalY - canvasRect.yMax);
        }

        private void PositionAtTarget(RectTransform target)
        {
            if (_rootCanvas == null) return;

            // Centre monde du target → coordonnées locales canvas
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 worldCenter = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

            Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
            Vector2 screenCenter = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? new Vector2(worldCenter.x, worldCenter.y)
                : RectTransformUtility.WorldToScreenPoint(cam, worldCenter);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_rootCanvas.transform,
                screenCenter,
                cam,
                out Vector2 localCenter
            );

            // Anchor + pivot (0.5, 0.5) : anchoredPosition = centre du tooltip en espace local canvas.
            // localCenter est déjà dans cet espace → positionne le tooltip centré sur la carte.
            Vector2 size       = tooltipPanel.rect.size;
            Rect    canvasRect = ((RectTransform)_rootCanvas.transform).rect;

            float cx = Mathf.Clamp(localCenter.x,
                canvasRect.xMin + edgeMargin + size.x * 0.5f,
                canvasRect.xMax - edgeMargin - size.x * 0.5f);

            float cy = Mathf.Clamp(localCenter.y,
                canvasRect.yMin + edgeMargin + size.y * 0.5f,
                canvasRect.yMax - edgeMargin - size.y * 0.5f);

            tooltipPanel.anchorMin        = new Vector2(0.5f, 0.5f);
            tooltipPanel.anchorMax        = new Vector2(0.5f, 0.5f);
            tooltipPanel.pivot            = new Vector2(0.5f, 0.5f);
            tooltipPanel.anchoredPosition = new Vector2(cx, cy);
        }

        private void PositionAtEdge(float anchorX, float pivotX, float offsetX)
        {
            tooltipPanel.anchorMin        = new Vector2(anchorX, 0.5f);
            tooltipPanel.anchorMax        = new Vector2(anchorX, 0.5f);
            tooltipPanel.pivot            = new Vector2(pivotX,  0.5f);
            tooltipPanel.anchoredPosition = new Vector2(offsetX, 0f);
        }
    }
}
