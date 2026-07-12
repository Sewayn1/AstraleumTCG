using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Astraleum
{
    /// <summary>
    /// Panel affiché pendant les 5 secondes avant le lancement du combat.
    /// Piloté depuis LobbyUI. Sauvegarder ce GO comme INACTIF dans la scène.
    /// </summary>
    public class LaunchPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button   btnCancel;

        private Coroutine    _countdownRoutine;
        private bool         _initialized;
        private bool         _cancelled;
        private System.Action _onCancel;

        public void Show(System.Action onCancel = null)
        {
            _onCancel = onCancel;

            if (!_initialized)
            {
                btnCancel?.onClick.AddListener(() => _onCancel?.Invoke());
                _initialized = true;
            }

            _cancelled = false;
            gameObject.SetActive(true);

            if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);
            _countdownRoutine = StartCoroutine(CountdownRoutine());
        }

        public void Hide()
        {
            _cancelled = true;
            if (_countdownRoutine != null)
            {
                StopCoroutine(_countdownRoutine);
                _countdownRoutine = null;
            }
            gameObject.SetActive(false);
        }

        private IEnumerator CountdownRoutine()
        {
            float remaining = 5f;
            while (remaining > 0f)
            {
                if (countText != null)
                    countText.text = Mathf.CeilToInt(remaining).ToString();
                yield return null;
                remaining -= Time.deltaTime;
            }
            if (countText != null) countText.text = "0";
            _countdownRoutine = null;

            if (_cancelled) yield break;
            SceneManager.LoadScene("Combat");
        }
    }
}
