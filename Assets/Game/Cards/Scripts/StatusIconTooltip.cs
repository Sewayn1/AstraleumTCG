using UnityEngine;

namespace Astraleum
{
    // Posé sur chaque icône de statut (BurnIcon, Poison, Bleed, HealBlock, HOT) dans CardPrefab2,
    // en complément d'un TooltipTrigger sur le même GameObject.
    [AddComponentMenu("Astraleum/UI/Status Icon Tooltip")]
    public class StatusIconTooltip : MonoBehaviour, ITooltipContent
    {
        [Tooltip("Type d'effet représenté par cette icône (ex: Burn, Poison, Saignement, HealBlock, HealOverTime)")]
        public EffectType effectType;

        [Tooltip("Clé de localisation du titre (ex: status_title_burn)")]
        public string titleKey;

        [Tooltip("Clé de localisation de la description générique (ex: codex_states_burn)")]
        public string descriptionKey;

        private CardInstance _card;

        private void Awake() => _card = GetComponentInParent<CardInstance>();

        public string GetTooltipTitle() => LocalizationManager.Get(titleKey);

        public string GetTooltipBody()
        {
            string generic = LocalizationManager.Get(descriptionKey);
            string live = BuffTooltipManager.Instance != null
                ? BuffTooltipManager.Instance.GetEffectTooltip(_card, effectType)
                : "";

            return string.IsNullOrEmpty(live) ? generic : $"{generic}\n\n{live}";
        }
    }
}
