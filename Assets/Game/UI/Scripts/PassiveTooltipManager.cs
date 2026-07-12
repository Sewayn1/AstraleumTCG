using UnityEngine;

namespace Astraleum
{
    public class PassiveTooltipManager : MonoBehaviour
    {
        public static PassiveTooltipManager Instance;

        private void Awake() => Instance = this;

        public void Show(CardInstance card, RectTransform cardRect)
        {
            if (card?.data?.passive == null) return;
            string desc = card.data.passive.passiveDescription;
            if (string.IsNullOrEmpty(desc)) return;

            string title = ColoredTitle(card.data.passive.passiveName ?? "Passif", card.data.passive.passiveColor);
            TooltipSystem.Instance?.ShowAtTarget(title, desc, cardRect, 10f);
        }

        public void Hide() => TooltipSystem.Instance?.Hide();

        private static string ColoredTitle(string name, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return $"<color=#{hex}>{name}</color>";
        }
    }
}
