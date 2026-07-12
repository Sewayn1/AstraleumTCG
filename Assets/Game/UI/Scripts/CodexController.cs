using UnityEngine;
using UnityEngine.UI;

namespace Astraleum.UI
{
    public class CodexController : MonoBehaviour
    {
        [System.Serializable]
        public struct CodexEntry
        {
            public Button     button;
            public GameObject panel;
        }

        [SerializeField] private CodexEntry[] entries;

        private int _currentIndex = -1;

        private void Awake()
        {
            for (int i = 0; i < entries.Length; i++)
            {
                int idx = i;
                entries[i].button?.onClick.AddListener(() => OpenPanel(idx));
                if (entries[i].panel != null)
                    entries[i].panel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            OpenPanel(_currentIndex >= 0 ? _currentIndex : 0);
        }

        public void OpenPanel(int index)
        {
            if (index < 0 || index >= entries.Length) return;

            if (_currentIndex >= 0 && _currentIndex != index && entries[_currentIndex].panel != null)
                entries[_currentIndex].panel.SetActive(false);

            _currentIndex = index;

            if (entries[index].panel != null)
                entries[index].panel.SetActive(true);
        }
    }
}
