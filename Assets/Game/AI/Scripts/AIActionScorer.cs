using System.Collections.Generic;
using System.Linq;

namespace Astraleum.AI
{
    public class ScoredAction
    {
        public CardInstance Attacker;
        public int SkillIndex;
        public CardInstance Target; // null valide pour AllEnemies / AllAllies / Self
        public float Score;
    }

    /// <summary>
    /// Module de scoring commun aux IA Facile/Moyenne/Difficile. Pur (aucune mutation d'état),
    /// réutilise DamageCalculator (source de vérité des dégâts) et SkillCondition.Evaluate
    /// (branches conditionnelles). Poids calibrables par playtest ci-dessous.
    /// </summary>
    public static class AIActionScorer
    {
        // ── Poids de calibration (playtest) ─────────────────────────────
        private const float KILL_BONUS = 500f;
        private const float HEAL_FULL_HP_WEIGHT = 0f;     // soigner une carte à PV pleins ne vaut rien
        private const float BUFF_DEBUFF_BASE_VALUE = 30f; // buff/debuff sans dégâts direct (heuristique fixe)

        private static readonly Dictionary<BranchEffectType, float> BranchWeights = new Dictionary<BranchEffectType, float>
        {
            { BranchEffectType.Stun, 80f },
            { BranchEffectType.Burn, 25f },
            { BranchEffectType.Poison, 25f },
            { BranchEffectType.Saignement, 20f },
            { BranchEffectType.InstantDamage, 40f },
            { BranchEffectType.AttackBoost, 15f },
            { BranchEffectType.AttackBoostFlat, 15f },
            { BranchEffectType.AttackReduction, 15f },
            { BranchEffectType.AttackReductionFlat, 15f },
            { BranchEffectType.DamageAmplify, 20f },
            { BranchEffectType.DamageReduction, 20f },
            { BranchEffectType.AddArmor, 15f },
            { BranchEffectType.ReduceArmor, 15f },
            { BranchEffectType.CritChanceBoost, 10f },
            { BranchEffectType.CritDamageBoost, 10f },
            { BranchEffectType.MaxHPReduction, 30f },
            { BranchEffectType.InstantHeal, 20f },
            { BranchEffectType.HealOverTime, 15f },
            { BranchEffectType.Cancel, 30f },
            { BranchEffectType.Inarretable, 10f },
        };

        public static List<ScoredAction> EnumerateActions(int aiPlayerID)
        {
            var result = new List<ScoredAction>();
            var bm = BoardManager.Instance;
            if (bm == null) return result;

            foreach (var attacker in bm.GetAliveCards(aiPlayerID))
            {
                if (!attacker.IsReady) continue;
                if (attacker.pendingIncantations.Count > 0) continue;

                for (int skillIndex = 0; skillIndex < 2; skillIndex++)
                {
                    var skill = skillIndex == 0 ? attacker.data.skillOne : attacker.data.skillTwo;
                    if (skill == null) continue;
                    int cooldown = skillIndex == 0 ? attacker.skill1Cooldown : attacker.skill2Cooldown;
                    if (cooldown > 0) continue;

                    foreach (var target in EnumerateTargets(attacker, skill, aiPlayerID))
                    {
                        result.Add(new ScoredAction
                        {
                            Attacker = attacker,
                            SkillIndex = skillIndex,
                            Target = target,
                            Score = Score(attacker, skill, skillIndex, target),
                        });
                    }
                }
            }
            return result;
        }

        private static IEnumerable<CardInstance> EnumerateTargets(CardInstance attacker, CardSkill skill, int aiPlayerID)
        {
            var bm = BoardManager.Instance;
            int enemyID = 1 - aiPlayerID;

            switch (skill.targetType)
            {
                case SkillTargetType.SingleEnemy:
                    foreach (var c in bm.GetAliveCards(enemyID))
                        if (!c.IsInvisible) yield return c;
                    break;

                case SkillTargetType.AdjacentEnemies:
                    // primaryTarget sert d'ancrage — SkillExecutor applique ensuite aux adjacents.
                    foreach (var c in bm.GetAliveCards(enemyID))
                        if (!c.IsInvisible) yield return c;
                    break;

                case SkillTargetType.SingleAlly:
                    foreach (var c in bm.GetAliveCards(aiPlayerID))
                        yield return c;
                    break;

                case SkillTargetType.AllEnemies:
                case SkillTargetType.AllAllies:
                case SkillTargetType.Self:
                    // Pas de ciblage individuel — une seule action possible, target ignoré par SkillExecutor.
                    yield return null;
                    break;
            }
        }

        public static float Score(CardInstance attacker, CardSkill skill, int skillIndex, CardInstance target)
        {
            if (skill.damage > 0 && (skill.skillType == SkillType.Attack || skill.skillType == SkillType.Debuff || skill.skillType == SkillType.Mixed))
                return ScoreDamage(attacker, skill, target);

            if (skill.IsHealSkill || skill.GetImmediateHealPercent() > 0f || skill.GetHealOverTimePercent() > 0f)
                return ScoreHeal(attacker, skill, target);

            return BUFF_DEBUFF_BASE_VALUE + ScoreBranches(attacker, skill, target ?? attacker);
        }

        private static float ScoreDamage(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            var bm = BoardManager.Instance;
            int enemyID = 1 - attacker.ownerPlayerID;

            if (target == null && skill.targetType == SkillTargetType.AllEnemies)
            {
                float total = 0f;
                foreach (var enemy in bm.GetAliveCards(enemyID))
                    total += ScoreSingleTarget(attacker, skill, enemy);
                return total;
            }

            if (target == null) return 0f; // AllAllies/Self n'ont pas de branche "dégâts"

            return ScoreSingleTarget(attacker, skill, target);
        }

        private static float ScoreSingleTarget(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            var preview = DamageCalculator.GetPreview(attacker, skill, target);
            float score = preview.hpDamage;

            if (preview.canCrit)
                score += preview.critChance * (preview.critHpDamage - preview.hpDamage);

            if (preview.hpDamage >= target.currentHP)
                score += KILL_BONUS;

            score += ScoreBranches(attacker, skill, target);
            return score;
        }

        private static float ScoreHeal(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            var bm = BoardManager.Instance;

            if (skill.targetType == SkillTargetType.AllAllies)
            {
                float total = 0f;
                foreach (var ally in bm.GetAliveCards(attacker.ownerPlayerID))
                    total += ScoreHealSingle(attacker, skill, ally);
                return total;
            }

            var actualTarget = target ?? attacker;
            return ScoreHealSingle(attacker, skill, actualTarget);
        }

        private static float ScoreHealSingle(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            float missingRatio = 1f - (float)target.currentHP / target.EffectiveMaxHP;
            if (missingRatio <= 0f) return HEAL_FULL_HP_WEIGHT;

            float healPercent = skill.GetImmediateHealPercent() + skill.GetHealOverTimePercent();
            float healValue = healPercent * target.EffectiveMaxHP * DamageCalculator.GetHealModifier(target);

            return healValue * missingRatio;
        }

        private static float ScoreBranches(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            float total = 0f;
            foreach (var branch in skill.branches)
            {
                if (!branch.condition.Evaluate(attacker, target)) continue;
                if (BranchWeights.TryGetValue(branch.effectType, out float weight))
                    total += weight;
            }
            return total;
        }
    }
}
