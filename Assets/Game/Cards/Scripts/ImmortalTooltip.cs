using UnityEngine;

namespace Astraleum
{
    // Posé sur l'icône Immortal (CardPrefabBoss), en complément d'un TooltipTrigger sur le même
    // GameObject. Contrairement à StatusIconTooltip (BurnIcon/Poison/Bleed/HealBlock/HOT), l'état
    // immortel n'est pas un ActiveEffect/EffectType — c'est CardInstance.isImmortal, un simple bool
    // — donc pas de section "live" à fusionner, juste le texte générique.
    [AddComponentMenu("Astraleum/UI/Immortal Tooltip")]
    public class ImmortalTooltip : MonoBehaviour, ITooltipContent
    {
        public string titleKey = "status_title_immortal";
        public string descriptionKey = "codex_states_immortal";

        public string GetTooltipTitle() => LocalizationManager.Get(titleKey);
        public string GetTooltipBody() => LocalizationManager.Get(descriptionKey);
    }
}
