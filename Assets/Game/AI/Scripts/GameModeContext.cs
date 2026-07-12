using System.Collections.Generic;

namespace Astraleum.AI
{
    public enum GameMode
    {
        PvP,
        AISandbox,
        AIEasy,
        AIMedium,
        AIHard,
        Boss
    }

    /// <summary>
    /// Contexte statique du mode de partie en cours, survit au chargement de la scène Combat
    /// (calqué sur NetworkBridge). Renseigné par DeckSelectPanel avant SceneManager.LoadScene("Combat"),
    /// consommé par LocalAIGameController.
    /// </summary>
    public static class GameModeContext
    {
        public static GameMode Mode = GameMode.PvP;
        public static List<int> PlayerDeckNumbers;
        public static List<int> AIDeckNumbers;
        public static string AIDisplayName = "IA";

        // CardData de la Phase 1 du Boss (ex. Voragoth) — utilisé uniquement quand Mode == GameMode.Boss.
        public static CardData BossEncounterData;

        public static bool IsAIMatch => Mode != GameMode.PvP;
        public static bool IsBossMatch => Mode == GameMode.Boss;

        public static void Reset()
        {
            Mode = GameMode.PvP;
            PlayerDeckNumbers = null;
            AIDeckNumbers = null;
            AIDisplayName = "IA";
            BossEncounterData = null;
        }
    }
}
