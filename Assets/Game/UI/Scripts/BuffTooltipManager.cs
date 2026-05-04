using System.Text;
using UnityEngine;
using TMPro;

namespace Astraleum
{
    public class BuffTooltipManager : MonoBehaviour
    {
        public static BuffTooltipManager Instance;

        [Header("Références")]
        public GameObject tooltipPanel;
        public TMP_Text   titleText;
        public TMP_Text   contentText;

        [Header("Couleurs")]
        [SerializeField] private Color sourceColor = new Color(0.96f, 0.78f, 0.25f); // or doré

        [Header("Position")]
        [SerializeField] private float rightMargin = 10f; // distance au bord droit (px canvas)

        private void Awake() => Instance = this;

        // ── API publique ──────────────────────────────────────────────

        public void Show(CardInstance card, RectTransform cardRect)
        {
            if (tooltipPanel == null || card == null || card.data == null) return;

            string content = BuildContent(card);
            if (string.IsNullOrEmpty(content)) return;

            if (titleText != null)
                titleText.text = card.data.cardName;

            if (contentText != null)
                contentText.text = content;

            // Ancrage fixe sur le bord droit de l'écran
            var rt = tooltipPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin        = new Vector2(1f, 0.5f);
                rt.anchorMax        = new Vector2(1f, 0.5f);
                rt.pivot            = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-rightMargin, 0f);
            }

            tooltipPanel.SetActive(true);
        }

        public void Hide()
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }

        // ── Construction du contenu ───────────────────────────────────

        private string BuildContent(CardInstance card)
        {
            var sb    = new StringBuilder();
            bool first = true;

            // Effets actifs (skills, passifs déclenchés)
            foreach (var effect in card.activeEffects)
            {
                if (!first) sb.Append('\n');
                sb.Append(FormatActiveEffect(effect, card));
                first = false;
            }

            // Effets passifs conditionnels (seuils de stacks)
            foreach (var cpe in card.conditionalPassiveEffects)
            {
                if (!first) sb.Append('\n');
                sb.Append(FormatCPE(cpe, card));
                first = false;
            }

            // HoT Lumière (stacks 3/5) — calculé dynamiquement, pas stocké en ActiveEffect
            if (StackManager.Instance != null)
            {
                float lightHoT = StackManager.Instance.GetLightHoTPercent(card.ownerPlayerID);
                if (lightHoT > 0f)
                {
                    int healPerTurn = Mathf.RoundToInt(card.data.maxHP * lightHoT);
                    int stacks = StackManager.Instance.GetStacks(card.ownerPlayerID, Element.Lumiere);
                    string seuil = stacks >= 5
                        ? LocalizationManager.Get("buff_light_seuil5")
                        : LocalizationManager.Get("buff_light_seuil3");
                    if (!first) sb.Append('\n');
                    sb.Append(LocalizationManager.Get("buff_hot_line", healPerTurn, $"{lightHoT * 100:0}", "∞", ColoredSource(seuil)));
                    first = false;
                }
            }

            return sb.ToString();
        }

        // ── Formatage ─────────────────────────────────────────────────

        private string ColoredSource(string name)
        {
            string hex = ColorUtility.ToHtmlStringRGB(sourceColor);
            string s   = string.IsNullOrEmpty(name) ? "?" : name;
            return $"<color=#{hex}>{s}</color>";
        }

        private string FormatActiveEffect(ActiveEffect e, CardInstance card)
        {
            string dur = e.remainingTurns == -1 ? "∞" : LocalizationManager.Get("buff_dur_turns", e.remainingTurns);
            string src = ColoredSource(e.sourceName);

            switch (e.type)
            {
                case EffectType.Saignement:
                    return LocalizationManager.Get("buff_dot_line", $"{e.value * 100:0}", dur, src);

                case EffectType.Poison:
                    return LocalizationManager.Get("buff_poison_line", $"{e.value * 100:0}", dur, src);

                case EffectType.Burn:
                    return LocalizationManager.Get("buff_burn_line", $"{e.value * 100:0}", dur, src);

                case EffectType.HealOverTime:
                {
                    int healPerTurn = Mathf.RoundToInt(card.data.maxHP * e.value);
                    return LocalizationManager.Get("buff_hot_line", healPerTurn, $"{e.value * 100:0}", dur, src);
                }

                case EffectType.AttackBoost:
                    return LocalizationManager.Get("buff_atk_line", $"{e.value * 100:0}", dur, src);

                case EffectType.AttackBoostFlat:
                    return LocalizationManager.Get("buff_atkflat_line", $"{e.value:0}", dur, src);

                case EffectType.AttackReduction:
                    return LocalizationManager.Get("buff_atkred_line", $"{e.value * 100:0}", dur, src);

                case EffectType.DamageReduction:
                    return LocalizationManager.Get("buff_dmgred_line", $"{e.value * 100:0}", dur, src);

                case EffectType.DamageAmplify:
                    return LocalizationManager.Get("buff_dmgamp_line", $"{e.value * 100:0}", dur, src);

                case EffectType.HealBlock:
                    return LocalizationManager.Get("buff_healblock_line", dur, src);

                case EffectType.Stun:
                    return LocalizationManager.Get("buff_stun_line", dur, src);

                case EffectType.CooldownIncrease:
                    return LocalizationManager.Get("buff_cdinc_line", $"{e.value:0}", dur, src);

                case EffectType.CooldownReduction:
                    return LocalizationManager.Get("buff_cddec_line", $"{e.value:0}", dur, src);

                case EffectType.LifeSteal:
                    return LocalizationManager.Get("buff_lifesteal_line", $"{e.value * 100:0}", dur, src);

                case EffectType.Invisible:
                    return LocalizationManager.Get("buff_invisible_line", src);

                default:
                    return $"{e.type} ({dur})\n  via {src}";
            }
        }

        private string FormatCPE(CardInstance.ConditionalPassiveEffect cpe, CardInstance card)
        {
            string src     = ColoredSource(cpe.sourceName);
            string elemName = LocalizationManager.Get($"ui_element_{cpe.triggerElement.ToString().ToLower()}");
            string seuil   = LocalizationManager.Get("buff_cpe_seuil", cpe.requiredThreshold, elemName);

            switch (cpe.type)
            {
                case EffectType.Saignement:
                    return LocalizationManager.Get("buff_dot_line", $"{cpe.value * 100:0}", seuil, src);

                case EffectType.HealOverTime:
                {
                    int healPerTurn = Mathf.RoundToInt(card.data.maxHP * cpe.value);
                    return LocalizationManager.Get("buff_hot_line", healPerTurn, $"{cpe.value * 100:0}", seuil, src);
                }

                case EffectType.DamageReduction:
                    return LocalizationManager.Get("buff_dmgred_line", $"{cpe.value * 100:0}", seuil, src);

                case EffectType.AttackBoost:
                    return LocalizationManager.Get("buff_atk_line", $"{cpe.value * 100:0}", seuil, src);

                case EffectType.AttackReduction:
                    return LocalizationManager.Get("buff_atkred_line", $"{cpe.value * 100:0}", seuil, src);

                default:
                    return $"{cpe.type} ({seuil})\n  via {src}";
            }
        }
    }
}
