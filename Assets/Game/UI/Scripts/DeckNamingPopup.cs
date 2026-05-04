using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum.UI
{
    /// <summary>
    /// Popup de nommage/renommage d'un deck.
    /// À attacher sur Panel_DeckNaming.
    /// Structure attendue :
    ///   Panel_DeckNaming
    ///   ├── DeckNameInput  (TMP_InputField)
    ///   ├── Btn_Confirm    → Confirm()
    ///   └── Btn_Cancel     → Cancel()
    /// </summary>
    public class DeckNamingPopup : MonoBehaviour
    {
        public static DeckNamingPopup Instance;

        [Header("Références")]
        public TMP_InputField nameInput;
        public Button         confirmButton;
        public TMP_Text       feedbackText;

        private Action<string> onConfirmed;

        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(false);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
        }

        /// <summary>
        /// Affiche le popup.
        /// currentName : nom pré-rempli (vide pour un nouveau deck).
        /// callback : appelé avec le nom validé.
        /// </summary>
        public void Show(string currentName, Action<string> callback)
        {
            onConfirmed = callback;

            if (nameInput != null)
            {
                nameInput.text = currentName ?? "";
                nameInput.onSubmit.RemoveAllListeners();
                nameInput.onSubmit.AddListener(_ => Confirm());
            }

            if (feedbackText != null)
                feedbackText.text = "";

            gameObject.SetActive(true);

            // Focus automatique sur le champ
            if (nameInput != null)
            {
                nameInput.Select();
                nameInput.ActivateInputField();
            }
        }

        /// <summary>Appelé par Btn_Confirm ou touche Entrée.</summary>
        public void Confirm()
        {
            string name = nameInput != null ? nameInput.text.Trim() : "";

            if (string.IsNullOrEmpty(name))
            {
                if (feedbackText != null)
                {
                    feedbackText.text  = Astraleum.LocalizationManager.Get("deck_name_prompt");
                    feedbackText.color = new Color(1f, 0.4f, 0.4f);
                }
                return;
            }

            gameObject.SetActive(false);
            onConfirmed?.Invoke(name);
            onConfirmed = null;
        }

        /// <summary>Appelé par Btn_Cancel.</summary>
        public void Cancel()
        {
            gameObject.SetActive(false);
            onConfirmed = null;
        }
    }
}
