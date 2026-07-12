using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    public class CancelledGamePanel : MonoBehaviour
    {
        [SerializeField] private Button btnClose;

        private bool _initialized;

        public void Show()
        {
            if (!_initialized)
            {
                btnClose?.onClick.AddListener(Close);
                _initialized = true;
            }
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
