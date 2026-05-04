using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum.UI
{
    /// <summary>
    /// Gère Panel_Graphics dans les Settings.
    /// Structure attendue :
    ///   dropdownQuality      → TMP_Dropdown  (niveaux de qualité Unity)
    ///   dropdownResolution   → TMP_Dropdown  (résolutions disponibles)
    ///   toggleFullscreen     → Toggle        (plein écran)
    ///   toggleVSync          → Toggle        (synchronisation verticale)
    ///   toggleParticles      → Toggle        (effets de particules)
    /// </summary>
    public class GraphicsSettingsPanel : MonoBehaviour
    {
        // ── Clés PlayerPrefs ──────────────────────────────────────────
        private const string KEY_QUALITY     = "Gfx_Quality";
        private const string KEY_RESOLUTION  = "Gfx_Resolution";
        private const string KEY_FULLSCREEN  = "Gfx_Fullscreen";
        private const string KEY_VSYNC       = "Gfx_VSync";
        private const string KEY_PARTICLES   = "Gfx_Particles";

        // ── Références UI ─────────────────────────────────────────────
        [Header("Dropdowns")]
        public TMP_Dropdown dropdownQuality;
        public TMP_Dropdown dropdownResolution;

        [Header("Toggles")]
        public Toggle toggleFullscreen;
        public Toggle toggleVSync;
        public Toggle toggleParticles;

        // ── État interne ──────────────────────────────────────────────
        private Resolution[] availableResolutions;
        private int          selectedResolutionIndex;

        // ── Init ──────────────────────────────────────────────────────

        private void OnEnable()
        {
            BuildResolutionList();
            LoadAndApply();
            BindListeners();
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        private void BindListeners()
        {
            if (dropdownQuality    != null) dropdownQuality   .onValueChanged.AddListener(OnQualityChanged);
            if (dropdownResolution != null) dropdownResolution.onValueChanged.AddListener(OnResolutionChanged);
            if (toggleFullscreen   != null) toggleFullscreen  .onValueChanged.AddListener(OnFullscreenToggled);
            if (toggleVSync        != null) toggleVSync       .onValueChanged.AddListener(OnVSyncToggled);
            if (toggleParticles    != null) toggleParticles   .onValueChanged.AddListener(OnParticlesToggled);
        }

        private void UnbindListeners()
        {
            if (dropdownQuality    != null) dropdownQuality   .onValueChanged.RemoveListener(OnQualityChanged);
            if (dropdownResolution != null) dropdownResolution.onValueChanged.RemoveListener(OnResolutionChanged);
            if (toggleFullscreen   != null) toggleFullscreen  .onValueChanged.RemoveListener(OnFullscreenToggled);
            if (toggleVSync        != null) toggleVSync       .onValueChanged.RemoveListener(OnVSyncToggled);
            if (toggleParticles    != null) toggleParticles   .onValueChanged.RemoveListener(OnParticlesToggled);
        }

        // ── Construction des listes ───────────────────────────────────

        private void BuildResolutionList()
        {
            if (dropdownResolution == null) return;

            availableResolutions = Screen.resolutions;
            dropdownResolution.ClearOptions();

            var opts = new List<TMP_Dropdown.OptionData>();
            int currentIndex = 0;

            for (int i = 0; i < availableResolutions.Length; i++)
            {
                var r = availableResolutions[i];
                opts.Add(new TMP_Dropdown.OptionData($"{r.width} × {r.height}  {r.refreshRateRatio.value:F0} Hz"));

                if (r.width  == Screen.currentResolution.width &&
                    r.height == Screen.currentResolution.height)
                    currentIndex = i;
            }

            dropdownResolution.AddOptions(opts);
            selectedResolutionIndex = PlayerPrefs.GetInt(KEY_RESOLUTION, currentIndex);
            selectedResolutionIndex = Mathf.Clamp(selectedResolutionIndex, 0, availableResolutions.Length - 1);
            dropdownResolution.SetValueWithoutNotify(selectedResolutionIndex);
        }

        private void BuildQualityList()
        {
            if (dropdownQuality == null) return;

            dropdownQuality.ClearOptions();
            var opts = new List<TMP_Dropdown.OptionData>();
            foreach (var name in QualitySettings.names)
                opts.Add(new TMP_Dropdown.OptionData(name));
            dropdownQuality.AddOptions(opts);
        }

        // ── Chargement & application ──────────────────────────────────

        private void LoadAndApply()
        {
            // Qualité
            BuildQualityList();
            int quality = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
            quality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);
            if (dropdownQuality != null) dropdownQuality.SetValueWithoutNotify(quality);
            QualitySettings.SetQualityLevel(quality, true);

            // Plein écran
            bool fullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
            if (toggleFullscreen != null) toggleFullscreen.SetIsOnWithoutNotify(fullscreen);
            Screen.fullScreen = fullscreen;

            // VSync
            bool vsync = PlayerPrefs.GetInt(KEY_VSYNC, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            if (toggleVSync != null) toggleVSync.SetIsOnWithoutNotify(vsync);
            QualitySettings.vSyncCount = vsync ? 1 : 0;

            // Particules
            bool particles = PlayerPrefs.GetInt(KEY_PARTICLES, 1) == 1;
            if (toggleParticles != null) toggleParticles.SetIsOnWithoutNotify(particles);
            ApplyParticles(particles);

            // Résolution (déjà appliquée dans BuildResolutionList via SetValueWithoutNotify)
            if (availableResolutions != null && selectedResolutionIndex < availableResolutions.Length)
            {
                var r = availableResolutions[selectedResolutionIndex];
                Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            }
        }

        // ── Callbacks ─────────────────────────────────────────────────

        private void OnQualityChanged(int index)
        {
            QualitySettings.SetQualityLevel(index, true);
            if (toggleVSync != null)
                toggleVSync.SetIsOnWithoutNotify(QualitySettings.vSyncCount > 0);
            PlayerPrefs.SetInt(KEY_QUALITY, index);
            PlayerPrefs.Save();
        }

        private void OnResolutionChanged(int index)
        {
            if (availableResolutions == null || index >= availableResolutions.Length) return;
            selectedResolutionIndex = index;
            var r = availableResolutions[index];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            PlayerPrefs.SetInt(KEY_RESOLUTION, index);
            PlayerPrefs.Save();
        }

        private void OnFullscreenToggled(bool isOn)
        {
            Screen.fullScreen = isOn;
            PlayerPrefs.SetInt(KEY_FULLSCREEN, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnVSyncToggled(bool isOn)
        {
            QualitySettings.vSyncCount = isOn ? 1 : 0;
            PlayerPrefs.SetInt(KEY_VSYNC, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnParticlesToggled(bool isOn)
        {
            ApplyParticles(isOn);
            PlayerPrefs.SetInt(KEY_PARTICLES, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ── Effets de particules ──────────────────────────────────────

        /// <summary>
        /// Active ou désactive tous les ParticleSystems de la scène.
        /// En combat, BoardManager ou un ParticleManager peut s'abonner
        /// à l'événement statique OnParticlesSettingChanged à la place.
        /// </summary>
        public static event System.Action<bool> OnParticlesSettingChanged;

        private void ApplyParticles(bool isOn)
        {
            OnParticlesSettingChanged?.Invoke(isOn);
        }

        // ── Accès depuis d'autres scripts ─────────────────────────────

        /// <summary>Retourne true si les particules sont activées dans les settings.</summary>
        public static bool ParticlesEnabled =>
            PlayerPrefs.GetInt(KEY_PARTICLES, 1) == 1;
    }
}
