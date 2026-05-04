using UnityEngine;
using TMPro;

namespace Astraleum
{
    public class EndGameHandler : MonoBehaviour
    {
        public static EndGameHandler Instance;

        [Header("Panel EndGame")]
        public GameObject panelEndGame;
        public TMP_Text resultTitle;
        public TMP_Text resultSubtitle;
        public TMP_Text prValue;
        public TMP_Text eclatsValue;

        private bool gameEnded = false;
        private bool gameStarted = false;

        private void Awake() => Instance = this;

        public void OnGameStarted() => gameStarted = true;

        private void LateUpdate()
        {
            if (gameEnded || !gameStarted || BoardManager.Instance == null) return;

            var p1Cards = BoardManager.Instance.GetAliveCards(0);
            var p2Cards = BoardManager.Instance.GetAliveCards(1);

            if (p1Cards.Count == 0 && p2Cards.Count == 0) return;

            bool p1Lost = p1Cards.Count == 0;
            bool p2Lost = p2Cards.Count == 0;

            if (p1Lost || p2Lost)
                ShowEndGame(p1Lost ? 1 : 0);
        }

        // loserID = joueur qui abandonne → le gagnant est l'adversaire
        public void ShowGiveUpResult(int loserID)
        {
            ShowEndGame(1 - loserID);
        }

        public void ShowEndGame(int winnerID)
        {
            if (gameEnded) return;
            gameEnded = true;

            // Perspective : le joueur local a-t-il gagné ?
            int localID = NetworkBridge.IsActive ? NetworkBridge.LocalPlayerID : 0;
            bool isVictory = winnerID == localID;

            if (resultTitle != null)
            {
                resultTitle.text = isVictory
                    ? LocalizationManager.Get("endgame_victory").ToUpper()
                    : LocalizationManager.Get("endgame_defeat").ToUpper();
                resultTitle.color = isVictory
                    ? new Color(0.39f, 0.86f, 0.59f)
                    : new Color(0.86f, 0.31f, 0.31f);
            }

            if (resultSubtitle != null)
                resultSubtitle.text = isVictory
                    ? LocalizationManager.Get("endgame_subtitle_victory")
                    : LocalizationManager.Get("endgame_subtitle_defeat");

            int prGain = isVictory ? 20 : -10;
            int eclats = isVictory ? 80 : 30;

            if (prValue != null)
            {
                prValue.text = prGain > 0 ? $"+{prGain} PR" : $"{prGain} PR";
                prValue.color = prGain > 0
                    ? new Color(0.39f, 0.86f, 0.59f)
                    : new Color(0.86f, 0.31f, 0.31f);
            }

            if (eclatsValue != null)
                eclatsValue.text = LocalizationManager.Get("endgame_eclats", eclats);

            if (panelEndGame != null)
                panelEndGame.SetActive(true);
        }

        // Bouton Btn_BackToMainMenu
        public void BackToMainMenu() => GameManager.Instance?.ReturnToMainMenu();
    }
}