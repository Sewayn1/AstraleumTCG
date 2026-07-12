using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

namespace Astraleum.UI
{
    public class SoundMuteButton : MonoBehaviour
    {
        private const string KEY_MUSIC_ON   = "Audio_MusicEnabled";
        private const string KEY_MUSIC      = "Audio_Music";
        private const string KEY_MENUMUZIC  = "Audio_MenuMusic";

        [Header("AudioMixer")]
        public AudioMixer audioMixer;
        public string paramMusic     = "MusicVolume";
        public string paramMenuMusic = "MenuMusicVolume";

        [Header("Icônes")]
        [Tooltip("Sprite affiché quand la musique est ON.")]
        public Sprite iconMusicOn;
        [Tooltip("Sprite affiché quand la musique est OFF.")]
        public Sprite iconMusicOff;

        [Header("Image cible")]
        [Tooltip("Image du bouton dont le sprite change. Laissez vide pour utiliser Image sur ce GameObject.")]
        public Image buttonImage;

        private bool _musicOn;

        private void Start()
        {
            if (buttonImage == null)
                buttonImage = GetComponent<Image>();

            _musicOn = PlayerPrefs.GetInt(KEY_MUSIC_ON, 1) == 1;
            RefreshIcon();
        }

        public void OnToggle()
        {
            _musicOn = !_musicOn;
            PlayerPrefs.SetInt(KEY_MUSIC_ON, _musicOn ? 1 : 0);
            PlayerPrefs.Save();
            ApplyToMixer();
            RefreshIcon();
        }

        private void ApplyToMixer()
        {
            if (audioMixer == null) return;

            if (_musicOn)
            {
                SetMixerVolume(paramMusic,     PlayerPrefs.GetFloat(KEY_MUSIC,     80f));
                SetMixerVolume(paramMenuMusic, PlayerPrefs.GetFloat(KEY_MENUMUZIC, 80f));
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

        private void RefreshIcon()
        {
            if (buttonImage == null) return;
            buttonImage.sprite = _musicOn ? iconMusicOn : iconMusicOff;
        }
    }
}
