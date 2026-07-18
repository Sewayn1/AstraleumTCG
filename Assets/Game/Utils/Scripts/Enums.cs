namespace Astraleum
{
    public enum Element
    {
        Feu,
        Eau,
        Terre,
        Air,
        Lumiere,
        Tenebres,
        Astral,
        Corrosif,
        Necrotique
    }

    public enum TriggerElement
    {
        Feu,
        Eau,
        Terre,
        Air,
        Lumiere,
        Tenebres,
        Astral,
        Corrosif,
        Any,  // ← N'importe quel élément sauf Astral
        Necrotique  // ← ajouté après Any pour ne pas décaler sa valeur sérialisée (=8)
    }

    public enum CardRarity
    {
        Commun,
        Rare,
        Epique,
        Legendaire,
        Supreme
    }

    public enum SkillTargetType
    {
        SingleEnemy,
        SingleAlly,
        AllEnemies,
        AllAllies,
        AdjacentEnemies,
        Self
    }

    public enum EffectType
    {
        // Dégâts
        Saignement,         // Saignement : dégâts sur la durée (% PV max/tour)
        DamageAmplify,      // Amplifie dégâts reçus
        DamageReduction,    // Réduit dégâts reçus

        // Soins
        ImmediateHeal,      // Soin immédiat % PV max
        HealOverTime,       // Régénération % PV max/tour
        HealBlock,          // Bloque les soins

        // Armure
        GiveArmor,          // ← NOUVEAU : donne X points d'armure à la cible
        GiveArmorAdjacent,  // ← NOUVEAU : donne X points d'armure aux adjacents alliés
        ArmorIgnore,        // Ignore l'armure lors d'une attaque

        // Stacks
        AddStack,           // Ajoute X stacks d'un élément
        RemoveStack,        // Retire X stacks d'un élément

        // Cooldowns
        CooldownReduction,
        CooldownIncrease,

        // Divers
        AttackBoost,        // Bonus dégâts % temporaire
        AttackReduction,    // Réduit dégâts infligés
        BonusAction,        // Accorde x actions supplémentaires à la carte cible
        Stun,               // Empêche d'agir
        Poison,             // Stack Ténèbres MAJEUR
        Burn,               // Brûlure : % PV max/tour, affecté par armure et DamageReduction
        LifeSteal,          // Vol de Vie : soigne l'attaquant d'un % des DGT infligés
        Invisible,          // Immunité au ciblage direct (SingleEnemy) ; perdu à l'action
        AttackBoostFlat,    // Bonus dégâts fixe temporaire (+N dégâts)
        CritChanceBoost,    // Augmente la chance de coup critique (value = % additionnel, ex. 0.15 = +15%)
        CritDamageBoost,    // Augmente le bonus DGT critique (value = % additif sur le +50% de base)
        MaxHPReduction,     // Réduit les PV Max d'un % (value = 0.05 = -5%)
        ReduceArmor,        // Réduit l'armure d'une carte de N points (value = N, flat)
        AttackReductionFlat, // Réduit DGT infligés de N flat (ex. -3 DGT absolus)
        Cancel,             // Annule immédiatement toutes les incantations en cours
        Inarretable,        // Immunité Stun et Cancel pendant N tours
        SelfDamageAmplify,  // Amplifie les dégâts INFLIGÉS par la carte (lu côté attaquant, ≠ DamageAmplify qui amplifie les dégâts subis)
        Necrose,            // Nécrotique : DGT plat/tour, empile en instances indépendantes (aucune fusion par source)
        HealReduction,      // Nécrotique : réduit les soins reçus d'un % (cumulatif additif, plafonné)
        Noyade,             // Eau (Boss Thalyra) : marqueur pur, empile en instances indépendantes, sert de compteur pour ConditionType.TargetEffectStackCount — TOUJOURS EN DERNIER
    }

    public enum EffectTarget
    {
        Target,
        Self,
        AllAllies,
        AllEnemies,
        RandomAllies,       // Un allié aléatoire
        RandomEnnemies,     // Un ennemi aléatoire
        AdjacentEnemies,    // Cible principale + ses voisins ennemis
    }

    public enum PassiveTrigger
    {
        OnTurnStart,              // Début de tour
        OnCardDestroyed,          // Une carte (alliée ou ennemie) est détruite
        OnAllyDestroyed,          // Un allié est détruit
        WhenThisCardDestroysCard, // ← NOUVEAU : cette carte détruit une carte adverse
        OnStackThreshold3,        // ← NOUVEAU : seuil 3 stacks atteint
        OnStackThreshold5,        // ← NOUVEAU : seuil 5 stacks atteint
        CardIsBurning,            // ← NOUVEAU : dynamique, pas un événement — voir DamageCalculator.Calculate
        ForEachAllyAlive,         // ← NOUVEAU : dynamique, pas un événement — voir DamageCalculator.Calculate
    }
    // ⚠️ TOUJOURS ajouter les nouvelles valeurs de PassiveTrigger EN DERNIER : cet enum est
    // sérialisé comme entier dans CardPassive.trigger sur les .asset de cartes existantes.
}