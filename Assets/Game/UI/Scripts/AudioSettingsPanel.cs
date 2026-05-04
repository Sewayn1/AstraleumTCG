using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

namespace Astraleum.UI
{
    /// <summary>
    /// Gère Panel_Audio dans les Settings.
    /// Nécessite un AudioMixer avec les paramètres exposés :
    ///   MasterVolume, MusicVolume, SFXVolume, MenuMusicVolume
    /// Assigner le AudioMixer dans l'inspecteur.
    /// </summary>
    public class AudioSettingsPanel : MonoBehaviour
    {
        // ── Clés PlayerPrefs ──────────────────────────────────────────
        private const string KEY_MASTER     = "Audio_Master";
        private const string KEY_MUSIC      = "Audio_Music";
        private const string KEY_SFX        = "Audio_SFX";
        private const string KEY_MENUMUZIC  = "Audio_MenuMusic";
        private const string KEY_MUSIC_ON   = "Audio_MusicEnabled";

        // ── Références AudioMixer ─────────────────────────────────────
        [Header("AudioMixer")]
        [Tooltip("AudioMixer principal du projet.")]
        public AudioMixer audioMixer;

        [Tooltip("Nom du paramètre exposé pour le volume général.")]
        public string paramMaster    = "MasterVolume";
        [Tooltip("Nom du paramètre exposé pour la musique.")]
        public string paramMusic     = "MusicVolume";
        [Tooltip("Nom du paramètre exposé pour les effets sonores.")]
        public string paramSFX       = "SFXVolume";
        [Tooltip("Nom du paramètre exposé pour la musique du menu.")]
        public string paramMenuMusic = "MenuMusicVolume";

        // ── Sliders ───────────────────────────────────────────────────
        [Header("Sliders (0 – 100)")]
        public Slider sliderMaster;
        public Slider sliderMusic;
        public Slider sliderSFX;
        public Slider sliderMenuMusic;

        // ── Labels valeur (optionnels) ────────────────────────────────
        [Header("Labels valeur — optionnels")]
        public TMP_Text labelMaster;
        public TMP_Text labelMusic;
        public TMP_Text labelSFX;
        public TMP_Text labelMenuMusic;

        // ── Toggle musique ────────────────────────────────────────────
        [Header("Toggle")]
        [Tooltip("Case à cocher pour activer/désactiver la musique.")]
        public Toggle toggleMusic;

        // ── Init ──────────────────────────────────────────────────────

        private void OnEnable()
        {
            LoadAndApply();
            BindListeners();
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        private void BindListeners()
        {
            if (sliderMaster    != null) sliderMaster   .onValueChanged.AddListener(OnMasterChanged);
            if (sliderMusic     != null) sliderMusic    .onValueChanged.AddListener(OnMusicChanged);
            if (sliderSFX       != null) sliderSFX      .onValueChanged.AddListener(OnSFXChanged);
            if (sliderMenuMusic != null) sliderMenuMusic.onValueChanged.AddListener(OnMenuMusicChanged);
            if (toggleMusic     != null) toggleMusic    .onValueChanged.AddListener(OnMusicToggled);
        }

        private void UnbindListeners()
        {
            if (sliderMaster    != null) sliderMaster   .onValueChanged.RemoveListener(OnMasterChanged);
            if (sliderMusic     != null) sliderMusic    .onValueChanged.RemoveListener(OnMusicChanged);
            if (sliderSFX       != null) sliderSFX      .onValueChanged.RemoveListener(OnSFXChanged);
            if (sliderMenuMusic != null) sliderMenuMusic.onValueChanged.RemoveListener(OnMenuMusicChanged);
            if (toggleMusic     != null) toggleMusic    .onValueChanged.RemoveListener(OnMusicToggled);
        }

        // ── Chargement ────────────────────────────────────────────────

        private void LoadAndApply()
        {
            SetSlider(sliderMaster,    labelMaster,    PlayerPrefs.GetFloat(KEY_MASTER,    80f));
            SetSlider(sliderMusic,     labelMusic,     PlayerPrefs.GetFloat(KEY_MUSIC,     80f));
            SetSlider(sliderSFX,       labelSFX,       PlayerPrefs.GetFloat(KEY_SFX,       80f));
            SetSlider(sliderMenuMusic, labelMenuMusic, PlayerPrefs.GetFloat(KEY_MENUMUZIC, 80f));

            bool musicOn = PlayerPrefs.GetInt(KEY_MUSIC_ON, 1) == 1;
            if (toggleMusic != null) toggleMusic.isOn = musicOn;

            // Appliquer immédiatement au mixer
            ApplyToMixer(paramMaster,    PlayerPrefs.GetFloat(KEY_MASTER,    80f));
            ApplyToMixer(paramMusic,     PlayerPrefs.GetFloat(KEY_MUSIC,     80f));
            ApplyToMixer(paramSFX,       PlayerPrefs.GetFloat(KEY_SFX,       80f));
            ApplyToMixer(paramMenuMusic, PlayerPrefs.GetFloat(KEY_MENUMUZIC, 80f));
            ApplyMusicToggle(musicOn);
        }

        private void SetSlider(Slider slider, TMP_Text label, float value)
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 100f;
                slider.value    = value;
            }
            UpdateLabel(label, value);
        }

