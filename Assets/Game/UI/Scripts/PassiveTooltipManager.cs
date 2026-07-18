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
            // pinned:true — reste affiché même si un OnPointerExit de survol se déclenche juste après
            // (ex. animation de zoom de la carte qui fait sortir le curseur de ses bornes), voir
            // TooltipSystem._pinned. Seul PassiveTooltipManager.Hide() (fermeture délibérée) le referme.
            TooltipSystem.Instance?.ShowAtTarget(title, desc, cardRect, 10f, pinned: true);
        }

        public void Hide() => TooltipSystem.Instance?.Hide(force: true);

        private static string ColoredTitle(string name, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return $"<color=#{hex}>{name}</color>";
        }
    }
}
