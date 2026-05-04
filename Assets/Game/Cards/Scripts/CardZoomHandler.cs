using System.Collections;
using UnityEngine;

namespace Astraleum
{
    public class CardZoomHandler : MonoBehaviour
    {
        [Header("Zoom")]
        public float zoomScale    = 1.15f;
        public float zoomDuration = 0.15f;

        private bool      isZoomed = false;
        private Coroutine zoomCoroutine;

        public bool IsZoomed => isZoomed;

        public void ZoomIn()
        {
            isZoomed = true;
            StartZoom(zoomScale);
        }

        public void ZoomOut()
        {
            isZoomed = false;
            StartZoom(1f);
        }

        private void StartZoom(float targetScale)
        {
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
            zoomCoroutine = StartCoroutine(AnimateZoom(targetScale));
        }

        private IEnumerator AnimateZoom(float targetScale)
        {
            float startScale = transform.localScale.x;
            float elapsed    = 0f;

            while (elapsed < zoomDuration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / zoomDuration);
                float s  = Mathf.Lerp(startScale, targetScale, t);
                transform.localScale = Vector3.one * s;
                yield return null;
            }

            transform.localScale = Vector3.one * targetScale;
            zoomCoroutine = null;
        }
    }
}