using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astraleum
{
    public static class SkillExecutor
    {
        public static void Execute(CardInstance attacker, CardSkill skill, CardInstance primaryTarget)
        {
            foreach (var e in skill.effects) e.sourceSkillName = skill.skillName;
            switch (skill.targetType)
            {
                case SkillTargetType.SingleEnemy:
                    ExecuteSingleEnemy(attacker, skill, primaryTarget);
                    break;
                case SkillTargetType.SingleAlly:
                    ExecuteSingleAlly(attacker, skill, primaryTarget);
                    break;
                case SkillTargetType.AllEnemies:
                    ExecuteAllEnemies(attacker, skill);
                    break;
                case SkillTargetType.AllAllies:
                    ExecuteAllAllies(attacker, skill);
                    break;
                case SkillTargetType.AdjacentEnemies:
                    ExecuteAdjacentEnemies(attacker, skill, primaryTarget);
                    break;
                case SkillTargetType.Self:
                    ExecuteSelf(attacker, skill);
                    break;
            }
        }

        // ── SingleEnemy ──────────────────────────────────────────────

        private static void ExecuteSingleEnemy(CardInstance attacker,
                                                CardSkill skill,
                                                CardInstance target)
        {
            if (!target.IsAlive) return;
            if (target.IsInvisible) return; // Cible invisible — non ciblable directement

            float branchAmplify = EvalBranchAmplify(attacker, skill, target);
            int dmg = DamageCalculator.Calculate(attacker, skill, target, branchAmplify);
            bool ignoreArmor = skill.GetArmorIgnorePercent() >= 1f;

            // Ténèbres majeur → Poison appliqué avant les dégâts
            if (attacker.data.element == Element.Tenebres && StackManager.Instance != null)
            {
                float poisonPct = StackManager.Instance.GetPoisonPercent(attacker.ownerPlayerID);
                if (poisonPct > 0f)
                {
                    target.ApplyEffect(new ActiveEffect
                    {
                        type = EffectType.Poison,
                        value = poisonPct,
                        remainingTurns = 2,
                        sourceName = attacker?.data?.cardName ?? ""
                    });
                    CombatLogManager.Instance?.AddEntry(
                        $"{target.data.cardName} ☠ Poison", playerID: attacker.ownerPlayerID);
                }
            }

            target.TakeDamage(dmg, ignoreArmor);
            target.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(dmg);
            StackManager.Instance?.RefreshPermanentStacks();

            // Effets de la compétence
            foreach (var effect in skill.effects)
            {
                if (effect.durationTurns == -1 && effect.type == EffectType.ImmediateHeal)
                {
                    // Drain : soigne l'attaquant d'un % des dégâts infligés
                    int drain = Mathf.RoundToInt(dmg * effect.value);
                    attacker.Heal(drain);
                }
                else if (effect.type == EffectType.LifeSteal && effect.durationTurns == -1)
                {
                    // géré par ApplyLifeSteal après la boucle
                }
                else
                {
                    ApplyEffect(effect, attacker, target);
                }
            }

            ApplyLifeSteal(attacker, skill, dmg);
            ApplyBranches(attacker, skill, target);

            // Bonus Feu : dégâts splash
            if (StackManager.Instance != null)
            {
                int attackerPlayerID = attacker.ownerPlayerID;
                if (StackManager.Instance.FireSplashAll(attackerPlayerID))
                {
                    // 5 stacks Feu : 50% à toutes les cibles ennemies
                    int splashDmg = Mathf.RoundToInt(dmg * 0.5f);
                    int enemyID = attacker.ownerPlayerID == 0 ? 1 : 0;
                    var allEnemies = BoardManager.Instance.GetAliveCards(enemyID);
                    foreach (var enemy in allEnemies)
                    {
                        if (enemy == target || !enemy.IsAlive) continue;
                        enemy.TakeDamage(splashDmg);
                        enemy.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(splashDmg);
                        if (!enemy.IsAlive)
                            HandleCardDeath(enemy, attacker);
                    }
                }
                else if (StackManager.Instance.FireSplashAdjacent(attackerPlayerID))
                {
                    // 3 stacks Feu : 50% aux adjacents
                    int splashDmg = Mathf.RoundToInt(dmg * 0.5f);
                    var adjacents = BoardManager.Instance.GetAdjacentCards(target);
                    foreach (var adj in adjacents)
                    {
                        if (!adj.IsAlive) continue;
                        adj.TakeDamage(splashDmg);
                        adj.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(splashDmg);
                        if (!adj.IsAlive)
                            HandleCardDeath(adj, attacker);
                    }
                }
            }

            // Mort de la cible principale
            if (!target.IsAlive)
                HandleCardDeath(target, attacker);

            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} → {dmg} DGT à {target.data.cardName} ({skill.skillName})", playerID: attacker.ownerPlayerID);
        }

        // ── SingleAlly ───────────────────────────────────────────────

        private static void ExecuteSingleAlly(CardInstance attacker,
                                               CardSkill skill,
                                               CardInstance target)
        {
            if (!target.IsAlive) return;

            foreach (var effect in skill.effects)
                ApplyEffect(effect, attacker, target);

            ApplyBranches(attacker, skill, target);

            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} → {skill.skillName} sur {target.data.cardName}", playerID: attacker.ownerPlayerID);
        }

        // ── AllEnemies ───────────────────────────────────────────────

        private static void ExecuteAllEnemies(CardInstance attacker, CardSkill skill)
        {
            int enemyID = attacker.ownerPlayerID == 0 ? 1 : 0;
            var enemies = BoardManager.Instance.GetAliveCards(enemyID);
            bool ignoreArmor = skill.GetArmorIgnorePercent() >= 1f;
            int totalHeal = 0;

            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive) continue;

                float branchAmplify = EvalBranchAmplify(attacker, skill, enemy);
                int dmg = DamageCalculator.Calculate(attacker, skill, enemy, branchAmplify);
                enemy.TakeDamage(dmg, ignoreArmor);
                enemy.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(dmg);

                foreach (var effect in skill.effects)
                {
                    if (effect.type == EffectType.ImmediateHeal)
                    {
                        int healAmt = effect.durationTurns == -1
                            ? Mathf.RoundToInt(dmg * effect.value)        // drain : % des dégâts infligés
                            : Mathf.RoundToInt(attacker.data.maxHP * effect.value);
                        totalHeal += healAmt;
                    }
                    else if (effect.type == EffectType.LifeSteal && effect.durationTurns == -1)
                        { /* géré par ApplyLifeSteal */ }
                    else
                        ApplyEffect(effect, attacker, enemy);
                }

                ApplyLifeSteal(attacker, skill, dmg);
                ApplyBranches(attacker, skill, enemy);

                if (!enemy.IsAlive)
                    HandleCardDeath(enemy, attacker);
            }

            if (totalHeal > 0)
            {
                attacker.Heal(totalHeal);
                CombatLogManager.Instance?.AddEntry(
                    $"{attacker.data.cardName} +{totalHeal} PV", playerID: attacker.ownerPlayerID);
            }

            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} → {skill.skillName} (AoE)", playerID: attacker.ownerPlayerID);
        }

        // ── AllAllies ────────────────────────────────────────────────

        private static void ExecuteAllAllies(CardInstance attacker, CardSkill skill)
        {
            var allies = BoardManager.Instance.GetAliveCards(attacker.ownerPlayerID);

            foreach (var ally in allies)
            {
                if (!ally.IsAlive) continue;
                foreach (var effect in skill.effects)
                    ApplyEffect(effect, attacker, ally);
                ApplyBranches(attacker, skill, ally);
            }

            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} → {skill.skillName} (Alliés)", playerID: attacker.ownerPlayerID);
        }

        // ── AdjacentEnemies ──────────────────────────────────────────

        private static void ExecuteAdjacentEnemies(CardInstance attacker,
                                                    CardSkill skill,
                                                    CardInstance primaryTarget)
        {
            if (!primaryTarget.IsAlive) return;

            float branchAmplify = EvalBranchAmplify(attacker, skill, primaryTarget);
            int mainDmg = DamageCalculator.Calculate(attacker, skill, primaryTarget, branchAmplify);
            bool ignoreArmor = skill.GetArmorIgnorePercent() >= 1f;

            primaryTarget.TakeDamage(mainDmg, ignoreArmor);
            primaryTarget.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(mainDmg);

            // Dégâts adjacents
            var adjacentCards = BoardManager.Instance.GetAdjacentCards(primaryTarget);
            foreach (var adj in adjacentCards)
            {
                if (!adj.IsAlive) continue;
                int adjDmg = Mathf.RoundToInt(mainDmg * skill.adjacentDamagePercent);
                adj.TakeDamage(adjDmg, ignoreArmor);
                adj.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(adjDmg);
                CombatLogManager.Instance?.AddEntry(
                    $"{adj.data.cardName} -{adjDmg} DGT (adj)", playerID: attacker.ownerPlayerID);
                ApplyLifeSteal(attacker, skill, adjDmg);
                if (!adj.IsAlive)
                    HandleCardDeath(adj, attacker);
            }

            foreach (var effect in skill.effects)
            {
                if (effect.durationTurns == -1 && effect.type == EffectType.ImmediateHeal)
                {
                    // Drain : soigne l'attaquant d'un % des dégâts infligés
                    int drain = Mathf.RoundToInt(mainDmg * effect.value);
                    attacker.Heal(drain);
                }
                else if (effect.type == EffectType.LifeSteal && effect.durationTurns == -1)
                    { /* géré par ApplyLifeSteal */ }
                else
                    ApplyEffect(effect, attacker, primaryTarget);
            }

            ApplyLifeSteal(attacker, skill, mainDmg);
            ApplyBranches(attacker, skill, primaryTarget);

            if (!primaryTarget.IsAlive)
                HandleCardDeath(primaryTarget, attacker);

            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} → {mainDmg} DGT à {primaryTarget.data.cardName} +adj ({skill.skillName})", playerID: attacker.ownerPlayerID);
        }

        // ── Self ─────────────────────────────────────────────────────

        private static void ExecuteSelf(CardInstance attacker, CardSkill skill)
        {
            foreach (var effect in skill.effects)
                ApplyEffect(effect, attacker, attacker);

            ApplyBranches(attacker, skill, attacker);

            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} → {skill.skillName}", playerID: attacker.ownerPlayerID);
        }

        // ── Mort d'une carte ─────────────────────────────────────────

        private static void HandleCardDeath(CardInstance target, CardInstance killer)
        {
            // DestroyCard appelle déjà PassiveManager.OnCardDestroyed en interne
            BoardManager.Instance.DestroyCard(target);
            if (killer != null)
                PassiveManager.Instance?.OnCardDestroyedByCard(killer, target);
            CombatLogManager.Instance?.AddEntry(
                $"{target.data.cardName} est détruit !", isDeathEntry: true);
        }

        // ── Application des effets ───────────────────────────────────

        public static void ApplyEffect(CardEffect effect,
                                        CardInstance source,
                                        CardInstance primaryTarget)
        {
            // GiveArmorAdjacent → toujours appliqué depuis la source
            if (effect.type == EffectType.GiveArmorAdjacent)
            {
                ApplyEffectToCard(effect, source, source);
                return;
            }

            if (effect.effectTarget == EffectTarget.AllAllies)
            {
                var allies = BoardManager.Instance.GetAliveCards(source.ownerPlayerID);
                foreach (var ally in allies)
                    ApplyEffectToCard(effect, source, ally);
                return;
            }

            if (effect.effectTarget == EffectTarget.AllEnemies)
            {
                int enemyID = source.ownerPlayerID == 0 ? 1 : 0;
                var enemies = BoardManager.Instance.GetAliveCards(enemyID);
                foreach (var enemy in enemies)
                    ApplyEffectToCard(effect, source, enemy);
                return;
            }

            if (effect.effectTarget == EffectTarget.RandomAllies)
            {
                var allies = BoardManager.Instance.GetAliveCards(source.ownerPlayerID);
                if (allies != null && allies.Count > 0)
                    ApplyEffectToCard(effect, source, allies[UnityEngine.Random.Range(0, allies.Count)]);
                return;
            }

            if (effect.effectTarget == EffectTarget.RandomEnnemies)
            {
                int enemyID = source.ownerPlayerID == 0 ? 1 : 0;
                var enemies = BoardManager.Instance.GetAliveCards(enemyID);
                if (enemies != null && enemies.Count > 0)
                    ApplyEffectToCard(effect, source, enemies[UnityEngine.Random.Range(0, enemies.Count)]);
                return;
            }

            if (effect.effectTarget == EffectTarget.AdjacentEnemies)
            {
                if (primaryTarget != null)
                    ApplyEffectToCard(effect, source, primaryTarget);
                var adjacents = BoardManager.Instance.GetAdjacentCards(primaryTarget);
                foreach (var adj in adjacents)
                {
                    if (adj.ownerPlayerID != source.ownerPlayerID)
                        ApplyEffectToCard(effect, source, adj);
                }
                return;
            }

            CardInstance actualTarget = effect.effectTarget == EffectTarget.Self
                ? source
                : primaryTarget;

            if (actualTarget != null)
                ApplyEffectToCard(effect, source, actualTarget);
        }

        private static void ApplyEffectToCard(CardEffect effect,
                                               CardInstance source,
                                               CardInstance target)

        {

            if (target == null || !target.IsAlive) return;


            switch (effect.type)
            {

                case EffectType.ImmediateHeal:
                    bool blocked = target.activeEffects
                        .Exists(e => e.type == EffectType.HealBlock);
                    if (!blocked)
                    {
                        int heal = Mathf.RoundToInt(target.data.maxHP * effect.value);
                        target.Heal(heal);
                    }
                    else
                    {
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} insoignable", playerID: source.ownerPlayerID);
                    }
                    break;

                case EffectType.CooldownReduction:
                    ReduceCooldown(target, (int)effect.value);
                    break;

                case EffectType.CooldownIncrease:
                    IncreaseCooldown(target, (int)effect.value);
                    break;

                case EffectType.ArmorIgnore:
                    // ArmorIgnore est lu dans CalculateDamage — pas un effet persistant
                    break;

                case EffectType.Saignement:
                    {
                        // value = % des PV max (ex. 0.05 = 5%/tour) — darkBonus appliqué dans ProcessActiveEffects
                        target.ApplyEffect(new ActiveEffect
                        {
                            type = EffectType.Saignement,
                            value = effect.value,
                            remainingTurns = effect.durationTurns,
                            sourceName = source?.data?.cardName ?? "",
                            sourceSkillName = effect.sourceSkillName,
                        });
                        break;
                    }
                case EffectType.BonusAction:
                    {
                        int bonus = Mathf.Max(1, Mathf.RoundToInt(effect.value));
                        target.bonusActionsRemaining += bonus;
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} +{bonus} action(s) bonus",
                            playerID: source.ownerPlayerID);
                        break;
                    }

                case EffectType.GiveArmor:
                    {
                        int armorGain = Mathf.RoundToInt(effect.value);
                        target.AddArmor(armorGain);
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} +{armorGain} armure", playerID: source.ownerPlayerID);
                        break;
                    }

                case EffectType.GiveArmorAdjacent:
                    {
                        int armorGain = Mathf.RoundToInt(effect.value);
                        var adjacents = BoardManager.Instance.GetAdjacentCards(source);
                        foreach (var adj in adjacents)
                        {
                            if (adj.ownerPlayerID != source.ownerPlayerID) continue;
                            adj.AddArmor(armorGain);
                            CombatLogManager.Instance?.AddEntry(
                                $"{adj.data.cardName} +{armorGain} armure", playerID: source.ownerPlayerID);
                        }
                        break;
                    }

                case EffectType.Invisible:
                    // Toujours permanent (remainingTurns=-1) quel que soit durationTurns
                    target.ApplyEffect(new ActiveEffect
                    {
                        type             = EffectType.Invisible,
                        value            = 1f,
                        remainingTurns   = -1,
                        sourcePassiveTrigger = effect.sourcePassiveTrigger,
                        sourceElement    = effect.sourceElement,
                        sourceName       = source?.data?.cardName ?? ""
                    });
                    break;

                default:
                    if (effect.durationTurns > 0 || effect.durationTurns == -1)
                    {
                        target.ApplyEffect(new ActiveEffect
                        {
                            type = effect.type,
                            value = effect.value,
                            remainingTurns = effect.durationTurns,
                            sourcePassiveTrigger = effect.sourcePassiveTrigger,
                            sourceElement = effect.sourceElement,
                            sourceName = source?.data?.cardName ?? ""
                        });
                    }
                    break;
            }

        }

        private static void ReduceCooldown(CardInstance card, int amount)
        {
            card.skill1Cooldown = Mathf.Max(0, card.skill1Cooldown - amount);
            card.skill2Cooldown = Mathf.Max(0, card.skill2Cooldown - amount);
        }

        private static void IncreaseCooldown(CardInstance card, int amount)
        {
            card.skill1Cooldown += amount;
            card.skill2Cooldown += amount;
        }

        // ── Branches conditionnelles ─────────────────────────────────

        // Retourne le total DamageAmplify des branches dont la condition est vraie.
        // Appelé AVANT DamageCalculator.Calculate pour affecter l'attaque courante.
        private static float EvalBranchAmplify(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            if (skill.branches == null || skill.branches.Count == 0) return 0f;
            float total = 0f;
            foreach (var branch in skill.branches)
            {
                if (branch.effectType != BranchEffectType.DamageAmplify) continue;
                if (!branch.condition.Evaluate(attacker, target)) continue;
                total += branch.valueMode == BranchValueMode.Percent
                    ? branch.valuePercent
                    : (float)branch.valueFlat;
            }
            return total;
        }

        private static void ApplyBranches(CardInstance attacker, CardSkill skill, CardInstance primaryTarget)
        {
            if (skill.branches == null || skill.branches.Count == 0) return;

            foreach (var branch in skill.branches)
            {
                if (branch.effectType == BranchEffectType.DamageAmplify) continue; // consommé pre-damage
                if (!branch.condition.Evaluate(attacker, primaryTarget)) continue;

                CardInstance branchTarget = branch.target == BranchTarget.Attacker ? attacker : primaryTarget;
                if (branchTarget == null || !branchTarget.IsAlive) continue;

                float value = branch.valueMode == BranchValueMode.Percent
                    ? branch.valuePercent
                    : (float)branch.valueFlat;

                // Dégâts immédiats → appliqués directement, pas stockés comme ActiveEffect
                if (branch.effectType == BranchEffectType.InstantDamage)
                {
                    int dmg = branch.valueMode == BranchValueMode.Flat
                        ? branch.valueFlat
                        : Mathf.RoundToInt(branchTarget.data.maxHP * branch.valuePercent);
                    branchTarget.TakeDamage(dmg);
                    CombatLogManager.Instance?.AddEntry(
                        $"{branchTarget.data.cardName} -{dmg} PV (branche)", playerID: attacker.ownerPlayerID);
                    continue;
                }

                // Soin immédiat → appliqué directement, pas stocké comme ActiveEffect
                if (branch.effectType == BranchEffectType.InstantHeal)
                {
                    bool blocked = branchTarget.activeEffects.Exists(e => e.type == EffectType.HealBlock);
                    if (!blocked)
                    {
                        int heal = Mathf.RoundToInt(branchTarget.data.maxHP * value);
                        branchTarget.Heal(heal);
                        CombatLogManager.Instance?.AddEntry(
                            $"{branchTarget.data.cardName} +{heal} PV (branche)", playerID: attacker.ownerPlayerID);
                    }
                    continue;
                }

                EffectType et = ToEffectType(branch.effectType);

                branchTarget.ApplyEffect(new ActiveEffect
                {
                    type            = et,
                    value           = value,
                    remainingTurns  = branch.durationTurns,
                    sourceName      = attacker?.data?.cardName ?? "",
                    sourceSkillName = skill.skillName,
                });

                CombatLogManager.Instance?.AddEntry(
                    $"{branchTarget.data.cardName} ← {et} (branche)", playerID: attacker.ownerPlayerID);
            }
        }

        private static EffectType ToEffectType(BranchEffectType bet) => bet switch
        {
            BranchEffectType.AttackBoost     => EffectType.AttackBoost,
            BranchEffectType.AttackBoostFlat => EffectType.AttackBoostFlat,
            BranchEffectType.AttackReduction => EffectType.AttackReduction,
            BranchEffectType.DamageAmplify   => EffectType.DamageAmplify,
            BranchEffectType.DamageReduction => EffectType.DamageReduction,
            BranchEffectType.Saignement       => EffectType.Saignement,
            BranchEffectType.Burn            => EffectType.Burn,
            BranchEffectType.Poison          => EffectType.Poison,
            BranchEffectType.Stun            => EffectType.Stun,
            BranchEffectType.HealOverTime    => EffectType.HealOverTime,
            _                                => EffectType.Saignement,
        };

        // ── Vol de Vie ───────────────────────────────────────────────

        // Calcule et applique le soin Vol de Vie après chaque coup.
        // Prend en compte : effets immédiats (durationTurns==-1) de la compétence
        // + buffs LifeSteal persistants sur l'attaquant (activeEffects).
        private static void ApplyLifeSteal(CardInstance attacker, CardSkill skill, int dmgDealt)
        {
            if (dmgDealt <= 0) return;

            float pct = 0f;

            // Effets immédiats de la compétence (durationTurns == -1)
            foreach (var eff in skill.effects)
                if (eff.type == EffectType.LifeSteal && eff.durationTurns == -1)
                    pct += eff.value;

            // Buffs LifeSteal persistants sur l'attaquant
            foreach (var eff in attacker.activeEffects)
                if (eff.type == EffectType.LifeSteal)
                    pct += eff.value;

            if (pct <= 0f) return;

            int heal = Mathf.RoundToInt(dmgDealt * pct);
            if (heal <= 0) return;

            int actual = attacker.Heal(heal, showPopup: false);
            if (actual > 0)
                attacker.GetComponent<CombatPopupHandler>()?.ShowHealPopup(actual, new Vector2(0f, -90f));
            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} +{heal} PV (Vol de Vie)", playerID: attacker.ownerPlayerID);
        }
    }
}