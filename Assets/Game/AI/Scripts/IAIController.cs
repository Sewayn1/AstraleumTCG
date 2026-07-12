namespace Astraleum.AI
{
    /// <summary>Action décidée par l'IA pour un tour donné.</summary>
    public class AIAction
    {
        public CardInstance Attacker;
        public int SkillIndex;
        public CardInstance Target; // null valide pour AllEnemies / AllAllies / Self
    }

    public interface IAIController
    {
        /// <summary>Retourne null si aucune action n'est possible (fin de tour IA).</summary>
        AIAction DecideNextAction(int aiPlayerID);
    }
}
