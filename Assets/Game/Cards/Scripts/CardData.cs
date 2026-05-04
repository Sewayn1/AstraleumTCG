using UnityEngine;
using System.Collections.Generic;
namespace Astraleum
{
    [System.Serializable]
    public class CardPassive
    {
        public string passiveName;
        public string passiveDescription;
        public PassiveTrigger trigger;
        public TriggerElement triggerElement; // ← TriggerElement au lieu de Element
        [Tooltip("Couleur du nom du passif dans l'infobulle (tooltip). Or par défaut.")]
        public Color passiveColor = new Color(1f, 0.84f, 0f, 1f);
        [Tooltip("Si coché, l'effet s'accumule à chaque déclenchement (max maxTriggerStacks fois). Utiliser avec OnAllyDestroyed pour un passif 'x alliés de type X détruits'.")]
        public bool stacksPerTrigger = false;
        [Tooltip("Nombre maximum de déclenchements cumulables (défaut : 4). Actif uniquement si stacksPerTrigger est coché.")]
        public int maxTriggerStacks = 4;
        public List<CardEffect> effects = new List<CardEffect>();
    }

    [CreateAssetMenu(fileName = "NewCard", menuName = "Astraleum/Card")]
    public class CardData : ScriptableObject
    {
        [Header("Identité")]
        public string cardName;
        [Tooltip("Sous-titre affiché sous le nom de la carte (ex. « Gardien des Flammes »).")]
        public string cardTitle;
        [Tooltip("Numéro unique de la carte dans le roster.")]
        public int cardNumber;
        public Element element;
        public CardRarity rarity;
        public Sprite artwork;

        [Header("Stats")]
        public int maxHP = 100;
        [Tooltip("Points d'armure initiaux. L'armure est un pool de PV supplémentaires absorbés avant les PV. Plafond en jeu : 100 pts.")]
        public int armorPoints = 0;
        [Tooltip("Actions supplémentaires accordées par tour. 0 par défaut. Utilisé pour les effets spéciaux de carte (ex. stacks Air 5).")]
        public int bonusActionsGranted = 0;

        [Header("Compétences")]
        public CardSkill skillOne;
        public CardSkill skillTwo;

        [Header("Passif")]
        public CardPassive passive;

        [Header("Lore")]
        [TextArea] public string loreText;
        [TextArea] public string loreQuote;
    }
}