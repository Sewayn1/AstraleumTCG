using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Astraleum
{
    public enum TooltipTriggerMode { HoverDelay, RightClick, Both }

    // Implémenter cette interface sur un composant frère pour fournir un contenu dynamique.
    public interface ITooltipContent
    {
        string GetTooltipTitle();
        string GetTooltipBody();
    }

    [AddComponentMenu("Astraleum/UI/Tooltip Trigger")]
    public class TooltipTrigger : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Contenu statique")]
        public string tooltipTitle;
        [TextArea(2, 5)]
        public string tooltipBody;

        [Header("Déclencheur")]
        public TooltipTriggerMode triggerMode = TooltipTriggerMode.Both;
        public float              hoverDelay  = 1.5f;
        public TooltipAnchor      anchor      = TooltipAnchor.NearCursor;

        private Coroutine _hoverCoroutine;

        public void OnPointerEnter(PointerEventData _)
        {
            if (triggerMode == TooltipTriggerMode.RightClick) return;
            CancelHover();
            _hoverCoroutine = StartCoroutine(ShowAfterDelay());
        }

        public void OnPointerExit(PointerEventData _)
        {
            CancelHover();
            TooltipSystem.Instance?.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;
            if (triggerMode == TooltipTriggerMode.HoverDelay) return;

            CancelHover();
            ShowTooltip();
        }

        // Appeler depuis le code pour afficher le tooltip sans interaction souris.
        public void ForceShow() => ShowTooltip();

        // Appeler depuis le code pour cacher le tooltip.
        public void ForceHide()
        {
            CancelHover();
            TooltipSystem.Instance?.Hide();
        }

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(hoverDelay);
            ShowTooltip();
        }

        private void ShowTooltip()
        {
            var (title, body) = GetContent();

            // AboveTarget = centré sur le RectTransform du déclencheur lui-même (ex. carte de Collection),
            // au lieu de suivre le curseur — seul TooltipSystem.ShowAtTarget sait le faire.
            if (anchor == TooltipAnchor.AboveTarget)
                TooltipSystem.Instance?.ShowAtTarget(title, body, transform as RectTransform);
            else
                TooltipSystem.Instance?.Show(title, body, anchor);
        }

        private (string title, string body) GetContent()
        {
            if (TryGetComponent<ITooltipContent>(out var provider))
                return (provider.GetTooltipTitle(), provider.GetTooltipBody());
            return (tooltipTitle, tooltipBody);
        }

        private void CancelHover()
        {
            if (_hoverCoroutine == null) return;
            StopCoroutine(_hoverCoroutine);
            _hoverCoroutine = null;
        }
    }
}
