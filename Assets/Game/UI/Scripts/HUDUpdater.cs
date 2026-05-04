using System.Linq;
using UnityEngine;
using TMPro;

namespace Astraleum
{
    public class HUDUpdater : MonoBehaviour
    {
        public static HUDUpdater Instance;

        [Header("HUD Joueur 1")]
        public TMP_Text p1HPTotal;
        public TMP_Text p1RankText;

        [Header("HUD Joueur 2")]
        public TMP_Text p2HPTotal;
        public TMP_Text p2RankText;

        private void Awake() => Instance = this;

        private void LateUpdate() => UpdateHUD();

        private void UpdateHUD()
        {
            if (BoardManager.Instance == null) return;
            UpdatePlayerHUD(0, p1HPTotal);
            UpdatePlayerHUD(1, p2HPTotal);
        }

        private void UpdatePlayerHUD(int playerID, TMP_Text hpText)
        {
            if (hpText == null) return;

            var cards = BoardManager.Instance.GetAliveCards(playerID);
            int totalHP = cards.Sum(c => c.currentHP);
            hpText.text = $"{totalHP} PV";

            // Rouge si PV totaux faibles
            hpText.color = totalHP < 200
                ? new Color(1f, 0.3f, 0.3f)
                : new Color(0.86f, 0.31f, 0.31f);
        }
    }
}