        // ── Callbacks sliders ─────────────────────────────────────────

        private void OnMasterChanged(float value)
        {
            UpdateLabel(labelMaster, value);
            ApplyToMixer(paramMaster, value);
            PlayerPrefs.SetFloat(KEY_MASTER, value);
            PlayerPrefs.Save();
        }

        private void OnMusicChanged(float value)
        {
            UpdateLabel(labelMusic, value);
            if (toggleMusic == null || toggleMusic.isOn)
                ApplyToMixer(paramMusic, value);
            PlayerPrefs.SetFloat(KEY_MUSIC, value);
            PlayerPrefs.Save();
        }

        private void OnSFXChanged(float value)
        {
            UpdateLabel(labelSFX, value);
            ApplyToMixer(paramSFX, value);
            PlayerPrefs.SetFloat(KEY_SFX, value);
            PlayerPrefs.Save();
        }

        private void OnMenuMusicChanged(float value)
        {
            UpdateLabel(labelMenuMusic, value);
            if (toggleMusic == null || toggleMusic.isOn)
                ApplyToMixer(paramMenuMusic, value);
            PlayerPrefs.SetFloat(KEY_MENUMUZIC, value);
            PlayerPrefs.Save();
        }

        private void OnMusicToggled(bool isOn)
        {
            ApplyMusicToggle(isOn);
            PlayerPrefs.SetInt(KEY_MUSIC_ON, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ── Appliquer au mixer ────────────────────────────────────────

        private void ApplyToMixer(string param, float sliderValue)
        {
            if (audioMixer == null) return;
            // Conversion linéaire → dB (0 = -80 dB, 100 = 0 dB)
            float db = sliderValue > 0.01f
                ? Mathf.Log10(sliderValue / 100f) * 20f
                : -80f;
            audioMixer.SetFloat(param, db);
        }

        private void ApplyMusicToggle(bool isOn)
        {
            if (audioMixer == null) return;
            // Couper ou restaurer musique et musique menu
            if (isOn)
            {
                ApplyToMixer(paramMusic,     PlayerPrefs.GetFloat(KEY_MUSIC,     80f));
                ApplyToMixer(paramMenuMusic, PlayerPrefs.GetFloat(KEY_MENUMUZIC, 80f));
            }
            else
            {
                audioMixer.SetFloat(paramMusic,     -80f);
                audioMixer.SetFloat(paramMenuMusic, -80f);
            }
        }

        // ── Utilitaires ───────────────────────────────────────────────

        private void UpdateLabel(TMP_Text label, float value)
        {
            if (label != null)
                label.text = Mathf.RoundToInt(value).ToString();
        }
    }
}
