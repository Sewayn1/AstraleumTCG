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
            if (target == null || !target.IsAlive) return;
            if (target.IsInvisible) return; // Cible invisible — non ciblable directement

            float branchAmplify = EvalBranchAmplify(attacker, skill, target);
            float branchAttackBoost = EvalBranchAttackBoost(attacker, skill, target);
            bool ignoreArmor = skill.GetArmorIgnorePercent() >= 1f;
            bool isCrit = false;
            int dmg = 0;
            int actualDmg = 0;

            if (skill.damage > 0)
            {
                isCrit = RollCrit(attacker, skill);
                dmg = DamageCalculator.Calculate(attacker, skill, target, branchAmplify, branchAttackBoost);
                if (isCrit) dmg = ApplyCritMult(dmg, attacker);
                actualDmg = target.TakeDamage(dmg, ignoreArmor);
                target.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(actualDmg);
                if (isCrit)
                {
                    CriticalHitAnnouncer.Instance?.Show();
                    target.GetComponent<CombatPopupHandler>()?.ShowCritDamagePopup(actualDmg);
                }
                StackManager.Instance?.RefreshPermanentStacks();
            }

            // Effets de la compétence
            foreach (var effect in skill.effects)
            {
                if (effect.durationTurns == -1 && effect.type == EffectType.ImmediateHeal)
                {
                    // Drain : soigne l'attaquant d'un % des dégâts infligés
                    int drain = Mathf.RoundToInt(actualDmg * effect.value);
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

            ApplyLifeSteal(attacker, skill, actualDmg);
            ApplyBranches(attacker, skill, target);

            // Bonus Feu : splash adjacent basé sur les DGT calculés (pas post-armure)
            if (StackManager.Instance != null && StackManager.Instance.FireSplashAdjacent(attacker.ownerPlayerID))
            {
                // 3+ stacks Feu : 50% aux adjacents (SingleEnemy uniquement)
                int splashDmg = Mathf.RoundToInt(dmg * 0.5f);
                var adjacents = BoardManager.Instance.GetAdjacentCards(target);
                foreach (var adj in adjacents)
                {
                    if (!adj.IsAlive) continue;
                    int splashActual = adj.TakeDamage(splashDmg);
                    adj.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(splashActual);
                    if (!adj.IsAlive)
                        HandleCardDeath(adj, attacker);
                }
            }

            // Mort de la cible principale
            if (!target.IsAlive)
                HandleCardDeath(target, attacker);

            string critTag = isCrit ? " ★CRIT" : "";
            float burningBonusTag = DamageCalculator.GetBurningPassiveFlatBonus(attacker);
            string burningTag = burningBonusTag > 0f ? $" (+{Mathf.RoundToInt(burningBonusTag)} Passif Brûlure)" : "";
            float allyAliveBonusTag = DamageCalculator.GetAllyAliveFlatBonus(attacker);
            string allyAliveTag = allyAliveBonusTag > 0f ? $" (+{Mathf.RoundToInt(allyAliveBonusTag)} Passif Alliés)" : "";
            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} →{critTag} {actualDmg} DGT à {target.data.cardName} ({skill.skillName}){burningTag}{allyAliveTag}", playerID: attacker.ownerPlayerID);
        }

        // ── SingleAlly ───────────────────────────────────────────────

        private static void ExecuteSingleAlly(CardInstance attacker,
                                               CardSkill skill,
                                               CardInstance target)
        {
            if (target == null || !target.IsAlive) return;

            bool isCrit = RollCrit(attacker, skill);
            bool critApplied = false;

            foreach (var effect in skill.effects)
            {
                if (effect.type == EffectType.ImmediateHeal && isCrit)
                {
                    bool blocked = target.activeEffects.Exists(e => e.type == EffectType.HealBlock);
                    if (!blocked)
                    {
                        int heal = ApplyCritMult(Mathf.RoundToInt(target.EffectiveMaxHP * effect.value), attacker);
                        target.Heal(heal);
                        critApplied = true;
                    }
                    else
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} insoignable", playerID: attacker.ownerPlayerID);
                }
                else
                    ApplyEffect(effect, attacker, target);
            }

            ApplyBranches(attacker, skill, target);

            if (critApplied) CriticalHitAnnouncer.Instance?.Show();
            string critTag = critApplied ? " ★CRIT" : "";
            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} →{critTag} {skill.skillName} sur {target.data.cardName}", playerID: attacker.ownerPlayerID);
        }

        // ── AllEnemies ───────────────────────────────────────────────

        private static void ExecuteAllEnemies(CardInstance attacker, CardSkill skill)
        {
            int enemyID = attacker.ownerPlayerID == 0 ? 1 : 0;
            var enemies = BoardManager.Instance.GetAliveCards(enemyID);
            bool ignoreArmor = skill.GetArmorIgnorePercent() >= 1f;
            int totalHeal = 0;
            bool isCrit = RollCrit(attacker, skill);
            bool attackerBranchesApplied = false;

            // Self effects (non-ImmediateHeal/LifeSteal) fire exactly once — not once per enemy.
            foreach (var effect in skill.effects)
            {
                if (effect.effectTarget != EffectTarget.Self) continue;
                if (effect.type == EffectType.ImmediateHeal) continue;
                if (effect.type == EffectType.LifeSteal && effect.durationTurns == -1) continue;
                ApplyEffectToCard(effect, attacker, attacker);
            }

            // Effets ciblant les alliés (AllAllies, RandomAllies) : s'appliquent une seule fois,
            // pas une fois par ennemi de la boucle ci-dessous.
            foreach (var effect in skill.effects)
            {
                if (effect.effectTarget != EffectTarget.AllAllies &&
                    effect.effectTarget != EffectTarget.RandomAllies) continue;
                if (effect.type == EffectType.ImmediateHeal ||
                    effect.type == EffectType.LifeSteal) continue;
                ApplyEffect(effect, attacker, null);
            }

            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive) continue;

                int dmg = 0;
                int actualDmg = 0;
                if (skill.damage > 0)
                {
                    float branchAmplify = EvalBranchAmplify(attacker, skill, enemy);
                    float branchAttackBoost = EvalBranchAttackBoost(attacker, skill, enemy);
                    dmg = DamageCalculator.Calculate(attacker, skill, enemy, branchAmplify, branchAttackBoost);
                    if (isCrit) dmg = ApplyCritMult(dmg, attacker);
                    actualDmg = enemy.TakeDamage(dmg, ignoreArmor);
                    enemy.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(actualDmg);
                    if (isCrit)
                        enemy.GetComponent<CombatPopupHandler>()?.ShowCritDamagePopup(actualDmg);
                }

                foreach (var effect in skill.effects)
                {
                    if (effect.type == EffectType.ImmediateHeal)
                    {
                        int healAmt = effect.durationTurns == -1
                            ? Mathf.RoundToInt(actualDmg * effect.value)   // drain : % des DGT réels
                            : Mathf.RoundToInt(attacker.EffectiveMaxHP * effect.value);
                        totalHeal += healAmt;
                    }
                    else if (effect.type == EffectType.LifeSteal && effect.durationTurns == -1)
                        { /* géré par ApplyLifeSteal */ }
                    else
                    {
                        if (effect.effectTarget == EffectTarget.Self) continue;
                        if (effect.effectTarget == EffectTarget.AllAllies ||
                            effect.effectTarget == EffectTarget.RandomAllies) continue;
                        // RandomEnnemies : appliqué après la boucle sur 1 seule cible aléatoire
                        if (effect.effectTarget == EffectTarget.RandomEnnemies) continue;
                        ApplyEffectToCard(effect, attacker, enemy);
                    }
                }

                ApplyLifeSteal(attacker, skill, actualDmg);
                // Attacker branches applied once only; Target branches applied per enemy.
                ApplyBranches(attacker, skill, enemy, applyAttackerBranches: !attackerBranchesApplied);
                attackerBranchesApplied = true;

                if (!enemy.IsAlive)
                    HandleCardDeath(enemy, attacker);
            }

            if (totalHeal > 0)
            {
                attacker.Heal(totalHeal);
                CombatLogManager.Instance?.AddEntry(
                    $"{attacker.data.cardName} +{totalHeal} PV", playerID: attacker.ownerPlayerID);
            }

            // RandomEnnemies : appliqué sur 1 seul ennemi aléatoire vivant après tous les dégâts
            var livingEnemies = BoardManager.Instance.GetAliveCards(enemyID);
            if (livingEnemies != null && livingEnemies.Count > 0)
            {
                var randomEnemy = livingEnemies[UnityEngine.Random.Range(0, livingEnemies.Count)];
                foreach (var effect in skill.effects)
                {
                    if (effect.effectTarget != EffectTarget.RandomEnnemies) continue;
                    if (effect.type == EffectType.ImmediateHeal ||
                        (effect.type == EffectType.LifeSteal && effect.durationTurns == -1)) continue;
                    ApplyEffectToCard(effect, attacker, randomEnemy);
                }
            }

            if (isCrit) CriticalHitAnnouncer.Instance?.Show();
            string critTag = isCrit ? " ★CRIT" : "";
            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} →{critTag} {skill.skillName} (AoE)", playerID: attacker.ownerPlayerID);
        }

        // ── AllAllies ────────────────────────────────────────────────

        private static void ExecuteAllAllies(CardInstance attacker, CardSkill skill)
        {
            // Self effects fire exactly once — not once per ally.
            foreach (var effect in skill.effects)
                if (effect.effectTarget == EffectTarget.Self)
                    ApplyEffectToCard(effect, attacker, attacker);

            var allies = BoardManager.Instance.GetAliveCards(attacker.ownerPlayerID);
            bool attackerBranchesApplied = false;

            foreach (var ally in allies)
            {
                if (!ally.IsAlive) continue;
                foreach (var effect in skill.effects)
                {
                    if (effect.effectTarget == EffectTarget.Self) continue; // already applied once before loop
                    ApplyEffectToCard(effect, attacker, ally);
                }
                // Attacker branches applied once only; Target branches applied per ally.
                ApplyBranches(attacker, skill, ally, applyAttackerBranches: !attackerBranchesApplied);
                attackerBranchesApplied = true;
            }

            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} → {skill.skillName} (Alliés)", playerID: attacker.ownerPlayerID);
        }

        // ── AdjacentEnemies ──────────────────────────────────────────

        private static void ExecuteAdjacentEnemies(CardInstance attacker,
                                                    CardSkill skill,
                                                    CardInstance primaryTarget)
        {
            if (primaryTarget == null || !primaryTarget.IsAlive) return;

            float branchAmplify = EvalBranchAmplify(attacker, skill, primaryTarget);
            float branchAttackBoost = EvalBranchAttackBoost(attacker, skill, primaryTarget);
            bool ignoreArmor = skill.GetArmorIgnorePercent() >= 1f;
            bool isCrit = false;
            int mainDmg = 0;
            int mainActual = 0;

            if (skill.damage > 0)
            {
                isCrit = RollCrit(attacker, skill);
                mainDmg = DamageCalculator.Calculate(attacker, skill, primaryTarget, branchAmplify, branchAttackBoost);
                if (isCrit) mainDmg = ApplyCritMult(mainDmg, attacker);
                mainActual = primaryTarget.TakeDamage(mainDmg, ignoreArmor);
                primaryTarget.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(mainActual);
                if (isCrit)
                {
                    CriticalHitAnnouncer.Instance?.Show();
                    primaryTarget.GetComponent<CombatPopupHandler>()?.ShowCritDamagePopup(mainActual);
                }
            }

            // Dégâts adjacents (basés sur mainDmg calculé, pas mainActual)
            var adjacentCards = BoardManager.Instance.GetAdjacentCards(primaryTarget);
            foreach (var adj in adjacentCards)
            {
                if (!adj.IsAlive) continue;
                int adjDmg = Mathf.RoundToInt(mainDmg * skill.adjacentDamagePercent);
                int adjActual = adj.TakeDamage(adjDmg, ignoreArmor);
                adj.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(adjActual);
                CombatLogManager.Instance?.AddEntry(
                    $"{adj.data.cardName} -{adjActual} DGT (adj)", playerID: attacker.ownerPlayerID);
                ApplyLifeSteal(attacker, skill, adjActual);
                if (!adj.IsAlive)
                    HandleCardDeath(adj, attacker);
            }

            foreach (var effect in skill.effects)
            {
                if (effect.durationTurns == -1 && effect.type == EffectType.ImmediateHeal)
                {
                    // Drain : soigne l'attaquant d'un % des DGT réels
                    int drain = Mathf.RoundToInt(mainActual * effect.value);
                    attacker.Heal(drain);
                }
                else if (effect.type == EffectType.LifeSteal && effect.durationTurns == -1)
                    { /* géré par ApplyLifeSteal */ }
                else
                    ApplyEffect(effect, attacker, primaryTarget);
            }

            ApplyLifeSteal(attacker, skill, mainActual);
            ApplyBranches(attacker, skill, primaryTarget);

            if (!primaryTarget.IsAlive)
                HandleCardDeath(primaryTarget, attacker);

            string critTag = isCrit ? " ★CRIT" : "";
            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} →{critTag} {mainActual} DGT à {primaryTarget.data.cardName} +adj ({skill.skillName})", playerID: attacker.ownerPlayerID);
        }

        // ── Self ─────────────────────────────────────────────────────

        private static void ExecuteSelf(CardInstance attacker, CardSkill skill)
        {
            bool isCrit = RollCrit(attacker, skill);
            bool critApplied = false;

            foreach (var effect in skill.effects)
            {
                if (effect.type == EffectType.ImmediateHeal && isCrit)
                {
                    bool blocked = attacker.activeEffects.Exists(e => e.type == EffectType.HealBlock);
                    if (!blocked)
                    {
                        int heal = ApplyCritMult(Mathf.RoundToInt(attacker.EffectiveMaxHP * effect.value), attacker);
                        attacker.Heal(heal);
                        critApplied = true;
                    }
                }
                else
                    ApplyEffect(effect, attacker, attacker);
            }

            ApplyBranches(attacker, skill, attacker);

            if (critApplied) CriticalHitAnnouncer.Instance?.Show();
            string critTag = critApplied ? " ★CRIT" : "";
            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} →{critTag} {skill.skillName}", playerID: attacker.ownerPlayerID);
        }

        // ── Mort d'une carte ─────────────────────────────────────────

        internal static void HandleCardDeath(CardInstance target, CardInstance killer)
        {
            if (NecroticReviveHandler.TryRevive(target)) return;
            NecroticExplosionHandler.TriggerExplosionIfApplicable(target);

            target.pendingIncantations?.Clear();
            // DestroyCard appelle déjà PassiveManager.OnCardDestroyed en interne
            BoardManager.Instance.DestroyCard(target);
            if (killer != null)
                PassiveManager.Instance?.OnCardDestroyedByCard(killer, target);
            CombatLogManager.Instance?.AddEntry(
                $"{target.data.cardName} est détruit !", isDeathEntry: true, playerID: target.ownerPlayerID);
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
                {
                    if (effect.type == EffectType.CooldownReduction && ally == source) continue;
                    ApplyEffectToCard(effect, source, ally);
                }
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
                if (effect.type == EffectType.CooldownReduction)
                    allies = allies.Where(a => a != source).ToList();
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
                        int heal = Mathf.RoundToInt(target.EffectiveMaxHP * effect.value);
                        target.Heal(heal);
                    }
                    else
                    {
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} insoignable", playerID: source.ownerPlayerID);
                    }
                    break;

                case EffectType.CooldownReduction:
                    // Ne peut jamais bénéficier à la carte qui lance la compétence elle-même
                    if (target == source) break;
                    ReduceCooldown(target, (int)effect.value);
                    CombatLogManager.Instance?.AddEntry(
                        $"{target.data.cardName} : Cooldown -{(int)effect.value} tour(s)", playerID: source.ownerPlayerID);
                    break;

                case EffectType.CooldownIncrease:
                    // Ajoute 1 tour de recharge sur les deux compétences de la cible (même si
                    // prêtes), puis bloque leur décompte jusqu'à la fin de son prochain tour
                    // (décompte skippé une fois dans CardInstance.OnTurnStart, effet retiré en
                    // fin de tour dans TurnManager.EndTurnLocal — même schéma que le Stun).
                    // Le décompte normal reprend ensuite son cours.
                    target.skill1Cooldown += 1;
                    target.skill2Cooldown += 1;
                    target.ApplyEffect(new ActiveEffect
                    {
                        type            = EffectType.CooldownIncrease,
                        value           = 0f,
                        remainingTurns  = 1,
                        sourceName      = source?.data?.cardName ?? "",
                        sourceSkillName = effect.sourceSkillName,
                    });
                    CombatLogManager.Instance?.AddEntry(
                        $"{target.data.cardName} : {CombatLogManager.DescribeEffect(EffectType.CooldownIncrease, 0f, 1)}",
                        playerID: source.ownerPlayerID);
                    break;

                case EffectType.ArmorIgnore:
                    // ArmorIgnore est lu dans CalculateDamage — pas un effet persistant
                    break;

                case EffectType.LifeSteal:
                    // Toujours stocké sur le SOURCE (attaquant), jamais sur la cible.
                    // effectTarget est ignoré — un buff LifeSteal ne peut bénéficier qu'à l'attaquant.
                    if (effect.durationTurns > 0 || effect.durationTurns == -1)
                    {
                        source.ApplyEffect(new ActiveEffect
                        {
                            type            = EffectType.LifeSteal,
                            value           = effect.value,
                            remainingTurns  = effect.durationTurns,
                            sourceName      = source?.data?.cardName ?? "",
                            sourceSkillName = effect.sourceSkillName,
                        });
                        CombatLogManager.Instance?.AddEntry(
                            $"{source.data.cardName} : {CombatLogManager.DescribeEffect(EffectType.LifeSteal, effect.value, effect.durationTurns)}",
                            playerID: source.ownerPlayerID);
                    }
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
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} : {CombatLogManager.DescribeEffect(EffectType.Saignement, effect.value, effect.durationTurns)}",
                            playerID: source.ownerPlayerID);
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
                        target.ApplyEffect(new ActiveEffect
                        {
                            type           = EffectType.GiveArmor,
                            value          = effect.value,
                            remainingTurns = effect.durationTurns,
                            sourceName     = source?.data?.cardName ?? "",
                            sourceSkillName = effect.sourceSkillName,
                        });
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} +{Mathf.RoundToInt(effect.value)} armure ({effect.durationTurns}T)", playerID: source.ownerPlayerID);
                        break;
                    }

                case EffectType.ReduceArmor:
                    {
                        target.ApplyEffect(new ActiveEffect
                        {
                            type            = EffectType.ReduceArmor,
                            value           = effect.value,
                            remainingTurns  = effect.durationTurns,
                            sourceName      = source?.data?.cardName ?? "",
                            sourceSkillName = effect.sourceSkillName,
                        });
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} -{Mathf.RoundToInt(effect.value)} armure ({effect.durationTurns}T)", playerID: source.ownerPlayerID);
                        break;
                    }

                case EffectType.GiveArmorAdjacent:
                    {
                        var adjacents = BoardManager.Instance.GetAdjacentCards(source);
                        foreach (var adj in adjacents)
                        {
                            if (adj.ownerPlayerID != source.ownerPlayerID) continue;
                            adj.ApplyEffect(new ActiveEffect
                            {
                                type           = EffectType.GiveArmor,
                                value          = effect.value,
                                remainingTurns = effect.durationTurns,
                                sourceName     = source?.data?.cardName ?? "",
                                sourceSkillName = effect.sourceSkillName,
                            });
                            CombatLogManager.Instance?.AddEntry(
                                $"{adj.data.cardName} +{Mathf.RoundToInt(effect.value)} armure adj ({effect.durationTurns}T)", playerID: source.ownerPlayerID);
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

                case EffectType.Stun:
                    if (target.activeEffects.Exists(e => e.type == EffectType.Inarretable))
                    {
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} — Inarrêtable : Stun ignoré", playerID: source.ownerPlayerID);
                        break;
                    }
                    bool hadPendingIncantation = target.pendingIncantations != null && target.pendingIncantations.Count > 0;
                    target.pendingIncantations?.Clear();
                    target.ApplyEffect(new ActiveEffect
                    {
                        type            = EffectType.Stun,
                        value           = 1f,
                        remainingTurns  = effect.durationTurns,
                        sourceName      = source?.data?.cardName ?? "",
                        sourceSkillName = effect.sourceSkillName,
                    });
                    if (hadPendingIncantation)
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} — incantation interrompue (Stun)", playerID: source.ownerPlayerID);
                    else
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} : {CombatLogManager.DescribeEffect(EffectType.Stun, 1f, effect.durationTurns)}",
                            playerID: source.ownerPlayerID);
                    break;

                case EffectType.Cancel:
                    if (target.activeEffects.Exists(e => e.type == EffectType.Inarretable))
                    {
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} — Inarrêtable : Cancel ignoré", playerID: source.ownerPlayerID);
                        break;
                    }
                    if (target.pendingIncantations != null && target.pendingIncantations.Count > 0)
                    {
                        target.pendingIncantations.Clear();
                        target.GetComponent<CardVisualUpdater>()?.UpdateVisuals();
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} — incantation annulée", playerID: source.ownerPlayerID);
                    }
                    break;

                case EffectType.Burn:
                    target.ApplyEffect(new ActiveEffect
                    {
                        type            = EffectType.Burn,
                        value           = effect.value,
                        remainingTurns  = effect.durationTurns,
                        sourceName      = source?.data?.cardName ?? "",
                        sourceSkillName = effect.sourceSkillName,
                    });
                    CombatLogManager.Instance?.AddEntry(
                        $"{target.data.cardName} : {CombatLogManager.DescribeEffect(EffectType.Burn, effect.value, effect.durationTurns)}",
                        playerID: source.ownerPlayerID);
                    break;

                case EffectType.Poison:
                    target.ApplyEffect(new ActiveEffect
                    {
                        type            = EffectType.Poison,
                        value           = effect.value,
                        remainingTurns  = effect.durationTurns,
                        sourceName      = source?.data?.cardName ?? "",
                        sourceSkillName = effect.sourceSkillName,
                    });
                    CombatLogManager.Instance?.AddEntry(
                        $"{target.data.cardName} : {CombatLogManager.DescribeEffect(EffectType.Poison, effect.value, effect.durationTurns)}",
                        playerID: source.ownerPlayerID);
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
                        CombatLogManager.Instance?.AddEntry(
                            $"{target.data.cardName} : {CombatLogManager.DescribeEffect(effect.type, effect.value, effect.durationTurns, target.data.maxHP)}",
                            playerID: source.ownerPlayerID);
                    }
                    break;
            }

        }

        private static void ReduceCooldown(CardInstance card, int amount)
        {
            card.skill1Cooldown = Mathf.Max(0, card.skill1Cooldown - amount);
            card.skill2Cooldown = Mathf.Max(0, card.skill2Cooldown - amount);
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

        // Retourne le bonus AttackBoost % des branches ciblant l'Attaquant dont la condition est vraie.
        // Appelé AVANT DamageCalculator.Calculate — identique au pattern DamageAmplify.
        private static float EvalBranchAttackBoost(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            if (skill.branches == null || skill.branches.Count == 0) return 0f;
            float total = 0f;
            foreach (var branch in skill.branches)
            {
                if (branch.effectType != BranchEffectType.AttackBoost) continue;
                if (branch.target != BranchTarget.Attacker) continue;
                if (!branch.condition.Evaluate(attacker, target)) continue;
                // AttackBoost est flat en jeu (branchAttackBoost est ajouté tel quel aux DGT dans
                // DamageCalculator.Calculate) : en mode Percent, convertit en DGT absolus (% des DGT
                // de base de la compétence) — sinon "+25%" devient +0.25 DGT (arrondi à ~0).
                total += branch.valueMode == BranchValueMode.Percent
                    ? skill.damage * branch.valuePercent
                    : (float)branch.valueFlat;
            }
            return total;
        }

        private static void ApplyBranches(CardInstance attacker, CardSkill skill, CardInstance primaryTarget,
                                            bool applyAttackerBranches = true)
        {
            if (skill.branches == null || skill.branches.Count == 0) return;

            foreach (var branch in skill.branches)
            {
                if (branch.effectType == BranchEffectType.DamageAmplify) continue; // consommé pre-damage
                // AttackBoost ciblant l'Attaquant → consommé pre-damage via EvalBranchAttackBoost
                if (branch.effectType == BranchEffectType.AttackBoost && branch.target == BranchTarget.Attacker) continue;
                // In AoE contexts, attacker-targeting branches must fire only once (not once per target).
                if (branch.target == BranchTarget.Attacker && !applyAttackerBranches) continue;
                if (!branch.condition.Evaluate(attacker, primaryTarget)) continue;

                CardInstance branchTarget = branch.target == BranchTarget.Attacker ? attacker : primaryTarget;
                if (branchTarget == null || !branchTarget.IsAlive) continue;

                float value = branch.valueMode == BranchValueMode.Percent
                    ? branch.valuePercent
                    : (float)branch.valueFlat;

                // AttackBoost / AttackReduction sont flat en jeu (EffectType.AttackBoost/
                // AttackReduction ne consomment jamais eff.value comme un %) : en mode Percent,
                // convertit en DGT absolus (% des DGT de base de la compétence) avant stockage —
                // sinon "+25%" devient +0.25 DGT (arrondi à ~0).
                if (branch.valueMode == BranchValueMode.Percent &&
                    (branch.effectType == BranchEffectType.AttackBoost ||
                     branch.effectType == BranchEffectType.AttackReduction))
                {
                    value = skill.damage * branch.valuePercent;
                }

                // Dégâts immédiats → appliqués directement, pas stockés comme ActiveEffect
                if (branch.effectType == BranchEffectType.InstantDamage)
                {
                    int dmg = branch.valueMode == BranchValueMode.Flat
                        ? branch.valueFlat
                        : Mathf.RoundToInt(branchTarget.data.maxHP * branch.valuePercent);
                    int actualBranchDmg = branchTarget.TakeDamage(dmg);
                    branchTarget.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(actualBranchDmg);
                    CombatLogManager.Instance?.AddEntry(
                        $"{branchTarget.data.cardName} -{actualBranchDmg} PV (branche)", playerID: attacker.ownerPlayerID);
                    continue;
                }

                // Soin immédiat → appliqué directement, pas stocké comme ActiveEffect
                if (branch.effectType == BranchEffectType.InstantHeal)
                {
                    bool blocked = branchTarget.activeEffects.Exists(e => e.type == EffectType.HealBlock);
                    if (!blocked)
                    {
                        int heal = Mathf.RoundToInt(branchTarget.EffectiveMaxHP * value);
                        branchTarget.Heal(heal);
                        CombatLogManager.Instance?.AddEntry(
                            $"{branchTarget.data.cardName} +{heal} PV (branche)", playerID: attacker.ownerPlayerID);
                    }
                    continue;
                }

                // Exécution → tue directement la cible (ignore l'armure), pas stocké comme
                // ActiveEffect. Contrairement à InstantDamage/InstantHeal ci-dessus, HandleCardDeath
                // est appelé explicitement : Execute garantit toujours la mort (branché derrière une
                // condition IF déjà vérifiée), donc laisser la carte à 0 PV sans nettoyage serait un
                // bug visible immédiat, pas un cas limite.
                if (branch.effectType == BranchEffectType.Execute)
                {
                    if (branchTarget.data.immuneToExecute)
                    {
                        CombatLogManager.Instance?.AddEntry(
                            $"{branchTarget.data.cardName} est immunisé à l'Exécution", playerID: attacker.ownerPlayerID);
                        continue;
                    }
                    int lethal = branchTarget.currentHP;
                    branchTarget.TakeDamage(lethal, ignoreArmor: true);
                    branchTarget.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(lethal);
                    CombatLogManager.Instance?.AddEntry(
                        $"{branchTarget.data.cardName} achevé ! (branche)", playerID: attacker.ownerPlayerID);
                    if (!branchTarget.IsAlive)
                        HandleCardDeath(branchTarget, attacker);
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
                    $"{branchTarget.data.cardName} : {CombatLogManager.DescribeEffect(et, value, branch.durationTurns, branchTarget.data.maxHP)} (branche)",
                    playerID: attacker.ownerPlayerID);
            }
        }

        private static EffectType ToEffectType(BranchEffectType bet) => bet switch
        {
            BranchEffectType.AttackBoost     => EffectType.AttackBoost,
            BranchEffectType.AttackBoostFlat => EffectType.AttackBoostFlat,
            BranchEffectType.AttackReduction => EffectType.AttackReduction,
            BranchEffectType.DamageAmplify   => EffectType.DamageAmplify,
            BranchEffectType.DamageReduction => EffectType.DamageReduction,
            BranchEffectType.Saignement      => EffectType.Saignement,
            BranchEffectType.Burn            => EffectType.Burn,
            BranchEffectType.Poison          => EffectType.Poison,
            BranchEffectType.Stun            => EffectType.Stun,
            BranchEffectType.HealOverTime    => EffectType.HealOverTime,
            BranchEffectType.AddArmor        => EffectType.GiveArmor,
            BranchEffectType.MaxHPReduction  => EffectType.MaxHPReduction,
            BranchEffectType.ReduceArmor     => EffectType.ReduceArmor,
            BranchEffectType.CritChanceBoost => EffectType.CritChanceBoost,
            BranchEffectType.CritDamageBoost    => EffectType.CritDamageBoost,
            BranchEffectType.AttackReductionFlat => EffectType.AttackReductionFlat,
            BranchEffectType.Cancel              => EffectType.Cancel,
            BranchEffectType.Inarretable         => EffectType.Inarretable,
            _                                    => EffectType.Saignement,
        };

        // ── Coup Critique ────────────────────────────────────────────

        // Retourne true si la compétence peut critter et que le roll réussit.
        // Buff et Debuff sont exclus de la mécanique critique.
        private static bool RollCrit(CardInstance attacker, CardSkill skill)
        {
            if (skill.skillType == SkillType.Buff || skill.skillType == SkillType.Debuff)
                return false;
            float chance = attacker.EffectiveCritChance;
            return chance > 0f && Random.value < chance;
        }

        // Applique le multiplicateur critique (+50% de base + CritDamageBoost éventuels).
        private static int ApplyCritMult(int baseDmg, CardInstance attacker)
            => Mathf.Max(1, Mathf.RoundToInt(baseDmg * (1f + attacker.EffectiveCritDamageBonus)));

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

            // Ténèbres majeur 3/5 : bonus Vol de Vie pour cartes Ténèbres
            if (attacker.data.element == Element.Tenebres && StackManager.Instance != null)
                pct += StackManager.Instance.GetDarkLifeStealBonus(attacker.ownerPlayerID);

            if (pct <= 0f) return;

            int heal = Mathf.RoundToInt(dmgDealt * pct);
            if (heal <= 0) return;

            int actual = attacker.Heal(heal, showPopup: false);
            if (actual > 0)
            {
                attacker.GetComponent<CombatPopupHandler>()?.ShowHealPopup(actual, new Vector2(0f, -90f));
                attacker.GetComponent<CardVisualUpdater>()?.SpawnHealVFX();
            }
            CombatLogManager.Instance?.AddEntry(
                $"{attacker.data.cardName} +{heal} PV (Vol de Vie)", playerID: attacker.ownerPlayerID);
        }
    }
}