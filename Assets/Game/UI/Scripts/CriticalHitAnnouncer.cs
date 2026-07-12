using System.Collections;
using UnityEngine;
using TMPro;

namespace Astraleum
{
    public class CriticalHitAnnouncer : MonoBehaviour
    {
        public static CriticalHitAnnouncer Instance;

        [Header("Références")]
        public TMP_Text critText;

        private Coroutine _hideCoroutine;

        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(false);
        }

        public void Show()
        {
            if (critText == null) return;
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            gameObject.SetActive(true);
            _hideCoroutine = StartCoroutine(HideAfter(0.5f));
        }

        private IEnumerator HideAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
            _hideCoroutine = null;
        }
    }
}
