using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    public class Panel_LeaveGame : MonoBehaviour
    {
        public static Panel_LeaveGame Instance;

        [SerializeField] private Button btnOk;

        private void Awake()
        {
            Instance = this;

            if (btnOk == null) btnOk = transform.Find("Btn_Ok")?.GetComponent<Button>();
            btnOk?.onClick.AddListener(Hide);

            gameObject.SetActive(false);
        }

        private void Start()
        {
            if (GameManager.ShowLeaveGameNotice)
            {
                GameManager.ShowLeaveGameNotice = false;
                Show();
            }
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
