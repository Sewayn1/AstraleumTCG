using System.Collections.Generic;
using UnityEngine;

namespace Astraleum
{

    public enum SkillType
    {
        Attack,   // Dégâts directs
        Heal,     // Soin
        Buff,     // Bonus temporaire sur allié
        Debuff,   // Malus sur ennemi
        Mixed     // Dégâts + effet secondaire
    }

    [System.Serializable]
    public class CardSkill
    {
        [Tooltip("Type de compétence — détermine le comportement de la preview et les cibles valides.")]
        public SkillType skillType = SkillType.Attack;

        public string skillName;
        [TextArea] public string description;
        public int damage;
        public int cooldownTurns;

        [Tooltip("Type de ciblage. Choisir AdjacentEnemies pour activer adjacentDamagePercent.")]
        public SkillTargetType targetType = SkillTargetType.SingleEnemy;

        [Range(0f, 1f)]
        [Tooltip("Dégâts infligés aux cartes adjacentes en % des dégâts principaux. Actif uniquement si targetType = AdjacentEnemies. (0.25 = 25%)")]
        public float adjacentDamagePercent = 0f;

        public List<CardEffect> effects = new List<CardEffect>();

        [Header("Branches conditionnelles")]
        [Tooltip("Effets supplémentaires appliqués si une condition est remplie au moment de l'impact.")]
        public List<ConditionalBranch> branches = new List<ConditionalBranch>();

        [Header("Effets visuels")]
        [Tooltip("Particule jouée sur la carte attaquante au déclenchement de la compétence.")]
        public GameObject attackVFXPrefab;
        [Tooltip("Particule jouée sur la/les cible(s) au moment de l'impact.")]
        public GameObject impactVFXPrefab;
        [Tooltip("Délai en secondes entre le lancement de l'attaque et l'impact (temps de vol). Les dégâts sont appliqués après ce délai.")]
        public float vfxTravelTime = 0.35f;
        [Tooltip("Durée en secondes après l'impact avant de passer à la suite (laisser l'effet d'impact visible).")]
        public float vfxImpactDuration = 0.2f;


        public bool TargetsAllies =>
            targetType == SkillTargetType.SingleAlly ||
            targetType == SkillTargetType.AllAllies ||
            targetType == SkillTargetType.Self;

        public bool IsHealSkill => damage == 0 && TargetsAllies;

        // Retourne la valeur de soin immédiat (ImmediateHeal) en % des PV max
        public float GetImmediateHealPercent()
        {
            foreach (var eff in effects)
                if (eff.type == EffectType.ImmediateHeal && eff.durationTurns != -1)
                    return eff.value;
            return 0f;
        }

        // Retourne la valeur de régénération par tour (HealOverTime) en % des PV max
        public float GetHealOverTimePercent()
        {
            foreach (var eff in effects)
                if (eff.type == EffectType.HealOverTime)
                    return eff.value;
            return 0f;
        }

        // Retourne le nombre de tours du HealOverTime
        public int GetHealOverTimeDuration()
        {
            foreach (var eff in effects)
                if (eff.type == EffectType.HealOverTime)
                    return eff.durationTurns;
            return 0;
        }

        // Retourne la valeur de drain (ImmediateHeal avec duration = -1)
        public float GetDrainPercent()
        {
            foreach (var eff in effects)
                if (eff.type == EffectType.ImmediateHeal && eff.durationTurns == -1)
                    return eff.value;
            return 0f;
        }

        public float GetArmorIgnorePercent()
        {
            foreach (var eff in effects)
                if (eff.type == EffectType.ArmorIgnore)
                    return eff.value;
            return 0f;
        }
    }

    [System.Serializable]
    public class CardEffect
    {
        public EffectType type;

        [Tooltip("Valeur de l'effet.\n— Effets en % (DamageAmplify, DamageReduction, AttackBoost, ImmediateHeal, HealOverTime, Poison, Burn, Saignement, ArmorIgnore) : décimal entre 0 et 1 (ex. 0.05 = 5% PV max/tour).\n— Effets en valeur absolue (GiveArmor, CooldownReduction, CooldownIncrease) : valeur entière.")]
        public float value;

        [Tooltip("Durée en tours.\n— Valeur positive : dure N tours.\n— -1 : permanent (infini).\n— 0 : NE PAS UTILISER.")]
        public int durationTurns;

        public EffectTarget effectTarget;

        // ← NOUVEAU : tag passif (non sérialisé — utilisé en runtime uniquement)
        [System.NonSerialized]
        public PassiveTrigger? sourcePassiveTrigger = null;
        [System.NonSerialized]
        public Element sourceElement = Element.Feu;
        [System.NonSerialized]
        public string sourceSkillName = "";  // Nom de la compétence source (runtime uniquement)
    }

    [System.Serializable]
    public class ActiveEffect
    {
        public EffectType type;
        public float value;
        public int remainingTurns;
        public string sourceName = "";   // Nom de la carte source (affiché dans le tooltip buff)

        public string sourceSkillName = "";  // Nom de la compétence source
        // ← Stocké comme int pour être sérialisable par Unity
        public int passiveTriggerID = -1; // -1 = pas de passif
        public int passiveElementID = -1;

        // Propriétés helper pour lire/écrire proprement
        public PassiveTrigger? sourcePassiveTrigger
        {
            get => passiveTriggerID >= 0 ? (PassiveTrigger?)passiveTriggerID : null;
            set => passiveTriggerID = value.HasValue ? (int)value.Value : -1;
        }

        public Element sourceElement
        {
            get => passiveElementID >= 0 ? (Element)passiveElementID : Element.Feu;
            set => passiveElementID = (int)value;
        }
    }
}