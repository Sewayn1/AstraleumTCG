using UnityEngine;

namespace Astraleum
{
    public class TurnAudioManager : MonoBehaviour
    {
        public static TurnAudioManager Instance;

        [Header("Sons de début de tour")]
        public AudioClip turnStartP1;
        public AudioClip turnStartP2;

        private AudioSource audioSource;

        private void Awake()
        {
            Instance     = this;
            audioSource  = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.volume      = 0.2f;
        }

        public void PlayTurnStart(int playerID)
        {
            var clip = playerID == 0 ? turnStartP1 : turnStartP2;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }
    }
}