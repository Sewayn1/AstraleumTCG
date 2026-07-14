using UnityEngine;

namespace Astraleum
{
    public static class DamageCalculator
    {
        // Plafond de la somme des instances SelfDamageAmplify d'un attaquant (8 stacks × 5% = 40%)
        private const float MAX_SELF_DAMAGE_AMPLIFY = 0.4f;

        public static int Calculate(CardInstance attacker,
                             CardSkill skill,
                             CardInstance target,
                             float extraAmplify = 0f,
                             float branchAttackBoost = 0f)
        {
            float dmg = skill.damage;

            // Bonus Feu MINEUR → s'applique à TOUS les alliés attaquants
            if (StackManager.Instance != null)
                dmg *= 1f + StackManager.Instance.GetFireDamageBonus(attacker.ownerPlayerID);

            // Bonus Ténèbres MINEUR → +2% DGT par carte adverse en vie (toutes cartes alliées)
            if (StackManager.Instance != null)
                dmg *= 1f + StackManager.Instance.GetDarkDamageBonus(attacker.ownerPlayerID);

            // (AttackBoost et AttackReduction sont traités en flat après les multiplicateurs)

            // Eau mineur → tous les alliés défenseurs
            if (StackManager.Instance != null)
                dmg *= 1f - StackManager.Instance.GetWaterDamageReduction(target.ownerPlayerID);

            // Terre mineur → tous les alliés défenseurs (-2% DGT subis/stack)
            if (StackManager.Instance != null)
                dmg *= 1f - StackManager.Instance.GetEarthDamageReduction(target.ownerPlayerID);

            // Eau majeur 3/5 → cartes Eau uniquement
            if (StackManager.Instance != null && target.data.element == Element.Eau)
                dmg *= 1f - StackManager.Instance.GetWaterMajorEnemyReduction(target.ownerPlayerID);

            // Amplification / réduction effets actifs
            foreach (var eff in target.activeEffects)
            {
                if (eff.type == EffectType.DamageAmplify) dmg *= 1f + eff.value;
                if (eff.type == EffectType.DamageReduction) dmg *= 1f - eff.value;
            }

            // Amplification des dégâts INFLIGÉS (côté attaquant, ex. Voragoth qui s'auto-buff).
            // Chaque instance vient d'un cast différent et n'est jamais fusionnée (voir
            // CardInstance.ApplyEffect) — sommée puis PLAFONNÉE à MAX_SELF_DAMAGE_AMPLIFY (40%,
            // soit 8 stacks de +5%) avant application unique, plutôt que multipliée en boucle
            // (qui composait de façon exponentielle et sans limite, ex. 1.05^12 ≈ +80%).
            float selfAmplifySum = 0f;
            foreach (var eff in attacker.activeEffects)
                if (eff.type == EffectType.SelfDamageAmplify) selfAmplifySum += eff.value;
            dmg *= 1f + Mathf.Min(selfAmplifySum, MAX_SELF_DAMAGE_AMPLIFY);

            // Amplification conditionnelle (branche — courante uniquement, non stockée)
            if (extraAmplify > 0f) dmg *= 1f + extraAmplify;

            // Bonus / malus flat (après tous les multiplicateurs)
            // AttackBoost et AttackReduction sont maintenant flat (valeur fixe en DGT)
            foreach (var eff in attacker.activeEffects)
            {
                if (eff.type == EffectType.AttackBoost)         dmg += eff.value;
                if (eff.type == EffectType.AttackBoostFlat)     dmg += eff.value;
                if (eff.type == EffectType.AttackReduction)     dmg -= eff.value;
                if (eff.type == EffectType.AttackReductionFlat) dmg -= eff.value;
            }

            foreach (var cpe in attacker.conditionalPassiveEffects)
            {
                int stacks = StackManager.Instance?.GetStacks(attacker.ownerPlayerID, cpe.triggerElement) ?? 0;
                if (stacks < cpe.requiredThreshold) continue;
                if (cpe.type == EffectType.AttackBoost)     dmg += cpe.value;
                if (cpe.type == EffectType.AttackBoostFlat) dmg += cpe.value;
            }

            if (branchAttackBoost != 0f) dmg += branchAttackBoost;

            // Passif dynamique CardIsBurning : +value DGT par carte brûlante sur le terrain
            // (n'importe quel camp). Recalculé à chaque attaque, jamais stocké comme ActiveEffect.
            dmg += GetBurningPassiveFlatBonus(attacker);

            // Passif dynamique ForEachAllyAlive : +value DGT par allié encore en vie (hors soi-même).
            // Se réduit automatiquement quand des alliés meurent — jamais stocké comme ActiveEffect.
            dmg += GetAllyAliveFlatBonus(attacker);

            return Mathf.Max(1, Mathf.RoundToInt(dmg));
        }

