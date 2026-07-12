using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace IndieImpulseAssets
{
    public class DemoSlide : MonoBehaviour
    {
        public GameObject[] Effects;
        private int index = 0;


        void Awake()
        {
            EnsureEventSystem();
        }

        void EnsureEventSystem()
        {
#if UNITY_2022_2_OR_NEWER
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
#else
            var eventSystem = Object.FindObjectOfType<EventSystem>();
#endif


            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                eventSystem = go.AddComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER

            var oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
                DestroyImmediate(oldModule);

            var inputSystemType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem"
            );

            if (inputSystemType != null &&
                eventSystem.GetComponent(inputSystemType) == null)
            {
                eventSystem.gameObject.AddComponent(inputSystemType);
            }

#else

            var newModule = eventSystem.GetComponent("InputSystemUIInputModule");
            if (newModule != null)
                DestroyImmediate(newModule);

            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();

#endif
        }


        void Start()
        {
            Application.targetFrameRate = 60;
            index = Effects.Length - 1;
            // Initially, deactivate all effects except the first one
            for (int i = 1; i < Effects.Length; i++)
            {
                Effects[i].SetActive(false);
            }
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

            if (spacePressed)
            {
                ChangeEffect();
            }
#else
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ChangeEffect();
            }

#endif
        }

        void ChangeEffect()
        {
            // Deactivate the current effect
            Effects[index].SetActive(false);

            // Update the index for the next effect
            if (index == Effects.Length - 1)
            {
                index = 0;
            }
            else
            {
                index++;
            }

            // Activate the new effect
            Effects[index].SetActive(true);
        }
    }
}
