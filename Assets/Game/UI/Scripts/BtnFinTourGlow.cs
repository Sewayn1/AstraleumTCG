using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    public class BtnFinTourGlow : MonoBehaviour
    {
        public static BtnFinTourGlow Instance;

        [Header("Références")]
        public Outline outline;

        [Header("Couleurs")]
        public Color outlineActive   = new Color(0.7f, 0.55f, 1f, 1f);
        public Color outlineInactive = new Color(0f,   0f,   0f, 0f);

        [Header("Animation")]
        public float pulseSpeed = 2.5f;

        private bool      isGlowing = false;
        private Coroutine pulseCoroutine;

        private void Awake()
        {
            Instance = this;
            if (outline == null)
                outline = GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = outlineInactive;
        }

        public void SetGlowing(bool active)
        {
            if (isGlowing == active) return;
            isGlowing = active;

            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }

            if (active)
                pulseCoroutine = StartCoroutine(PulseOutline());
            else if (outline != null)
                outline.effectColor = outlineInactive;
        }

        private IEnumerator PulseOutline()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * pulseSpeed;
                float alpha  = Mathf.Lerp(0.4f, 1f, (Mathf.Sin(t) + 1f) / 2f);
                Color c      = outlineActive;
                c.a          = alpha;
                if (outline != null)
                    outline.effectColor = c;
                yield return null;
            }
        }
    }
}