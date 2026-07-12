using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Astraleum.UI
{
    public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float hoverScale    = 1.08f;
        [SerializeField] private float hoverDuration = 0.15f;
        [SerializeField] private float clickPunch    = 0.12f;
        [SerializeField] private float clickDuration = 0.25f;

        private void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnClick);
        }

        public void OnPointerEnter(PointerEventData _)
        {
            transform.DOKill();
            transform.DOScale(hoverScale, hoverDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        public void OnPointerExit(PointerEventData _)
        {
            transform.DOKill();
            transform.DOScale(1f, hoverDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void OnClick()
        {
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * clickPunch, clickDuration, 1, 0.5f)
                .SetUpdate(true);
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
