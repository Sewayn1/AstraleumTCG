using UnityEngine;
using UnityEngine.Audio;

namespace Astraleum.UI
{
    /// <summary>
    /// Applique tous les paramètres sauvegardés (audio + graphiques) au démarrage.
    /// À placer sur un GameObject persistant dans la première scène (ex. MainMenu).
    /// DontDestroyOnLoad : actif dans toutes les scènes.
    /// </summary>
    public class SettingsInitializer : MonoBehaviour
    {
        public static SettingsInitializer Instance;

        [Header("AudioMixer")]
        [Tooltip("Le même AudioMixer que celui assigné dans AudioSettingsPanel.")]
        public AudioMixer audioMixer;

        [Header("Paramètres exposés — noms identiques à AudioSettingsPanel")]
        public string paramMaster    = "MasterVolume";
        public string paramMusic     = "MusicVolume";
        public string paramSFX       = "SFXVolume";
        public string paramMenuMusic = "MenuMusicVolume";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Start() et non Awake() : l'AudioMixer doit être initialisé avant SetFloat
            ApplyAllSettings();
        }

        public void ApplyAllSettings()
        {
            ApplyAudio();
            ApplyGraphics();
        }

        // ── Audio ─────────────────────────────────────────────────────

        private void ApplyAudio()
        {
            if (audioMixer == null) return;

            bool musicOn = PlayerPrefs.GetInt("Audio_MusicEnabled", 1) == 1;

            SetMixerVolume(paramMaster,    PlayerPrefs.GetFloat("Audio_Master",    80f));
            SetMixerVolume(paramSFX,       PlayerPrefs.GetFloat("Audio_SFX",       80f));

            if (musicOn)
            {
                SetMixerVolume(paramMusic,     PlayerPrefs.GetFloat("Audio_Music",     80f));
                SetMixerVolume(paramMenuMusic, PlayerPrefs.GetFloat("Audio_MenuMusic", 80f));
            }
            else
            {
                audioMixer.SetFloat(paramMusic,     -80f);
                audioMixer.SetFloat(paramMenuMusic, -80f);
            }
        }

        private void SetMixerVolume(string param, float sliderValue)
        {
            float db = sliderValue > 0.01f
                ? Mathf.Log10(sliderValue / 100f) * 20f
                : -80f;
            audioMixer.SetFloat(param, db);
        }

        // ── Graphiques ────────────────────────────────────────────────

        private void ApplyGraphics()
        {
            // Qualité
            int quality = PlayerPrefs.GetInt("Gfx_Quality", QualitySettings.GetQualityLevel());
            quality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(quality, true);

            // VSync
            bool vsync = PlayerPrefs.GetInt("Gfx_VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            QualitySettings.vSyncCount = vsync ? 1 : 0;

            // Plein écran
            bool fullscreen = PlayerPrefs.GetInt("Gfx_Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            Screen.fullScreen = fullscreen;

            // Résolution
            var resolutions = Screen.resolutions;
            int resIdx = PlayerPrefs.GetInt("Gfx_Resolution", -1);
            if (resIdx >= 0 && resIdx < resolutions.Length)
            {
                var r = resolutions[resIdx];
                Screen.SetResolution(r.width, r.height, fullscreen);
            }
        }
    }
}
