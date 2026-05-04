using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum
{
    /// Crée un groupe Horizontal contenant une Image (icône) + TMP_Text (texte)
    /// à utiliser partout où un caractère Unicode était utilisé.
    [RequireComponent(typeof(HorizontalLayoutGroup))]
    public class InlineIcon : MonoBehaviour
    {
        [Header("Références")]
        public Image    iconImage;
        public TMP_Text label;

        [Header("Taille icône")]
        public float iconSize = 16f;

        public void Set(Sprite icon, string text, Color? textColor = null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.color  = icon != null ? Color.white : Color.clear;

                var rt = iconImage.GetComponent<RectTransform>();
                if (rt != null)
                    rt.sizeDelta = new Vector2(iconSize, iconSize);
            }

            if (label != null)
            {
                label.text = text;
                if (textColor.HasValue)
                    label.color = textColor.Value;
            }
        }

        public void SetText(string text) { if (label != null) label.text = text; }
        public void SetIcon(Sprite icon) { if (iconImage != null) iconImage.sprite = icon; }
        public void SetColor(Color c)    { if (label != null) label.color = c; }
    }
}