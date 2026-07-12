using System.Collections;
using UnityEngine;
namespace IndieImpulseAssets
{
    public class SpriteFader : MonoBehaviour
    {
        public float delay = 1.0f; // Delay before the fade starts
        public float fadeDuration = 2.0f; // Duration of the fade
        private SpriteRenderer spriteRenderer;
        private Coroutine fadeCoroutine;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            // Ensure the sprite starts fully opaque when the GameObject is enabled
            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;

            // Start the fading process
            fadeCoroutine = StartCoroutine(FadeOut());
        }

        private void OnDisable()
        {
            // Stop the coroutine if the GameObject is disabled to avoid any running coroutines
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
        }

        private IEnumerator FadeOut()
        {
            // Wait for the initial delay
            yield return new WaitForSeconds(delay);

            float elapsedTime = 0f;
            Color color = spriteRenderer.color;

            while (elapsedTime < fadeDuration)
            {
                // Calculate the new alpha value
                color.a = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
                spriteRenderer.color = color;

                // Increment the elapsed time
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Ensure the sprite is fully transparent at the end of the fade
            color.a = 0f;
            spriteRenderer.color = color;
        }
    }
}