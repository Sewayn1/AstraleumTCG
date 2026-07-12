using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum
{
    /// <summary>
    /// Journal de combat : trace textuelle de toutes les actions d'une partie (dégâts, soins,
    /// buffs/debuffs, morts de cartes, passifs...). Alimenté par AddEntry() depuis TurnManager,
    /// SkillExecutor, CardInstance, PassiveManager et NetworkGameController.
    /// La fenêtre est masquée par défaut et s'ouvre/se ferme via un bouton persistant (Btn_OpenLog).
    /// </summary>
    public class CombatLogManager : MonoBehaviour
    {
        public static CombatLogManager Instance;

        [Header("Templates")]
        public GameObject logEntryTemplate;
        public GameObject logDeathTemplate;

        [Header("Références")]
        public RectTransform logContent;
        public GameObject    logWindow;
        public TMP_Text      toggleArrow;
        public ScrollRect    logScrollRect;

        [Header("Couleurs joueurs")]
        [SerializeField] private Color player1Color = new Color(0.30f, 0.85f, 0.40f, 1f); // vert (préfixe P1)
        [SerializeField] private Color player2Color = new Color(0.95f, 0.40f, 0.30f, 1f); // rouge (préfixe P2)
        [SerializeField] private Color neutralColor = new Color(1.00f, 1.00f, 1.00f, 1f); // blanc (texte)
        [SerializeField] private Color cardNameColor = new Color(1.00f, 0.84f, 0.00f, 1f); // doré (noms de cartes)
        [SerializeField] private Color deathColor   = new Color(1.00f, 0.25f, 0.25f, 1f); // rouge vif

        private readonly List<GameObject> entries = new List<GameObject>();
        private List<string> cachedCardNames;
        private bool isOpen;

        private void Awake()
        {
            Instance = this;
            if (logWindow != null) logWindow.SetActive(false);
            if (toggleArrow != null) toggleArrow.text = "▲";
        }

        public void ToggleLog()
        {
            isOpen = !isOpen;
            if (logWindow != null) logWindow.SetActive(isOpen);
            if (toggleArrow != null) toggleArrow.text = isOpen ? "▼" : "▲";
        }

        // playerID : 0 = J1 (préfixe "P1" vert), 1 = J2 (préfixe "P2" rouge), -1 = neutre (pas de préfixe)
        public void AddEntry(string message, bool isDeathEntry = false, int playerID = -1)
        {
            if (logContent == null) return;

            var template = isDeathEntry ? logDeathTemplate : logEntryTemplate;
            if (template == null) return;

            var entry = Instantiate(template, logContent);
            entry.SetActive(true);

            var entryTxt = entry.transform.Find("EntryText")?.GetComponent<TMP_Text>();
            if (entryTxt != null)
            {
                string prefix = playerID == 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(player1Color)}>P1</color> " :
                                playerID == 1 ? $"<color=#{ColorUtility.ToHtmlStringRGB(player2Color)}>P2</color> " :
                                "";
                entryTxt.text  = prefix + HighlightCardNames(message);
                entryTxt.color = isDeathEntry ? deathColor : neutralColor;
            }

            entries.Add(entry);
            StartCoroutine(ScrollToBottom());
        }

        // Entoure chaque nom de carte connu d'une balise <color> dorée. Le cache est construit une
        // seule fois (les noms de cartes ne changent pas en cours de partie) et trié par longueur
        // décroissante pour qu'un nom composé (ex. "Drake de Feu") soit remplacé avant un nom plus court.
        private string HighlightCardNames(string message)
        {
            if (cachedCardNames == null)
            {
                cachedCardNames = CardDatabase.LoadVisibleCards()
                    .Select(c => c.cardName)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .OrderByDescending(n => n.Length)
                    .ToList();
            }

            string hex = ColorUtility.ToHtmlStringRGB(cardNameColor);
            foreach (var name in cachedCardNames)
            {
                if (message.Contains(name))
                    message = message.Replace(name, $"<color=#{hex}>{name}</color>");
            }
            return message;
        }

        // Résumé court d'un effet (buff/debuff) pour une entrée de journal — pas de source, pas de
        // saut de ligne (contrairement à BuffTooltipManager qui affiche le détail complet au survol).
        public static string DescribeEffect(EffectType type, float value, int durationTurns, int targetMaxHP = 0)
        {
            string dur = durationTurns == -1 ? "∞" : $"{durationTurns}T";
            switch (type)
            {
                case EffectType.Saignement:          return $"Saignement -{value * 100:0}%/tour ({dur})";
                case EffectType.Poison:              return $"Poison -{value * 100:0}%/tour ({dur})";
                case EffectType.Burn:                return $"Brûlure -{value * 100:0}%/tour ({dur})";
                case EffectType.HealOverTime:
                    int healPerTurn = Mathf.RoundToInt(targetMaxHP * value);
                    return $"Régénération +{healPerTurn} PV/tour ({dur})";
                case EffectType.AttackBoost:
                case EffectType.AttackBoostFlat:     return $"Attaque +{value:0} DGT ({dur})";
                case EffectType.AttackReduction:
                case EffectType.AttackReductionFlat: return $"Attaque -{value:0} DGT ({dur})";
                case EffectType.DamageReduction:     return $"Dégâts subis -{value * 100:0}% ({dur})";
                case EffectType.DamageAmplify:       return $"Dégâts subis +{value * 100:0}% ({dur})";
                case EffectType.SelfDamageAmplify:   return $"Dégâts infligés +{value * 100:0}% ({dur})";
                case EffectType.HealBlock:           return $"Soin bloqué ({dur})";
                case EffectType.Stun:                return $"Étourdi ({dur})";
                case EffectType.Inarretable:         return $"Inarrêtable ({dur})";
                case EffectType.LifeSteal:           return $"Vol de Vie +{value * 100:0}% ({dur})";
                case EffectType.CritChanceBoost:     return $"Chance Critique +{value * 100:0}% ({dur})";
                case EffectType.CritDamageBoost:     return $"Dégâts Critique +{value * 100:0}% ({dur})";
                case EffectType.MaxHPReduction:      return $"PV Max -{value * 100:0}% ({dur})";
                case EffectType.GiveArmor:           return $"Armure +{value:0} ({dur})";
                case EffectType.ReduceArmor:         return $"Armure -{value:0} ({dur})";
                case EffectType.CooldownIncrease:    return $"Recharge +1 tour ({dur})";
                default:                             return $"{type} ({dur})";
            }
        }

        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            if (logScrollRect != null)
                logScrollRect.verticalNormalizedPosition = 0f;
        }

        // Conservé pour compatibilité avec TurnManager/NetworkGameController — le numéro de
        // tour n'est plus affiché dans le journal.
        public void OnTurnChanged(int newTurn) { }
    }
}
