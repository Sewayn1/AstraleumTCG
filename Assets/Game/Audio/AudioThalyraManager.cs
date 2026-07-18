using System.Collections;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Musique de combat de Thalyra. Les pistes Marée Haute/Basse sont le MÊME morceau à deux
    /// intensités différentes (fournies par l'utilisateur) — la bascule doit donc rester au même
    /// instant musical, jamais redémarrer depuis 0. Solution retenue : les DEUX AudioSource jouent
    /// en permanence, démarrées à la même frame et jamais stoppées/redémarrées tant que le combat
    /// dure — seul leur volume est crossfadé à chaque changement de marée. Comme aucune des deux
    /// n'est jamais réellement arrêtée, elles restent par construction TOUJOURS synchronisées à
    /// l'instant musical exact, sans avoir besoin de mémoriser/reseeker un timestamp (approche plus
    /// robuste qu'un stop+seek+restart : pas de latence de lancement, pas de dérive, pas de piège
    /// de rebouclage). Script parallèle à AudioBossManager/AudioVaelthorManager — ne les modifie
    /// jamais.
    /// </summary>
    public class AudioThalyraManager : MonoBehaviour
    {
        public static AudioThalyraManager Instance;

        [Header("Pistes — même morceau, deux intensités")]
        [Tooltip("Piste jouée à pleine intensité pendant les Marées Hautes.")]
        public AudioClip hauteMusic;
        [Tooltip("Piste jouée à pleine intensité pendant les Marées Basses — même morceau que hauteMusic, synchronisée en permanence, seul le volume bascule.")]
        public AudioClip basseMusic;

        [Tooltip("Durée du fondu croisé entre les deux intensités, à chaque changement de marée (secondes).")]
        public float crossfadeDuration = 2f;

        private AudioSource hauteSource;
        private AudioSource basseSource;
        private float baseVolume = 1f;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            Instance = this;

            var sources = GetComponents<AudioSource>();
            hauteSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            basseSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

            hauteSource.loop = true;
            hauteSource.playOnAwake = false;
            basseSource.loop = true;
            basseSource.playOnAwake = false;

            // Volume de référence pris sur hauteSource — les deux AudioSource doivent être réglées
            // au même volume de base dans l'Inspector (même groupe de mixage "Music").
            baseVolume = hauteSource.volume;
        }

        private void Start()
        {
            // Abonnement en Start (garanti après TOUS les Awake de la scène), pas en OnEnable —
            // même correctif que AudioVaelthorManager (voir son commentaire) : évite une course où
            // OnEnable() de ce script s'exécuterait avant ThalyraPhaseController.Awake(), Instance
            // encore null, abonnement silencieusement perdu pour toute la durée du combat.
            if (ThalyraPhaseController.Instance != null)
                ThalyraPhaseController.Instance.OnTideChanged += HandleTideChanged;
        }

        private void OnDisable()
        {
            if (ThalyraPhaseController.Instance != null)
                ThalyraPhaseController.Instance.OnTideChanged -= HandleTideChanged;
        }

        /// <summary>
        /// Démarre les deux pistes en parallèle, à la même frame — à appeler au début du combat
        /// Thalyra. Le combat commence toujours en Marée Haute (voir ThalyraPhaseController).
        /// </summary>
        public void StartCombatMusic()
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);

            hauteSource.clip = hauteMusic;
            basseSource.clip = basseMusic;

            hauteSource.volume = baseVolume;
            basseSource.volume = 0f;

            if (hauteMusic != null) hauteSource.Play();
            if (basseMusic != null) basseSource.Play();
        }

        private void HandleTideChanged(ThalyraTideState newState)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(CrossfadeTo(newState));
        }

        private IEnumerator CrossfadeTo(ThalyraTideState newState)
        {
            float targetHaute = newState == ThalyraTideState.Haute ? baseVolume : 0f;
            float targetBasse = newState == ThalyraTideState.Basse ? baseVolume : 0f;
            float startHaute = hauteSource.volume;
            float startBasse = basseSource.volume;

            float t = 0f;
            while (t < crossfadeDuration)
            {
                t += Time.deltaTime;
                float f = t / crossfadeDuration;
                hauteSource.volume = Mathf.Lerp(startHaute, targetHaute, f);
                basseSource.volume = Mathf.Lerp(startBasse, targetBasse, f);
                yield return null;
            }
            hauteSource.volume = targetHaute;
            basseSource.volume = targetBasse;
            fadeRoutine = null;
        }

        /// <summary>Fondu de sortie jusqu'au silence puis arrêt complet des deux pistes — utilisé pour la séquence de défaite.</summary>
        public IEnumerator FadeOutAndStop(float duration)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);

            float startHaute = hauteSource.volume;
            float startBasse = basseSource.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float f = t / duration;
                hauteSource.volume = Mathf.Lerp(startHaute, 0f, f);
                basseSource.volume = Mathf.Lerp(startBasse, 0f, f);
                yield return null;
            }
            hauteSource.volume = 0f;
            basseSource.volume = 0f;
            hauteSource.Stop();
            basseSource.Stop();
        }
    }
}
