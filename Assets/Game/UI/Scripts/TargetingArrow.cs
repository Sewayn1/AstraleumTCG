using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Astraleum
{
    /// <summary>
    /// Affiche une flèche allant de la carte source vers le curseur souris pendant le ciblage.
    /// En mode réseau, ShowStatic() trace la flèche entre deux cartes fixes (côté remote).
    /// Placer sous le Canvas. Requiert deux enfants RectTransform : ArrowLine et ArrowHead.
    /// </summary>
    public class TargetingArrow : MonoBehaviour
    {
        public static TargetingArrow Instance;

        [Header("Éléments visuels")]
        public RectTransform arrowLine;
        public RectTransform arrowHead;

        [Header("Réglages")]
        public float lineWidth = 5f;
        public Color arrowColor = new Color(1f, 0.85f, 0.3f, 0.85f);

        private Canvas        canvas;
        private RectTransform canvasRect;
        private bool          isShowing;
        private bool          isShowingStatic;
        private RectTransform sourceRect;
        private RectTransform staticTargetRect;

private void Awake() { Instance = this; canvas = GetComponentInParent<Canvas>(); if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>(); if (arrowLine == null) arrowLine = transform.Find("ArrowLine")?.GetComponent<RectTransform>(); if (arrowHead == null) arrowHead = transform.Find("ArrowHead")?.GetComponent<RectTransform>(); ApplyColor(arrowLine); ApplyColor(arrowHead); gameObject.SetActive(false); }

        private void ApplyColor(RectTransform rt)
        {
            if (rt == null) return;
            var graphic = rt.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null) graphic.color = arrowColor;
        }

        /// <summary>Affiche la flèche depuis le centre de la carte source vers le curseur (local).</summary>
        public void Show(RectTransform source, Color color)
        {
            sourceRect       = source;
            staticTargetRect = null;
            arrowColor       = color;
            ApplyColor(arrowLine);
            ApplyColor(arrowHead);
            isShowing       = true;
            isShowingStatic = false;
            gameObject.SetActive(true);
        }

        /// <summary>Affiche la flèche fixe entre deux cartes (côté remote — pas de curseur).</summary>
        public void ShowStatic(RectTransform source, RectTransform target, Color color)
        {
            sourceRect       = source;
            staticTargetRect = target;
            arrowColor       = color;
            ApplyColor(arrowLine);
            ApplyColor(arrowHead);
            isShowing       = false;
            isShowingStatic = true;
            gameObject.SetActive(true);
        }

        /// <summary>Masque la flèche locale (curseur-suivant).</summary>
        public void Hide()
        {
            isShowing        = false;
            isShowingStatic  = false;
            staticTargetRect = null;
            gameObject.SetActive(false);
            sourceRect = null;
        }

        /// <summary>Masque uniquement la flèche statique (remote target hover).</summary>
        public void HideStatic()
        {
            isShowingStatic  = false;
            staticTargetRect = null;
            if (!isShowing)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            if (canvasRect == null) return;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;

            // ── Mode statique (flèche remote entre deux cartes) ───────────
            if (isShowingStatic && sourceRect != null && staticTargetRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, CardCenterScreen(sourceRect, cam), cam, out Vector2 fromLocal);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, CardCenterScreen(staticTargetRect, cam), cam, out Vector2 toLocal);
                DrawArrow(fromLocal, toLocal);
                return;
            }

            // ── Mode local (flèche curseur-suivant) ───────────────────────
            if (!isShowing || sourceRect == null) return;
            if (Mouse.current == null) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, CardCenterScreen(sourceRect, cam), cam, out Vector2 srcLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, mouseScreen, cam, out Vector2 mouseLocal);
            DrawArrow(srcLocal, mouseLocal);
        }

        private Vector2 CardCenterScreen(RectTransform rt, Camera cam)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && cam != null)
                return ((Vector2)cam.WorldToScreenPoint(corners[0]) + (Vector2)cam.WorldToScreenPoint(corners[2])) * 0.5f;
            return ((Vector2)corners[0] + (Vector2)corners[2]) * 0.5f;
        }

        private void DrawArrow(Vector2 from, Vector2 to)
        {
            Vector2 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 5f) return;

            float angle    = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float headSize = arrowHead != null ? 20f : 0f;
            float lineLen  = Mathf.Max(0f, dist - headSize);

            if (arrowLine != null)
            {
                arrowLine.anchoredPosition = from + dir.normalized * (lineLen * 0.5f);
                arrowLine.sizeDelta        = new Vector2(lineLen, lineWidth);
                arrowLine.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            if (arrowHead != null)
            {
                arrowHead.anchoredPosition = to;
                arrowHead.localEulerAngles = new Vector3(0f, 0f, angle - 90f);
            }
        }
    }
}
