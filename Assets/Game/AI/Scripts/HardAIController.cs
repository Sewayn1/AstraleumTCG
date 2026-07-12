using System.Linq;
using UnityEngine;

namespace Astraleum.AI
{
    /// <summary>
    /// Toujours le meilleur coup scoré, avec anticipation 1 coup : parmi les meilleures candidates,
    /// choisit celle qui minimise la meilleure réplique humaine possible (minimax profondeur 1).
    /// Lookahead par mutation temporaire de currentHP + rollback synchrone (pas d'infra de snapshot).
    /// </summary>
    public class HardAIController : IAIController
    {
        private const int CANDIDATE_COUNT = 3;

        public AIAction DecideNextAction(int aiPlayerID)
        {
            var actions = AIActionScorer.EnumerateActions(aiPlayerID);
            if (actions.Count == 0) return null;

            int humanPlayerID = 1 - aiPlayerID;
            var candidates = actions.OrderByDescending(a => a.Score).Take(CANDIDATE_COUNT).ToList();

            var chosen = candidates[0];
            float bestNetScore = float.NegativeInfinity;

            foreach (var candidate in candidates)
            {
                float counterThreat = EvaluateCounterThreat(candidate, aiPlayerID, humanPlayerID);
                float netScore = candidate.Score - counterThreat;
                if (netScore > bestNetScore)
                {
                    bestNetScore = netScore;
                    chosen = candidate;
                }
            }

            return new AIAction { Attacker = chosen.Attacker, SkillIndex = chosen.SkillIndex, Target = chosen.Target };
        }

        // Mutation temporaire + rollback synchrone (aucune coroutine entre les deux, aucun rendu
        // intermédiaire visible) : applique les dégâts prédits sur la cible réelle, score la
        // meilleure réplique humaine possible dans cet état hypothétique, puis restaure immédiatement.
        private float EvaluateCounterThreat(ScoredAction candidate, int aiPlayerID, int humanPlayerID)
        {
            var skill = GetSkill(candidate);
            bool isDamageOnEnemy = candidate.Target != null
                                   && candidate.Target.ownerPlayerID != aiPlayerID
                                   && skill.damage > 0;

            if (!isDamageOnEnemy)
                return BestHumanScore(humanPlayerID);

            int before = candidate.Target.currentHP;
            var preview = DamageCalculator.GetPreview(candidate.Attacker, skill, candidate.Target);
            candidate.Target.currentHP = Mathf.Max(0, before - preview.hpDamage);

            float threat = BestHumanScore(humanPlayerID);

            candidate.Target.currentHP = before; // rollback

            return threat;
        }

        private float BestHumanScore(int humanPlayerID)
        {
            var humanActions = AIActionScorer.EnumerateActions(humanPlayerID);
            return humanActions.Count == 0 ? 0f : humanActions.Max(a => a.Score);
        }

        private CardSkill GetSkill(ScoredAction action)
            => action.SkillIndex == 0 ? action.Attacker.data.skillOne : action.Attacker.data.skillTwo;
    }
}
