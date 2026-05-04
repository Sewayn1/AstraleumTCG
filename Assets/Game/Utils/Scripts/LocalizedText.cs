using TMPro;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Composant à placer sur un GameObject portant un TMP_Text.
    /// Met à jour automatiquement le texte quand la langue change.
    /// Usage : renseigner la clé dans l'inspecteur, ex. "ui_btn_end_turn".
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Clé dans fr.json / en.json. Ex : ui_btn_end_turn")]
        [SerializeField] private string key;

        private TMP_Text _label;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_label == null || string.IsNullOrEmpty(key)) return;
            _label.text = LocalizationManager.Get(key);
        }

        /// <summary>Change la clé dynamiquement depuis le code.</summary>
        public void SetKey(string newKey)
        {
            key = newKey;
            Refresh();
        }
    }
}
