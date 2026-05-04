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
        Astral
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
        Any  // ← N'importe quel élément sauf Astral
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
        AttackBoostFlat,    // Bonus dégâts fixe temporaire (+N dégâts) — TOUJOURS EN DERNIER
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
    }
}