        // Nombre de cartes (alliées + ennemies) actuellement affectées par Burn.
        public static int CountBurningCardsOnField()
        {
            if (BoardManager.Instance == null) return 0;
            int count = 0;
            for (int p = 0; p < 2; p++)
                foreach (var c in BoardManager.Instance.GetAliveCards(p))
                    if (c.activeEffects.Exists(e => e.type == EffectType.Burn))
                        count++;
            return count;
        }

        // Bonus DGT flat du passif CardIsBurning : effect.value × nb de cartes brûlantes sur le terrain.
        // Ne s'applique qu'aux cartes dont le passif utilise explicitement ce trigger — aucun impact
        // sur les passifs existants (autres triggers).
        public static float GetBurningPassiveFlatBonus(CardInstance card)
        {
            var passive = card?.data?.passive;
            if (passive == null || string.IsNullOrEmpty(passive.passiveDescription)) return 0f;
            if (passive.trigger != PassiveTrigger.CardIsBurning) return 0f;
            if (passive.effects == null || passive.effects.Count == 0) return 0f;

            int burningCount = CountBurningCardsOnField();
            if (burningCount == 0) return 0f;

            float bonus = 0f;
            foreach (var effect in passive.effects)
                if (effect.type == EffectType.AttackBoost || effect.type == EffectType.AttackBoostFlat)
                    bonus += effect.value * burningCount;
            return bonus;
        }

        // Nombre d'alliés vivants de la carte, en excluant la carte elle-même.
        public static int CountAliveAllies(CardInstance card)
        {
            if (card == null || BoardManager.Instance == null) return 0;
            int count = 0;
            foreach (var c in BoardManager.Instance.GetAliveCards(card.ownerPlayerID))
                if (c != card)
                    count++;
            return count;
        }

        // Bonus DGT flat du passif ForEachAllyAlive : effect.value × nb d'alliés encore en vie
        // (hors soi-même). Diminue automatiquement à mesure que des alliés sont détruits, jusqu'à 0.
        public static float GetAllyAliveFlatBonus(CardInstance card)
        {
            var passive = card?.data?.passive;
            if (passive == null || string.IsNullOrEmpty(passive.passiveDescription)) return 0f;
            if (passive.trigger != PassiveTrigger.ForEachAllyAlive) return 0f;
            if (passive.effects == null || passive.effects.Count == 0) return 0f;

            int allyCount = CountAliveAllies(card);
            if (allyCount == 0) return 0f;

            float bonus = 0f;
            foreach (var effect in passive.effects)
                if (effect.type == EffectType.AttackBoost || effect.type == EffectType.AttackBoostFlat)
                    bonus += effect.value * allyCount;
            return bonus;
        }

        public static DamagePreviewData GetPreview(CardInstance attacker,
                                                    CardSkill skill,
                                                    CardInstance target)
        {
            int rawDmg = Calculate(attacker, skill, target);
            int armor = target.TotalArmor;
            bool hasArmor = armor > 0;
            bool ignoreArmor = skill.GetArmorIgnorePercent() >= 1f;

            // DGT réels après réduction armure (pour preview)
            int finalDmg = ignoreArmor ? rawDmg : Mathf.Max(0, rawDmg - armor);
            int armorAbsorbed = ignoreArmor ? 0 : Mathf.Min(armor, rawDmg);

            bool isAmplified = attacker.data.element == Element.Feu ||
                               (StackManager.Instance?.GetDarkDamageBonus(attacker.ownerPlayerID) > 0f) ||
                               attacker.activeEffects.Exists(e => e.type == EffectType.AttackBoost) ||
                               attacker.activeEffects.Exists(e => e.type == EffectType.AttackBoostFlat) ||
                               target.activeEffects.Exists(e => e.type == EffectType.DamageAmplify) ||
                               GetBurningPassiveFlatBonus(attacker) > 0f ||
                               GetAllyAliveFlatBonus(attacker) > 0f;

            bool isReduced = hasArmor ||
                               target.activeEffects.Exists(e => e.type == EffectType.DamageReduction) ||
                               attacker.activeEffects.Exists(e => e.type == EffectType.AttackReduction);

            bool canCrit = skill.skillType != SkillType.Buff &&
                           skill.skillType != SkillType.Debuff &&
                           attacker.EffectiveCritChance > 0f;
            int critDmg = canCrit
                ? Mathf.Max(1, Mathf.RoundToInt(rawDmg * (1f + attacker.EffectiveCritDamageBonus)))
                : 0;
            int critFinalDmg = canCrit
                ? (ignoreArmor ? critDmg : Mathf.Max(0, critDmg - armor))
                : 0;

            return new DamagePreviewData
            {
                rawDamage = rawDmg,
                estimatedDamage = rawDmg,
                armorAbsorbed = armorAbsorbed,
                hpDamage = finalDmg,
                hasArmor = hasArmor,
                ignoreArmor = ignoreArmor,
                isAmplified = isAmplified,
                isReduced = isReduced,
                hasCombo = false,
                canCrit = canCrit,
                critDamage = critDmg,
                critHpDamage = critFinalDmg,
                critChance = attacker.EffectiveCritChance,
            };
        }

