using System.Collections;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Musique de combat du Boss — une piste par phase. À un changement de phase : fondu de
    /// sortie (3s par défaut) sur la piste en cours, puis bascule sans fondu sur la piste de la
    /// nouvelle phase. S'abonne à BossPhaseController.OnPhaseChanged.
    /// </summary>
    public class AudioBossManager : MonoBehaviour
    {
        public static AudioBossManager Instance;

        [Header("Pistes par phase")]
        public AudioClip phase1Music;
        public AudioClip phase2Music;
        public AudioClip phase3Music;

        [Tooltip("Durée du fondu de sortie avant de couper la musique de la phase précédente (secondes).")]
        public float fadeOutDuration = 2f;

        private AudioSource audioSource;
        private float baseVolume;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            Instance = this;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            baseVolume = audioSource.volume;
        }

        private void OnEnable()
        {
            if (BossPhaseController.Instance != null)
                BossPhaseController.Instance.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (BossPhaseController.Instance != null)
                BossPhaseController.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }

        /// <summary>Démarre la musique de la Phase 1 — à appeler au début du combat Boss.</summary>
        public void StartCombatMusic()
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            audioSource.volume = baseVolume;
            PlayImmediate(phase1Music);
        }

        private void HandlePhaseChanged(int newPhase)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeOutThenSwitch(newPhase));
        }

        private IEnumerator FadeOutThenSwitch(int newPhase)
        {
            float t = 0f;
            float startVolume = audioSource.volume;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeOutDuration);
                yield return null;
            }
            audioSource.volume = 0f;
            audioSource.Stop();

            AudioClip next = newPhase == 2 ? phase2Music : phase3Music;
            audioSource.volume = baseVolume;
            PlayImmediate(next);
            fadeRoutine = null;
        }

        private void PlayImmediate(AudioClip clip)
        {
            if (clip == null) return;
            audioSource.clip = clip;
            audioSource.Play();
        }

        /// <summary>Fondu de sortie jusqu'au silence puis arrêt complet, sans bascule vers une piste suivante — utilisé pour la séquence de défaite du Boss.</summary>
        public IEnumerator FadeOutAndStop(float duration)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            float t = 0f;
            float startVolume = audioSource.volume;
            while (t < duration)
            {
                t += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
                yield return null;
            }
            audioSource.volume = 0f;
            audioSource.Stop();
        }
    }
}
