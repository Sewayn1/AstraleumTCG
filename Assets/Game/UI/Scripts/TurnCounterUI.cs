using TMPro;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Affiche le nombre de tours joués depuis le début du combat (Canvas/TurnIndicator/TurnCount).
    /// Alimenté par TurnManager (local/IA) et NetworkGameController (SignalR).
    /// </summary>
    public class TurnCounterUI : MonoBehaviour
    {
        public static TurnCounterUI Instance;

        public TMP_Text turnCountText;

        public int CurrentTurn { get; private set; } = 0;

        private void Awake()
        {
            Instance = this;

            if (turnCountText == null)
                turnCountText = transform.Find("TurnCount")?.GetComponent<TMP_Text>();
        }

        private void OnEnable()  => LocalizationManager.OnLanguageChanged += UpdateText;
        private void OnDisable() => LocalizationManager.OnLanguageChanged -= UpdateText;

        public void SetTurn(int turn)
        {
            CurrentTurn = turn;
            UpdateText();
        }

        public void IncrementTurn()
        {
            CurrentTurn++;
            UpdateText();
        }

        private void UpdateText()
        {
            if (turnCountText == null || CurrentTurn <= 0) return;
            turnCountText.text = LocalizationManager.Get("combat_turn_count", CurrentTurn);
        }
    }
}
