using UnityEngine;

namespace Astraleum
{
    public static class DamageCalculator
    {
        public static int Calculate(CardInstance attacker,
                             CardSkill skill,
                             CardInstance target,
                             float extraAmplify = 0f)
        {
            float dmg = skill.damage;

            // Bonus Feu MINEUR → s'applique à TOUS les alliés attaquants
            if (StackManager.Instance != null)
                dmg *= 1f + StackManager.Instance.GetFireDamageBonus(attacker.ownerPlayerID);

            // Bonus AttackBoost des passifs conditionnels (seuils de stacks)
            foreach (var cpe in attacker.conditionalPassiveEffects)
            {
                if (cpe.type != EffectType.AttackBoost) continue;
                int stacks = StackManager.Instance?.GetStacks(
                    attacker.ownerPlayerID, cpe.triggerElement) ?? 0;
                if (stacks >= cpe.requiredThreshold)
                    dmg *= 1f + cpe.value;
            }

            // Bonus AttackBoost des effets actifs (ex. passifs stacksPerTrigger)
            foreach (var eff in attacker.activeEffects)
                if (eff.type == EffectType.AttackBoost)
                    dmg *= 1f + eff.value;

            // Malus AttackReduction des effets actifs (réduit les dégâts infligés)
            foreach (var eff in attacker.activeEffects)
                if (eff.type == EffectType.AttackReduction)
                    dmg *= 1f - eff.value;

            // Eau mineur → tous les alliés défenseurs
            if (StackManager.Instance != null)
                dmg *= 1f - StackManager.Instance.GetWaterDamageReduction(target.ownerPlayerID);

            // Eau majeur 3/5 → cartes Eau uniquement
            if (StackManager.Instance != null && target.data.element == Element.Eau)
                dmg *= 1f - StackManager.Instance.GetWaterMajorEnemyReduction(target.ownerPlayerID);

            // Amplification / réduction effets actifs
            foreach (var eff in target.activeEffects)
            {
                if (eff.type == EffectType.DamageAmplify) dmg *= 1f + eff.value;
                if (eff.type == EffectType.DamageReduction) dmg *= 1f - eff.value;
            }

            // Amplification conditionnelle (branche — courante uniquement, non stockée)
            if (extraAmplify > 0f) dmg *= 1f + extraAmplify;

            // Bonus dégâts plat (après tous les multiplicateurs)
            foreach (var eff in attacker.activeEffects)
                if (eff.type == EffectType.AttackBoostFlat)
                    dmg += eff.value;

            foreach (var cpe in attacker.conditionalPassiveEffects)
            {
                if (cpe.type != EffectType.AttackBoostFlat) continue;
                int stacks = StackManager.Instance?.GetStacks(attacker.ownerPlayerID, cpe.triggerElement) ?? 0;
                if (stacks >= cpe.requiredThreshold) dmg += cpe.value;
            }

            return Mathf.Max(1, Mathf.RoundToInt(dmg));
        }

        public static DamagePreviewData GetPreview(CardInstance attacker,
                                                    CardSkill skill,
                                                    CardInstance target)
        {
            int rawDmg = Calculate(attacker, skill, target);
            bool hasArmor = target.currentArmor > 0;
            bool ignoreArmor = skill.GetArmorIgnorePercent() >= 1f;

            // Dégâts après armure (pour preview)
            int finalDmg;
            if (ignoreArmor || !hasArmor)
            {
                finalDmg = rawDmg;
            }
            else
            {
                int armorAbsorbed = Mathf.Min(target.currentArmor, rawDmg);
                finalDmg = rawDmg - armorAbsorbed;
            }

            bool isAmplified = attacker.data.element == Element.Feu ||
                               attacker.activeEffects.Exists(e => e.type == EffectType.AttackBoost) ||
                               attacker.activeEffects.Exists(e => e.type == EffectType.AttackBoostFlat) ||
                               target.activeEffects.Exists(e => e.type == EffectType.DamageAmplify);

            bool isReduced = target.currentArmor > 0 ||
                               target.activeEffects.Exists(e => e.type == EffectType.DamageReduction) ||
                               attacker.activeEffects.Exists(e => e.type == EffectType.AttackReduction);

            return new DamagePreviewData
            {
                rawDamage = rawDmg,
                estimatedDamage = rawDmg,   // dégâts bruts (armure absorbée visuellement)
                armorAbsorbed = hasArmor && !ignoreArmor
                                  ? Mathf.Min(target.currentArmor, rawDmg)
                                  : 0,
                hpDamage = finalDmg,
                hasArmor = hasArmor,
                ignoreArmor = ignoreArmor,
                isAmplified = isAmplified,
                isReduced = isReduced,
                hasCombo = false      // plus de combo élémentaire
            };
        }
    }

    public class DamagePreviewData
    {
        public int rawDamage;
        public int estimatedDamage;
        public int armorAbsorbed;
        public int hpDamage;
        public bool hasArmor;
        public bool ignoreArmor;
        public bool isAmplified;
        public bool isReduced;
        public bool hasCombo;
    }
}