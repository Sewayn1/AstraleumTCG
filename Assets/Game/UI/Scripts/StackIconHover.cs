using UnityEngine;
using UnityEngine.EventSystems;

namespace Astraleum
{
    public class StackIconHover : MonoBehaviour,
                                   IPointerEnterHandler,
                                   IPointerExitHandler
    {
        public int     playerID;
        public Element element;

        public void OnPointerEnter(PointerEventData eventData)
        {
            StackDisplayManager.Instance?.ShowTooltip(
                playerID, element, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StackDisplayManager.Instance?.HideTooltip();
        }
    }
    
}