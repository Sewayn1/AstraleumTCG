using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Astraleum.UI
{
    /// <summary>
    /// À attacher sur un bouton catégorie (ex. Btn_Training) qui révèle/masque un sous-menu
    /// de boutons par fondu en cascade. Les items ne sont JAMAIS déplacés — seule leur
    /// transparence est animée — pour garantir qu'ils restent exactement à la position
    /// définie dans l'éditeur, active ou non.
    /// </summary>
    public class DropdownToggle : MonoBehaviour
    {
        [Tooltip("Boutons révélés/masqués au clic, dans l'ordre d'affichage.")]
        [SerializeField] private RectTransform[] items;
        [SerializeField] private float staggerDelay = 0.06f;
        [SerializeField] private float duration = 0.25f;

        private Button _toggleButton;
        private CanvasGroup[] _groups;
        private bool _expanded = false;

        private void Awake()
        {
            _toggleButton = GetComponent<Button>();
            _toggleButton.onClick.AddListener(Toggle);

            _groups = new CanvasGroup[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                _groups[i] = items[i].GetComponent<CanvasGroup>();
                if (_groups[i] == null) _groups[i] = items[i].gameObject.AddComponent<CanvasGroup>();
                _groups[i].alpha = 0f;
                items[i].gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // Toujours replié à l'ouverture du panel — pas d'animation (état instantané).
            if (!_expanded) return;
            _expanded = false;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                _groups[i].DOKill();
                items[i].gameObject.SetActive(false);
                _groups[i].alpha = 0f;
            }
        }

        private void Toggle()
        {
            _expanded = !_expanded;
            if (_expanded) Expand();
            else Collapse();
        }

        /// <summary>Replie le sous-menu s'il est actuellement déplié (no-op sinon). Appelable depuis l'extérieur
        /// (ex. Btn_Normal doit refermer le dropdown Btn_Training avec la même animation).</summary>
        public void ForceCollapse()
        {
            if (!_expanded) return;
            _expanded = false;
            Collapse();
        }

        private void Expand()
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                var rt = items[i];
                var cg = _groups[i];

                cg.DOKill();
                rt.gameObject.SetActive(true);
                cg.alpha = 0f;

                cg.DOFade(1f, duration)
                  .SetDelay(i * staggerDelay)
                  .SetUpdate(true);
            }
        }

        private void Collapse()
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                var rt = items[i];
                var cg = _groups[i];

                cg.DOKill();
                cg.DOFade(0f, duration)
                  .SetUpdate(true)
                  .OnComplete(() => rt.gameObject.SetActive(false));
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                _groups[i]?.DOKill();
            }
        }
    }
}
