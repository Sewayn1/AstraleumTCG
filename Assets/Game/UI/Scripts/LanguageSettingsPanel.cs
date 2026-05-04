using TMPro;
using UnityEngine;

namespace Astraleum.UI
{
    /// <summary>
    /// Sélecteur de langue dans le panel Settings.
    /// Attacher sur le GameObject contenant le TMP_Dropdown de langue.
    ///
    /// Structure attendue dans la scène :
    ///   GameObject (LanguageSettingsPanel)
    ///   └── dropdownLanguage  (TMP_Dropdown)  ← assigner dans l'inspecteur
    ///
    /// Le dropdown affiche les langues dans l'ordre de l'enum Language (FR, EN).
    /// Compatible avec Weblate : ajouter une entrée ici + un fichier JSON suffit.
    /// </summary>
    public class LanguageSettingsPanel : MonoBehaviour
    {
        [Header("Références")]
        [Tooltip("TMP_Dropdown listant les langues disponibles.")]
        public TMP_Dropdown dropdownLanguage;

        // Libellés affichés dans le dropdown, dans l'ordre de l'enum Language
        private static readonly string[] LanguageLabels =
        {
            "Français",   // Language.FR
            "English",    // Language.EN
        };

        private void OnEnable()
        {
            if (dropdownLanguage == null) return;

            dropdownLanguage.ClearOptions();
            var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
            foreach (string label in LanguageLabels)
                options.Add(new TMP_Dropdown.OptionData(label));

            dropdownLanguage.AddOptions(options);
            dropdownLanguage.value = (int)LocalizationManager.CurrentLanguage;

            dropdownLanguage.onValueChanged.RemoveAllListeners();
            dropdownLanguage.onValueChanged.AddListener(OnLanguageSelected);
        }

        private void OnDisable()
        {
            if (dropdownLanguage != null)
                dropdownLanguage.onValueChanged.RemoveAllListeners();
        }

        private void OnLanguageSelected(int index)
        {
            LocalizationManager.SetLanguage((LocalizationManager.Language)index);
        }
    }
}
