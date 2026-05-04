using System.Collections;
using UnityEngine;
using TMPro;

namespace Astraleum
{
    public class PassiveTooltipManager : MonoBehaviour
    {
        public static PassiveTooltipManager Instance;

        [Header("Références")]
        public GameObject tooltipPanel;
        public TMP_Text   passiveName;
        public TMP_Text   passiveDesc;

        private void Awake() => Instance = this;

        public void Show(CardInstance card, RectTransform cardRect)
        {
            if (tooltipPanel == null || card?.data?.passive == null) return;

            string desc = card.data.passive.passiveDescription;
            if (string.IsNullOrEmpty(desc)) return;

            if (passiveName != null)
            {
                passiveName.text  = card.data.passive.passiveName ?? "Passif";
                passiveName.color = card.data.passive.passiveColor;
            }

            if (passiveDesc != null)
                passiveDesc.text = desc;

            tooltipPanel.SetActive(true);
            StartCoroutine(PositionOnCard(cardRect));
        }

        public void Hide()
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }

        private IEnumerator PositionOnCard(RectTransform cardRect)
        {
            yield return null;
            yield return null;

            Canvas.ForceUpdateCanvases();

            var rt     = tooltipPanel.GetComponent<RectTransform>();
            var canvas = tooltipPanel.GetComponentInParent<Canvas>();
            if (rt == null || canvas == null || cardRect == null) yield break;

            var canvasRT = canvas.GetComponent<RectTransform>();

            Vector3[] cardCorners = new Vector3[4];
            cardRect.GetWorldCorners(cardCorners);
            Vector3 cardTopCenter = (cardCorners[1] + cardCorners[2]) / 2f;

            // Pour Screen Space Overlay, GetWorldCorners retourne déjà des pixels écran.
            // Pour Screen Space Camera/World, il faut projeter via la caméra du canvas.
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screenPoint = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? new Vector2(cardTopCenter.x, cardTopCenter.y)
                : RectTransformUtility.WorldToScreenPoint(cam, cardTopCenter);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT,
                screenPoint,
                cam,
                out Vector2 local
            );

            local.y -= cardRect.rect.height * 0.3f;

            float cW   = canvasRT.rect.width;
            float cH   = canvasRT.rect.height;
            float tipW = rt.rect.width;
            float tipH = rt.rect.height;

            local.x = Mathf.Clamp(local.x, -cW / 2f + tipW / 2f, cW / 2f - tipW / 2f);
            local.y = Mathf.Clamp(local.y, -cH / 2f + tipH / 2f, cH / 2f - tipH / 2f);

            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local;
        }
    }
}