        // ── Modificateurs côté attaquant (sans cible) ─────────────────────────
        // Cumule TOUS les bonus/malus DGT atteignables sans connaître la cible :
        // stacks mineurs, passifs conditionnels actifs, effets actifs.
        // Retourne > 1 si boosté, < 1 si réduit, ≈ 1 si neutre.
        // Retourne uniquement le multiplicateur % (Feu, Ténèbres, stacks).
        // Les bonus flat (AttackBoost/AttackReduction) sont gérés séparément par GetAttackerFlatBonus.
        public static float GetAttackerModifier(CardInstance card)
        {
            if (card == null) return 1f;
            float mod = 1f;

            if (StackManager.Instance != null)
            {
                mod *= 1f + StackManager.Instance.GetFireDamageBonus(card.ownerPlayerID);
                mod *= 1f + StackManager.Instance.GetDarkDamageBonus(card.ownerPlayerID);
            }

            return mod;
        }

        // Retourne le bonus/malus flat total (AttackBoost + AttackBoostFlat - AttackReduction - AttackReductionFlat)
        // incluant les passifs conditionnels actifs. Utilisé pour la coloration des descriptions.
        public static float GetAttackerFlatBonus(CardInstance card)
        {
            if (card == null) return 0f;
            float flat = 0f;

            foreach (var eff in card.activeEffects)
            {
                if (eff.type == EffectType.AttackBoost)         flat += eff.value;
                if (eff.type == EffectType.AttackBoostFlat)     flat += eff.value;
                if (eff.type == EffectType.AttackReduction)     flat -= eff.value;
                if (eff.type == EffectType.AttackReductionFlat) flat -= eff.value;
            }

            foreach (var cpe in card.conditionalPassiveEffects)
            {
                int stacks = StackManager.Instance?.GetStacks(card.ownerPlayerID, cpe.triggerElement) ?? 0;
                if (stacks < cpe.requiredThreshold) continue;
                if (cpe.type == EffectType.AttackBoost)     flat += cpe.value;
                if (cpe.type == EffectType.AttackBoostFlat) flat += cpe.value;
            }

            flat += GetBurningPassiveFlatBonus(card);
            flat += GetAllyAliveFlatBonus(card);

            return flat;
        }

        // Retourne le multiplicateur de soins de la carte.
        // < 1 si HealBlock actif, > 1 si stacks Lumière actifs.
        public static float GetHealModifier(CardInstance card)
        {
            if (card == null) return 1f;

            // HealBlock → soins complètement bloqués
            if (card.activeEffects.Exists(e => e.type == EffectType.HealBlock)) return 0f;

            float mod = 1f;
            if (StackManager.Instance != null)
                mod += StackManager.Instance.GetHealBonus(card.ownerPlayerID);

            // Compensation combat Boss : +5% Instant Heal / HOT pour le joueur humain dès la Phase 2
            if (BossPhaseController.Instance != null && BossPhaseController.Instance.IsPhase2Or3
                && card.ownerPlayerID == BossPhaseController.Instance.humanPlayerID)
                mod += 0.05f;

            // Nécrotique — réduction de soins reçus
            float healReduction = 0f;
            foreach (var eff in card.activeEffects)
                if (eff.type == EffectType.HealReduction) healReduction += eff.value;
            mod *= Mathf.Max(0f, 1f - healReduction);

            return mod;
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
        public bool  canCrit;
        public int   critDamage;
        public int   critHpDamage;
        public float critChance;
    }
}