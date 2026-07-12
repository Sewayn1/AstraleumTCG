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
        [Tooltip("Si coché, cette carte n'apparaît jamais dans la Collection ni dans le pool de deckbuilding IA (réservée à un usage interne, ex. mode Entraînement).")]
        public bool hiddenFromCollection = false;
        [Tooltip("Si coché : PV masqués à l'affichage (case vierge) et régénération complète à chaque tour. Réservé aux cartes d'entraînement (ex. Card_AITraining).")]
        public bool isTrainingDummy = false;

        [Header("Stats")]
        public int maxHP = 100;
        [Tooltip("Armure permanente de la carte. Réduit les DGT subis d'un montant fixe par attaque (DGT réels = max(0, DGT - Armure)). Les Compétences Ignore-Armure contournent cette réduction.")]
        public int armorPoints = 0;
        [Tooltip("Chance de coup critique de base (0 par défaut). Peut être augmentée via Buff / Passif / Stacks. 0.15 = 15%.")]
        [Range(0f, 1f)] public float critChance = 0f;
        [Tooltip("Actions supplémentaires accordées par tour. 0 par défaut. Utilisé pour les effets spéciaux de carte (ex. stacks Air 5).")]
        public int bonusActionsGranted = 0;

        [Header("Compétences")]
        public CardSkill skillOne;
        public CardSkill skillTwo;
        [Tooltip("3e compétence, réservée aux cartes Boss. Non utilisée par le SkillPanel joueur ni l'IA standard — pilotée directement par le contrôleur du Boss.")]
        public CardSkill skillThree;

        // CardSkill est une classe [Serializable] embarquée (pas un UnityEngine.Object) : une fois
        // l'asset resauvegardé dans l'éditeur après l'ajout de ce champ, Unity matérialise un objet
        // vide (skillName="") au lieu de conserver null. Un simple "skillThree != null" ne suffit
        // donc PAS à détecter une vraie 3e compétence — toujours utiliser HasSkillThree à la place.
        public bool HasSkillThree => skillThree != null && !string.IsNullOrEmpty(skillThree.skillName);

        [Header("Passif")]
        public CardPassive passive;

        [Header("Lore")]
        [TextArea] public string loreText;
        [TextArea] public string loreQuote;
    }
}