using UnityEngine;
using UnityEngine.UI;

namespace Astraleum.UI
{
    public class CombatSettingsPanel : MonoBehaviour
    {
        [Header("Références (auto-trouvées si laissées vides)")]
        public GameObject panelSettings;
        public Button     btnSettings;

        private void Start()
        {
            // Auto-find via le Canvas pour traverser les GO inactifs
            if (panelSettings == null)
                panelSettings = FindInactive("Panel_Settings");

            if (btnSettings == null)
            {
                var go = FindInactive("Settings");
                if (go != null) btnSettings = go.GetComponent<Button>();
            }

            if (btnSettings != null)
                btnSettings.onClick.AddListener(Toggle);

            if (panelSettings != null)
            {
                // Bouton Close à l'intérieur du panel
                var closeT = panelSettings.transform.Find("Close");
                if (closeT != null)
                {
                    var btn = closeT.GetComponent<Button>();
                    if (btn != null) btn.onClick.AddListener(Close);
                }

                panelSettings.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (panelSettings == null) return;
            panelSettings.SetActive(!panelSettings.activeSelf);
        }

        public void Open()  => panelSettings?.SetActive(true);
        public void Close() => panelSettings?.SetActive(false);

        // Cherche dans toute la hiérarchie du Canvas, y compris les GO inactifs
        private GameObject FindInactive(string goName)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return null;

            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                if (t.name == goName) return t.gameObject;

            return null;
        }
    }
}
