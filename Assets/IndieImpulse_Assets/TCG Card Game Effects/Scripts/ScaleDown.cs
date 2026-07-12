using UnityEngine;

namespace IndieImpulseAssets
{
    public class ScaleDown : MonoBehaviour
    {
        public Vector3 targetScale = new Vector3(0.5f, 0.5f, 0.5f); // The target scale
        public float duration = 2.0f; // Duration over which to scale down

        private Vector3 initialScale;
        private float elapsedTime = 0f;
        private bool scaling = false;

        void OnEnable()
        {
            // Store the initial scale of the GameObject
            initialScale = transform.localScale;
            elapsedTime = 0f;
            scaling = true;
        }

        void Update()
        {
            if (scaling)
            {
                // Increment elapsed time by the time passed since last frame
                elapsedTime += Time.deltaTime;

                // Calculate the percentage of completion
                float t = elapsedTime / duration;

                // Interpolate the scale based on the percentage of completion
                transform.localScale = Vector3.Lerp(initialScale, targetScale, t);

                // Stop updating after the duration is reached
                if (elapsedTime >= duration)
                {
                    // Ensure the final scale is exactly the target scale
                    transform.localScale = targetScale;

                    // Reset for next enable
                    scaling = false;
                }
            }
        }

        void OnDisable()
        {
            // Reset scaling parameters when disabled
            transform.localScale = initialScale;
            elapsedTime = 0f;
            scaling = false;
        }
    }
}
