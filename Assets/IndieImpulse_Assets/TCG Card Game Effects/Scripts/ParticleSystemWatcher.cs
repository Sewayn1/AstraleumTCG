using UnityEngine;
namespace IndieImpulseAssets
{
    public class ParticleSystemWatcher : MonoBehaviour
    {
        public ParticleSystem particleSystem; // Reference to the particle system
        public GameObject objectToEnable;     // Reference to the game object to enable

        void Start()
        {
            if (particleSystem == null)
            {
                particleSystem = GetComponent<ParticleSystem>();
            }

            if (particleSystem == null)
            {
                Debug.LogError("No ParticleSystem found on " + gameObject.name);
            }
        }
        private void OnDisable()
        {
            objectToEnable.SetActive(false);
        }
        void Update()
        {
            if (particleSystem != null && !particleSystem.IsAlive())
            {
                objectToEnable.SetActive(true);
                //enabled = false; // Optionally disable this script once the object is enabled
            }
        }
    }
}