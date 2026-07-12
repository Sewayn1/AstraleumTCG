using UnityEngine;
using TMPro;

namespace Astraleum
{
    /// <summary>
    /// Panel bloquant affiché quand une nouvelle version du jeu est disponible.
    /// Le joueur ne peut pas continuer : seul le bouton "Fermer le jeu" est disponible.
    /// Le GO doit être ACTIF dans la scène (Awake le désactive ensuite).
    /// </summary>
    public class PanelUpdateAvailable : MonoBehaviour
    {
        public static PanelUpdateAvailable Instance;

        [Header("Textes")]
        [Tooltip("Affiche la version disponible, ex. 'Version 0.0.5 disponible'")]
        public TMP_Text versionText;
        [Tooltip("Message explicatif sous le titre")]
        public TMP_Text messageText;

        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(false);
        }

        /// <summary>Affiche le panel et verrouille la navigation.</summary>
        public void Show(string newVersion)
        {
            if (versionText != null)
                versionText.text = LocalizationManager.Get("ui_update_title", newVersion);

            if (messageText != null)
                messageText.text = LocalizationManager.Get("ui_update_desc");

            gameObject.SetActive(true);
        }

        /// <summary>Bouton "Fermer le jeu" — câbler dans l'inspecteur.</summary>
        public void CloseGame()
        {
            Application.Quit();
        }
    }
}
