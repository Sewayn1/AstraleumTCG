using UnityEngine;
using UnityEngine.EventSystems;

namespace Astraleum
{
    // Empêche la DamagePreviewBar de disparaître quand le curseur y entre depuis une carte.
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public class PreviewBarHoverKeepAlive : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData _) => CombatUIManager.Instance?.CancelHideDamagePreview();
        public void OnPointerExit(PointerEventData _)  => CombatUIManager.Instance?.HideDamagePreview();
    }
}
