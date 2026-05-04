using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    public class CardTargetHighlight : MonoBehaviour
    {
        [Header("Pulse Settings")]
        public float pulseSpeed    = 2.5f;
        public float pulseMinScale = 1.00f;
        public float pulseMaxScale = 1.04f;

        [Header("Bounce Settings (sélection remote)")]
        public float bounceHeight = 8f;
        public float bounceSpeed  = 3.5f;

        private Image     cardImage;
        private Vector3   originalScale;
        private Color     originalColor;
        private Vector3   originalLocalPos;
        private Coroutine pulseCoroutine;
        private Coroutine bounceCoroutine;
        private bool      isHighlighted = false;
        private bool      isBouncing    = false;

        // Couleurs de highlight
        private static readonly Color attackColor    = new Color(1f,    0.55f, 0.45f, 1f);
        private static readonly Color healColor      = new Color(0.45f, 0.95f, 0.55f, 1f);
        private static readonly Color blockedColor   = new Color(0.5f,  0.5f,  0.5f,  1f);
        private static readonly Color selectionColor = new Color(0.35f, 0.75f, 1f,    1f);

        private Color currentHighlightColor;

        private void Awake()
        {
            cardImage       = GetComponent<Image>();
            originalScale   = transform.localScale;
            originalColor   = cardImage != null ? cardImage.color : Color.white;
            originalLocalPos = transform.localPosition;
        }

        // ── Highlight de ciblage (pulse échelle + teinte) ─────────────────

        public void ActivateHighlight(HighlightType type = HighlightType.Attack)
        {
            if (isHighlighted) return;
            isHighlighted = true;

            currentHighlightColor = type switch
            {
                HighlightType.Heal    => healColor,
                HighlightType.Blocked => blockedColor,
                _                     => attackColor
            };

            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            pulseCoroutine = StartCoroutine(PulseRoutine());
        }

        public void DeactivateHighlight()
        {
            if (!isHighlighted) return;
            isHighlighted = false;

            if (pulseCoroutine != null) {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }

            StartCoroutine(ResetRoutine());
        }

        // ── Bounce de sélection (oscillation verticale, côté remote) ──────

        public void ActivateBounce()
        {
            if (isBouncing) return;
            isBouncing       = true;
            originalLocalPos = transform.localPosition;
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            bounceCoroutine  = StartCoroutine(BounceRoutine());
        }

        public void DeactivateBounce()
        {
            if (!isBouncing) return;
            isBouncing = false;
            if (bounceCoroutine != null) { StopCoroutine(bounceCoroutine); bounceCoroutine = null; }
            StartCoroutine(ResetBounceRoutine());
        }

        // ── Coroutines ────────────────────────────────────────────────────

        private IEnumerator PulseRoutine()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * pulseSpeed;
                float factor = (Mathf.Sin(t) + 1f) / 2f;

                transform.localScale = originalScale * Mathf.Lerp(pulseMinScale, pulseMaxScale, factor);

                if (cardImage != null)
                    cardImage.color = Color.Lerp(originalColor, currentHighlightColor, factor * 0.35f);

                yield return null;
            }
        }

        private IEnumerator BounceRoutine()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * bounceSpeed;
                float offset = Mathf.Sin(t) * bounceHeight;
                transform.localPosition = originalLocalPos + new Vector3(0f, offset, 0f);

                if (cardImage != null)
                    cardImage.color = Color.Lerp(originalColor, selectionColor, (Mathf.Sin(t) + 1f) / 2f * 0.2f);

                yield return null;
            }
        }

        private IEnumerator ResetRoutine()
        {
            float   elapsed    = 0f;
            float   duration   = 0.15f;
            Vector3 startScale = transform.localScale;
            Color   startColor = cardImage != null ? cardImage.color : originalColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, originalScale, t);
                if (cardImage != null) cardImage.color = Color.Lerp(startColor, originalColor, t);
                yield return null;
            }

            transform.localScale = originalScale;
            if (cardImage != null) cardImage.color = originalColor;
        }

        private IEnumerator ResetBounceRoutine()
        {
            float   elapsed  = 0f;
            float   duration = 0.2f;
            Vector3 startPos = transform.localPosition;
            Color   startCol = cardImage != null ? cardImage.color : originalColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = elapsed / duration;
                transform.localPosition = Vector3.Lerp(startPos, originalLocalPos, t);
                if (cardImage != null) cardImage.color = Color.Lerp(startCol, originalColor, t);
                yield return null;
            }

            transform.localPosition = originalLocalPos;
            if (cardImage != null) cardImage.color = originalColor;
        }
    }

    public enum HighlightType
    {
        Attack,
        Heal,
        Blocked
    }
}
