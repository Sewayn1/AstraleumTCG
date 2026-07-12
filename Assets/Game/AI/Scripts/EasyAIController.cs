using UnityEngine;

namespace Astraleum.AI
{
    /// <summary>Tirage aléatoire uniforme parmi les actions légales — jamais le meilleur coup scoré.</summary>
    public class EasyAIController : IAIController
    {
        public AIAction DecideNextAction(int aiPlayerID)
        {
            var actions = AIActionScorer.EnumerateActions(aiPlayerID);
            if (actions.Count == 0) return null;

            var pick = actions[Random.Range(0, actions.Count)];
            return new AIAction { Attacker = pick.Attacker, SkillIndex = pick.SkillIndex, Target = pick.Target };
        }
    }
}
