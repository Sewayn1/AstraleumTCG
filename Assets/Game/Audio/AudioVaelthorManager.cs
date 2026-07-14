using System.Collections;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Musique de combat de Vaelthor — une piste par phase (2, contrairement aux 3 d'AudioBossManager).
    /// S'abonne à VaelthorPhaseController.OnPhaseChanged. Script parallèle à AudioBossManager —
    /// ne le modifie jamais (câblé en dur pour 3 phases et le singleton BossPhaseController).
    /// </summary>
    public class AudioVaelthorManager : MonoBehaviour
    {
        public static AudioVaelthorManager Instance;

        [Header("Pistes par phase")]
        public AudioClip phase1Music;
        public AudioClip phase2Music;

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

        private void Start()
        {
            // Abonnement fait en Start (garanti après TOUS les Awake de la scène), pas en
            // OnEnable : Awake+OnEnable s'enchaînent par objet dans un ordre non garanti entre
            // scripts différents. Repro Play Mode 2026-07-14 : OnEnable() de ce script pouvait
            // s'exécuter avant VaelthorPhaseController.Awake(), Instance était encore null, et le
            // guard silencieux empêchait l'abonnement pour toute la durée du combat — la Phase 2
            // gardait la musique de la Phase 1 car HandlePhaseChanged n'était jamais appelé.
            if (VaelthorPhaseController.Instance != null)
                VaelthorPhaseController.Instance.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (VaelthorPhaseController.Instance != null)
                VaelthorPhaseController.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }

        /// <summary>Démarre la musique de la Phase 1 — à appeler au début du combat Vaelthor.</summary>
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

            audioSource.volume = baseVolume;
            PlayImmediate(phase2Music);
            fadeRoutine = null;
        }

        private void PlayImmediate(AudioClip clip)
        {
            if (clip == null) return;
            audioSource.clip = clip;
            audioSource.Play();
        }

        /// <summary>Fondu de sortie jusqu'au silence puis arrêt complet, sans bascule — utilisé pour la séquence de défaite.</summary>
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
