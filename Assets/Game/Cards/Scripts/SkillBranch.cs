using UnityEngine;

namespace Astraleum
{
    public enum ConditionType
    {
        TargetHPPercent,    // % PV actuel de la cible vs maxHP
        AttackerHPPercent,  // % PV actuel de l'attaquant vs maxHP
        TargetHasEffect,    // La cible possède un effet actif du type donné
        AttackerHasEffect,  // L'attaquant possède un effet actif du type donné
        TargetIsBurning,    // La cible est en état Brûlure
        TargetIsPoisoned,   // La cible est en état Poison
        AttackerIsBurning,  // L'attaquant est en état Brûlure
        AttackerIsPoisoned, // L'attaquant est en état Poison
        AlwaysTrue,         // Toujours vrai (branche inconditionnelle)
    }

    public enum CompareOp
    {
        LessOrEqual,    // ≤
        GreaterOrEqual, // ≥
        Equal,          // =
    }

    public enum BranchEffectType
    {
        AttackBoost,        // Augmente les dégâts infligés par la cible de la branche
        AttackReduction,    // Réduit les dégâts infligés par la cible de la branche
        DamageAmplify,      // Augmente les dégâts reçus par la cible de la branche
        DamageReduction,    // Réduit les dégâts reçus par la cible de la branche
        Saignement,         // Saignement % PV max/tour
        Burn,               // Brûlure % PV max/tour (affectée armure)
        Poison,             // Poison % PV max/tour (ignore armure)
        Stun,               // Étourdissement N tours
        InstantHeal,        // Soin immédiat % PV max (pas de durée)
        HealOverTime,       // Régénération % PV max/tour pendant N tours
        InstantDamage,      // Dégâts immédiats fixes (pas de durée)
        AttackBoostFlat,    // Bonus dégâts fixe (+N dégâts absolus) — TOUJOURS EN DERNIER
    }

    public enum BranchTarget
    {
        Target,     // Appliqué sur la cible principale de la compétence
        Attacker,   // Appliqué sur la carte qui utilise la compétence
    }

    public enum BranchValueMode
    {
        Percent,    // Valeur décimale (ex. 0.15 = 15%)
        Flat,       // Valeur entière absolue (ex. 1 pour 1 tour)
    }

    [System.Serializable]
    public class SkillCondition
    {
        public ConditionType conditionType = ConditionType.TargetHPPercent;
        public CompareOp compareOp = CompareOp.LessOrEqual;

        [Range(0f, 1f)]
        [Tooltip("Seuil HP en % (0.5 = 50% PV). Utilisé pour TargetHPPercent / AttackerHPPercent.")]
        public float threshold = 0.5f;

        [Tooltip("Type d'effet requis. Utilisé pour TargetHasEffect / AttackerHasEffect.")]
        public EffectType effectType = EffectType.Stun;

        public bool Evaluate(CardInstance attacker, CardInstance target)
        {
            switch (conditionType)
            {
                case ConditionType.AlwaysTrue:
                    return true;

                case ConditionType.TargetHPPercent:
                    if (target == null || target.data.maxHP <= 0) return false;
                    return Compare((float)target.currentHP / target.data.maxHP, threshold);

                case ConditionType.AttackerHPPercent:
                    if (attacker == null || attacker.data.maxHP <= 0) return false;
                    return Compare((float)attacker.currentHP / attacker.data.maxHP, threshold);

                case ConditionType.TargetHasEffect:
                    return target != null && target.activeEffects.Exists(e => e.type == effectType);

                case ConditionType.AttackerHasEffect:
                    return attacker != null && attacker.activeEffects.Exists(e => e.type == effectType);

                case ConditionType.TargetIsBurning:
                    return target != null && target.activeEffects.Exists(e => e.type == EffectType.Burn);

                case ConditionType.TargetIsPoisoned:
                    return target != null && target.activeEffects.Exists(e => e.type == EffectType.Poison);

                case ConditionType.AttackerIsBurning:
                    return attacker != null && attacker.activeEffects.Exists(e => e.type == EffectType.Burn);

                case ConditionType.AttackerIsPoisoned:
                    return attacker != null && attacker.activeEffects.Exists(e => e.type == EffectType.Poison);
            }
            return false;
        }

        private bool Compare(float a, float b)
        {
            return compareOp switch
            {
                CompareOp.LessOrEqual    => a <= b,
                CompareOp.GreaterOrEqual => a >= b,
                CompareOp.Equal          => Mathf.Approximately(a, b),
                _                        => false,
            };
        }
    }

    [System.Serializable]
    public class ConditionalBranch
    {
        [Tooltip("Condition à évaluer avant d'appliquer l'effet de branche.")]
        public SkillCondition condition = new SkillCondition();

        [Tooltip("Effet à appliquer si la condition est vraie.")]
        public BranchEffectType effectType = BranchEffectType.AttackBoost;

        [Tooltip("Carte sur laquelle l'effet est appliqué.")]
        public BranchTarget target = BranchTarget.Target;

        [Tooltip("Mode de la valeur.")]
        public BranchValueMode valueMode = BranchValueMode.Percent;

        [Range(0f, 1f)]
        [Tooltip("Valeur décimale (ex. 0.15 = 15%). Actif si valueMode = Percent.")]
        public float valuePercent = 0.15f;

        [Tooltip("Valeur absolue (ex. 1 pour Stun 1 tour). Actif si valueMode = Flat.")]
        public int valueFlat = 1;

        [Tooltip("Durée en tours. DoT/Burn/Poison = durée du saignement. Stun = tours bloqués. -1 = permanent.")]
        public int durationTurns = 2;
    }
}
