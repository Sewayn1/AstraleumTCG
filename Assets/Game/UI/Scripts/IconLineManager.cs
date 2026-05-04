using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum
{
    public class IconLineManager : MonoBehaviour
    {
        private List<GameObject> activeLines = new List<GameObject>();

        public void Clear()
        {
            foreach (var line in activeLines)
                if (line != null) Destroy(line);
            activeLines.Clear();

            // Reset la hauteur pour que Content Size Fitter recalcule depuis 0
            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = Vector2.zero;
        }

        public void AddLine(Sprite icon, string text, Color textColor)
        {
            var lineGO = new GameObject("Line", typeof(RectTransform));
            lineGO.transform.SetParent(transform, false);

            var hlg = lineGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            var lineLE = lineGO.AddComponent<LayoutElement>();
            lineLE.minHeight = 14f;
            lineLE.flexibleHeight = 1f;
            lineLE.flexibleWidth = 1f;

            // Icône
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(lineGO.transform, false);

            var img = iconGO.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.sprite = icon;
            img.color = icon != null ? Color.white : Color.clear;

            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.minWidth = 14f;
            iconLE.preferredWidth = 14f;
            iconLE.minHeight = 14f;
            iconLE.preferredHeight = 14f;
            iconLE.flexibleWidth = 0f;

            // Texte
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(lineGO.transform, false);

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = textColor;
            tmp.fontSize = 13f;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;

            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1f;
            labelLE.flexibleHeight = 1f;

            activeLines.Add(lineGO);
        }
        public void AddSeparator()
        {
            var sep = new GameObject("Sep", typeof(RectTransform));
            sep.transform.SetParent(transform, false);

            var img = sep.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.08f);
            img.raycastTarget = false;

            var le = sep.AddComponent<LayoutElement>();
            le.minHeight = 1f;
            le.preferredHeight = 1f;
            le.flexibleHeight = 0f;

            activeLines.Add(sep);
        }

    }
}