using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum
{
    public class TurnAnnouncementManager : MonoBehaviour
    {
        public static TurnAnnouncementManager Instance;

        [Header("Références")]
        public CanvasGroup canvasGroup;
        public RectTransform panelRT;
        public TMP_Text label;

        [Header("Couleurs")]
        public Color colorMyTurn    = new Color(0.30f, 0.88f, 0.42f, 1f);
        public Color colorEnemyTurn = new Color(0.92f, 0.28f, 0.28f, 1f);

        [Header("Timing")]
        public float fadeInDuration  = 0.28f;
        public float holdDuration    = 1.0f;
        public float fadeOutDuration = 0.32f;

        private Coroutine _routine;

        private void Awake()
        {
            Instance = this;
            if (canvasGroup != null)
            {
                canvasGroup.alpha          = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable   = false;
            }
            if (panelRT != null)
                panelRT.localScale = Vector3.one;
        }

        public void Show(int currentPlayerID)
        {
            bool isMyTurn;
            if (NetworkBridge.IsActive)
                isMyTurn = currentPlayerID == NetworkBridge.LocalPlayerID;
            else
                isMyTurn = currentPlayerID == 0;

            label.text  = isMyTurn ? "VOTRE TOUR" : "TOUR ADVERSE";
            label.color = isMyTurn ? colorMyTurn : colorEnemyTurn;

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            // — Fade in + scale in ————————————————————————
            float t = 0f;
            Vector3 scaleIn  = new Vector3(0.78f, 0.78f, 1f);
            Vector3 scaleRest = Vector3.one;

            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                float p    = Mathf.Clamp01(t / fadeInDuration);
                float ease = 1f - Mathf.Pow(1f - p, 3f); // easeOutCubic
                canvasGroup.alpha = ease;
                panelRT.localScale = Vector3.LerpUnclamped(scaleIn, scaleRest, ease);
                yield return null;
            }

            canvasGroup.alpha  = 1f;
            panelRT.localScale = scaleRest;

            // — Hold ———————————————————————————————————————
            yield return new WaitForSeconds(holdDuration);

            // — Fade out + scale out ——————————————————————
            t = 0f;
            Vector3 scaleOut = new Vector3(1.08f, 1.08f, 1f);

            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                float p    = Mathf.Clamp01(t / fadeOutDuration);
                float ease = p * p; // easeInQuad
                canvasGroup.alpha  = 1f - ease;
                panelRT.localScale = Vector3.LerpUnclamped(scaleRest, scaleOut, ease);
                yield return null;
            }

            canvasGroup.alpha  = 0f;
            panelRT.localScale = scaleRest;
            _routine = null;
        }
    }
}
