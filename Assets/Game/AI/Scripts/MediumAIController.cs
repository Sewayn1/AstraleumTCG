using System.Linq;
using UnityEngine;

namespace Astraleum.AI
{
    /// <summary>75% de chance de jouer le meilleur coup scoré, 25% un coup aléatoire.</summary>
    public class MediumAIController : IAIController
    {
        private const float BEST_MOVE_CHANCE = 0.75f;

        public AIAction DecideNextAction(int aiPlayerID)
        {
            var actions = AIActionScorer.EnumerateActions(aiPlayerID);
            if (actions.Count == 0) return null;

            var best = actions.OrderByDescending(a => a.Score).First();
            ScoredAction pick;

            if (Random.value < BEST_MOVE_CHANCE || actions.Count == 1)
            {
                pick = best;
            }
            else
            {
                var others = actions.Where(a => a != best).ToList();
                pick = others[Random.Range(0, others.Count)];
            }

            return new AIAction { Attacker = pick.Attacker, SkillIndex = pick.SkillIndex, Target = pick.Target };
        }
    }
}
