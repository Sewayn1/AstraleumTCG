using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astraleum.AI
{
    /// <summary>
    /// Décide de l'action de Voragoth chaque tour : mélange Focus / Chaos pondéré par
    /// BossPhaseController.FocusChance (P1 50/50, P2 70/30, P3 90/10).
    ///
    /// Focus : réutilise AIActionScorer.EnumerateActions À L'ENVERS (appelé avec l'ID du joueur
    /// humain) pour détecter quelle carte adverse a la meilleure action scorée ce tour-ci —
    /// c'est la carte la plus menaçante. Voragoth choisit alors, parmi ses compétences prêtes,
    /// celle la plus adaptée pour la neutraliser (HealBlock sur un soigneur détecté, sinon la
    /// plus grosse compétence de dégâts disponible).
    ///
    /// Chaos : pioche pseudo-aléatoire parmi les compétences prêtes, mais cohérente — évite de
    /// reproposer un debuff déjà actif à l'identique sur une cible si une alternative existe.
    /// </summary>
    public static class BossAIController
    {
        public struct BossAction
        {
            public int SkillIndex;
            public CardSkill Skill;
            public CardInstance Target; // null valide pour AllEnemies / Self
        }

        public static BossAction? DecideAction(CardInstance boss, int humanPlayerID, float focusChance)
        {
            var readySkills = GetReadySkills(boss);
            if (readySkills.Count == 0) return null;

            bool focus = Random.value < focusChance;
            return focus
                ? DecideFocus(boss, humanPlayerID, readySkills)
                : DecideChaos(boss, readySkills);
        }

        private static List<(int index, CardSkill skill)> GetReadySkills(CardInstance boss)
        {
            var list = new List<(int, CardSkill)>();
            if (boss.skill1Cooldown == 0 && boss.data.skillOne   != null) list.Add((0, boss.data.skillOne));
            if (boss.skill2Cooldown == 0 && boss.data.skillTwo   != null) list.Add((1, boss.data.skillTwo));
            if (boss.skill3Cooldown == 0 && boss.data.HasSkillThree) list.Add((2, boss.data.skillThree));
            return list;
        }

        private static bool NeedsTarget(SkillTargetType t) =>
            t == SkillTargetType.SingleEnemy || t == SkillTargetType.AdjacentEnemies;

        private static List<CardInstance> ValidTargets(CardInstance boss)
        {
            int enemyID = 1 - boss.ownerPlayerID;
            return BoardManager.Instance.GetAliveCards(enemyID).Where(c => !c.IsInvisible).ToList();
        }

        // ── Focus ──────────────────────────────────────────────────────
        private static BossAction? DecideFocus(CardInstance boss, int humanPlayerID,
                                                List<(int index, CardSkill skill)> readySkills)
        {
            var actions = AIActionScorer.EnumerateActions(humanPlayerID);
            CardInstance threat = null;
            bool threatIsHeal = false;

            if (actions.Count > 0)
            {
                var best = actions.OrderByDescending(a => a.Score).First();
                threat = best.Attacker;
                var bestSkill = best.SkillIndex == 0 ? best.Attacker.data.skillOne : best.Attacker.data.skillTwo;
                threatIsHeal = bestSkill != null &&
                               (bestSkill.IsHealSkill ||
                                bestSkill.GetImmediateHealPercent() > 0f ||
                                bestSkill.GetHealOverTimePercent() > 0f);
            }

            // Menace = un soigneur → privilégie une compétence prête qui pose HealBlock
            if (threatIsHeal)
            {
                var healBlockSkill = readySkills.FirstOrDefault(s =>
                    s.skill.effects.Exists(e => e.type == EffectType.HealBlock));
                if (healBlockSkill.skill != null)
                    return Build(healBlockSkill.index, healBlockSkill.skill,
                                 NeedsTarget(healBlockSkill.skill.targetType) ? threat : null);
            }

            // Sinon : la compétence prête qui inflige le plus de dégâts de base, sur la menace détectée.
            // Le skill CD1 de chaque phase est un filet de sécurité ("toujours au moins une action
            // disponible" — voir design doc) censé n'être choisi que si RIEN d'autre n'est prêt.
            // Comme il redevient prêt chaque tour, un tri par dégâts brut sans distinction le fait
            // gagner presque à chaque fois (il inflige souvent le plus de dégâts des 3), starvant les
            // compétences à vrai cooldown (CD3). On priorise donc d'abord les compétences prêtes
            // ayant un vrai cooldown (>1 tour), et on ne retombe sur le CD1 que s'il est la seule
            // option prête ce tour-ci.
            var nonFillerReady = readySkills.Where(s => s.skill.cooldownTurns > 1).ToList();
            var pool = nonFillerReady.Count > 0 ? nonFillerReady : readySkills;
            var best2 = pool.OrderByDescending(s => s.skill.damage).First();
            var target2 = NeedsTarget(best2.skill.targetType)
                ? (threat ?? ValidTargets(boss).FirstOrDefault())
                : null;
            return Build(best2.index, best2.skill, target2);
        }

        // ── Chaos ──────────────────────────────────────────────────────
        private static BossAction? DecideChaos(CardInstance boss, List<(int index, CardSkill skill)> readySkills)
        {
            var targets = ValidTargets(boss);

            var candidates = readySkills.Where(s => !NeedsTarget(s.skill.targetType) || targets.Count > 0).ToList();
            if (candidates.Count == 0) return null;

            var pick = candidates[Random.Range(0, candidates.Count)];

            CardInstance target = null;
            if (NeedsTarget(pick.skill.targetType))
            {
                var filtered = FilterNonRedundantTargets(pick.skill, targets);
                var pool = filtered.Count > 0 ? filtered : targets;
                target = pool[Random.Range(0, pool.Count)];
            }

            return Build(pick.index, pick.skill, target);
        }

        // Évite de reproposer un debuff déjà actif à l'identique sur une cible s'il existe une
        // alternative (ex. ne pas re-HealBlock une cible déjà HealBlock).
        private static List<CardInstance> FilterNonRedundantTargets(CardSkill skill, List<CardInstance> targets)
        {
            var debuffTypes = skill.effects
                .Where(e => e.type == EffectType.HealBlock ||
                            e.type == EffectType.Poison ||
                            e.type == EffectType.Saignement)
                .Select(e => e.type)
                .ToList();
            if (debuffTypes.Count == 0) return targets;

            return targets.Where(t => !debuffTypes.Any(dt => t.activeEffects.Exists(e => e.type == dt))).ToList();
        }

        private static BossAction Build(int index, CardSkill skill, CardInstance target)
            => new BossAction { SkillIndex = index, Skill = skill, Target = target };
    }
}
