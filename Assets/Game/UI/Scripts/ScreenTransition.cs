using System.Collections;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Transition plein écran (fondu vers opaque puis retour) — masque les changements de décor
    /// pendant une transition de phase de Boss (plateau, artwork de carte, musique). Reste active
    /// en permanence dans la scène, alpha=0 au repos.
    /// </summary>
    public class ScreenTransition : MonoBehaviour
    {
        public static ScreenTransition Instance;

        public CanvasGroup canvasGroup;
        public float coverDuration = 0.5f;
        public float revealDuration = 0.5f;

        private void Awake()
        {
            Instance = this;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        public IEnumerator Cover()
        {
            canvasGroup.blocksRaycasts = true;
            yield return Fade(canvasGroup.alpha, 1f, coverDuration);
        }

        public IEnumerator Reveal()
        {
            yield return Fade(canvasGroup.alpha, 0f, revealDuration);
            canvasGroup.blocksRaycasts = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            canvasGroup.alpha = to;
        }
    }
}
