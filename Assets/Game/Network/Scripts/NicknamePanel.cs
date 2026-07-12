using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    public class NicknamePanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputNickname;
        [SerializeField] private Button         btnValidate;
        [SerializeField] private TMP_Text       lblFeedback;

        private const string KEY_NAME         = "PlayerName";
        private const string KEY_FIRST_LAUNCH = "FirstLaunchDone";
        private const int    MAX_LENGTH       = 16;

        private void Awake()
        {
            // Premier lancement déjà effectué → masquer Panel_FirstLaunch immédiatement
            if (PlayerPrefs.GetInt(KEY_FIRST_LAUNCH, 0) == 1)
            {
                transform.parent.gameObject.SetActive(false);
                return;
            }

            // Auto-détection si non assignés en inspecteur
            if (inputNickname == null)
                inputNickname = GetComponent<TMP_InputField>();
            if (btnValidate == null)
            {
                var t = transform.Find("ValidateNickname");
                if (t != null) btnValidate = t.GetComponent<Button>();
            }

            btnValidate?.onClick.AddListener(OnValidateClicked);
        }

        private void OnEnable()
        {
            if (PlayerPrefs.GetInt(KEY_FIRST_LAUNCH, 0) == 1) return;

            if (inputNickname != null)
                inputNickname.text = PlayerPrefs.GetString(KEY_NAME, "");
            if (lblFeedback != null)
                lblFeedback.text = "";
        }

        private void OnValidateClicked()
        {
            if (inputNickname == null) return;
            string name = inputNickname.text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                if (lblFeedback != null) lblFeedback.text = LocalizationManager.Get("ui_nickname_empty");
                return;
            }

            if (name.Length > MAX_LENGTH)
                name = name.Substring(0, MAX_LENGTH);

            PlayerPrefs.SetString(KEY_NAME, name);
            PlayerPrefs.SetInt(KEY_FIRST_LAUNCH, 1);
            PlayerPrefs.Save();

            transform.parent.gameObject.SetActive(false);
        }
    }
}
