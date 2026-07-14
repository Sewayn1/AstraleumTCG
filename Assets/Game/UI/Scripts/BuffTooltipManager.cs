using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Astraleum
{
    public class BuffTooltipManager : MonoBehaviour
    {
        public static BuffTooltipManager Instance;

        [Header("Couleurs")]
        [SerializeField] private Color sourceColor = new Color(0.96f, 0.78f, 0.25f);

        private void Awake() => Instance = this;

        // ── API publique ──────────────────────────────────────────────

        public void Show(CardInstance card, RectTransform _)
        {
            if (card == null || card.data == null) return;
            string content = BuildContent(card);

            if (!string.IsNullOrEmpty(content))
            {
                TooltipSystem.Instance?.Show(card.data.cardName, content, TooltipAnchor.RightEdge, 11f);
                return;
            }

            // Fallback : affiche la description du passif statique si aucun effet actif
            var passive = card.data.passive;
            if (passive != null && !string.IsNullOrEmpty(passive.passiveDescription))
            {
                string hex   = ColorUtility.ToHtmlStringRGB(passive.passiveColor);
                string title = $"<color=#{hex}>{passive.passiveName ?? "Passif"}</color>";
                TooltipSystem.Instance?.Show(title, TMPIconReplacer.Apply(passive.passiveDescription), TooltipAnchor.RightEdge, 11f);
            }
        }

        public void Hide() => TooltipSystem.Instance?.Hide();

        // Formate uniquement les instances actives d'un EffectType donné (utilisé par les tooltips d'icônes de statut).
        // Plusieurs instances du même type sont fusionnées sur une seule ligne (voir MergeEffects).
        public string GetEffectTooltip(CardInstance card, EffectType type)
        {
            if (card == null) return "";

            var matching = card.activeEffects.Where(e => e.type == type).ToList();
            return matching.Count == 0 ? "" : FormatActiveEffect(MergeEffects(matching), card);
        }

        // ── Construction du contenu ───────────────────────────────────

        private string BuildContent(CardInstance card)
        {
            var sb    = new StringBuilder();
            bool first = true;

            // Regroupe les effets similaires (même famille d'affichage) sur une seule ligne.
            // Recalculé à chaque appel depuis card.activeEffects : reflète toujours les
            // instances actuellement actives (celles expirées ont déjà été retirées ailleurs).
            foreach (var group in card.activeEffects.GroupBy(e => GetTooltipGroupKey(e.type)))
            {
                if (!first) sb.Append('\n');
                sb.Append(FormatActiveEffect(MergeEffects(group.ToList()), card));
                first = false;
            }

            // Incantations en cours
            if (card.pendingIncantations != null)
                foreach (var incant in card.pendingIncantations)
                {
                    if (!first) sb.Append('\n');
                    string src = ColoredSource(card.data?.cardName ?? "?");
                    sb.Append(LocalizationManager.Get("buff_incanting_line",
                        incant.skill?.skillName ?? "?",
                        incant.turnsRemaining,
                        src));
                    first = false;
                }

            if (StackManager.Instance != null)
            {
                float lightHoT = StackManager.Instance.GetLightHoTPercent(card.ownerPlayerID);
                if (lightHoT > 0f)
                {
                    int healPerTurn = Mathf.RoundToInt(card.EffectiveMaxHP * lightHoT);
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

        // ── Fusion des effets similaires ──────────────────────────────

        // AttackBoost/AttackBoostFlat et AttackReduction/AttackReductionFlat partagent déjà
        // le même format d'affichage (buff_atkflat_line / buff_atkredflat_line) — on les
        // regroupe donc ensemble en plus des instances strictement du même EffectType.
        private static string GetTooltipGroupKey(EffectType type) => type switch
        {
            EffectType.AttackBoost or EffectType.AttackBoostFlat         => "AttackBoost",
            EffectType.AttackReduction or EffectType.AttackReductionFlat => "AttackReduction",
            _ => type.ToString()
        };

        // Fusionne plusieurs instances d'un même groupe en une seule entrée : valeurs
        // sommées (cohérent avec DamageCalculator/GetEffectiveCritChance/etc. qui font de
        // même), durée = la plus longue restante (∞ si au moins une instance est permanente),
        // sources listées. Un seul FormatActiveEffect() suffit ensuite pour l'affichage.
        private ActiveEffect MergeEffects(List<ActiveEffect> effects)
        {
            if (effects.Count == 1) return effects[0];

            float sum = effects.Sum(e => e.value);
            bool anyInfinite = effects.Any(e => e.remainingTurns == -1);
            int maxFinite = effects.Where(e => e.remainingTurns != -1)
                                   .Select(e => e.remainingTurns)
                                   .DefaultIfEmpty(0)
                                   .Max();

            var sources = effects
                .Select(e => string.IsNullOrEmpty(e.sourceName) ? "?" : e.sourceName)
                .Distinct();

            return new ActiveEffect
            {
                type            = effects[0].type,
                value           = sum,
                remainingTurns  = anyInfinite ? -1 : maxFinite,
                sourceName      = string.Join(", ", sources),
                sourceSkillName = effects[0].sourceSkillName,
            };
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

            string raw;
            switch (e.type)
            {
                case EffectType.Saignement:
                    raw = LocalizationManager.Get("buff_dot_line", $"{e.value * 100:0}", dur, src); break;
                case EffectType.Poison:
                    raw = LocalizationManager.Get("buff_poison_line", $"{e.value * 100:0}", dur, src); break;
                case EffectType.Burn:
                    raw = LocalizationManager.Get("buff_burn_line", $"{e.value * 100:0}", dur, src); break;
                case EffectType.HealOverTime:
                {
                    int healPerTurn = Mathf.RoundToInt(card.EffectiveMaxHP * e.value);
                    raw = LocalizationManager.Get("buff_hot_line", healPerTurn, $"{e.value * 100:0}", dur, src); break;
                }
                case EffectType.AttackBoost:
                case EffectType.AttackBoostFlat:
                    raw = LocalizationManager.Get("buff_atkflat_line", $"{e.value:0}", dur, src); break;
                case EffectType.AttackReduction:
                case EffectType.AttackReductionFlat:
                    raw = LocalizationManager.Get("buff_atkredflat_line", $"{e.value:0}", dur, src); break;
                case EffectType.DamageReduction:
                    raw = LocalizationManager.Get("buff_dmgred_line", $"{e.value * 100:0}", dur, src); break;
                case EffectType.DamageAmplify:
                    raw = LocalizationManager.Get("buff_dmgamp_line", $"{e.value * 100:0}", dur, src); break;
                case EffectType.HealBlock:
                    raw = LocalizationManager.Get("buff_healblock_line", dur, src); break;
                case EffectType.Stun:
                    raw = LocalizationManager.Get("buff_stun_line", dur, src); break;
                case EffectType.Inarretable:
                    raw = LocalizationManager.Get("buff_inarretable_line", dur, src); break;
                case EffectType.CooldownIncrease:
                    raw = LocalizationManager.Get("buff_cdinc_line", dur, src); break;
                case EffectType.CooldownReduction:
                    raw = LocalizationManager.Get("buff_cddec_line", $"{e.value:0}", dur, src); break;
                case EffectType.LifeSteal:
                    raw = LocalizationManager.Get("buff_lifesteal_line", $"{e.value * 100:0}", dur, src); break;
                case EffectType.GiveArmor:
                    raw = LocalizationManager.Get("buff_armor_line", $"{e.value:0}", dur, src); break;
                case EffectType.Invisible:
                    raw = LocalizationManager.Get("buff_invisible_line", src); break;
                case EffectType.CritChanceBoost:
                    raw = LocalizationManager.Get("buff_critchance_line", $"{e.value * 100:0}", dur, src); break;
                case EffectType.CritDamageBoost:
                    raw = LocalizationManager.Get("buff_critdmg_line", $"{e.value * 100:0}", dur, src); break;
                case EffectType.MaxHPReduction:
                    raw = LocalizationManager.Get("buff_maxhpreduction_line", $"{e.value * 100:0}", dur, src); break;
                case EffectType.ReduceArmor:
                    raw = LocalizationManager.Get("buff_armorred_line", $"{e.value:0}", dur, src); break;
                case EffectType.Necrose:
                    raw = LocalizationManager.Get("buff_necrose_line", $"{e.value:0}", dur, src); break;
                default:
                    raw = $"{e.type} ({dur})\n  via {src}"; break;
            }
            return TMPIconReplacer.Apply(raw);
        }

        private string FormatCPE(CardInstance.ConditionalPassiveEffect cpe, CardInstance card)
        {
            string src      = ColoredSource(cpe.sourceName);
            string elemName = LocalizationManager.Get($"ui_element_{cpe.triggerElement.ToString().ToLower()}");
            string seuil    = LocalizationManager.Get("buff_cpe_seuil", cpe.requiredThreshold, elemName);

            switch (cpe.type)
            {
                case EffectType.Saignement:
                    return LocalizationManager.Get("buff_dot_line", $"{cpe.value * 100:0}", seuil, src);
                case EffectType.HealOverTime:
                {
                    int healPerTurn = Mathf.RoundToInt(card.EffectiveMaxHP * cpe.value);
                    return LocalizationManager.Get("buff_hot_line", healPerTurn, $"{cpe.value * 100:0}", seuil, src);
                }
                case EffectType.DamageReduction:
                    return LocalizationManager.Get("buff_dmgred_line", $"{cpe.value * 100:0}", seuil, src);
                case EffectType.AttackBoost:
                    return LocalizationManager.Get("buff_atkflat_line", $"{cpe.value:0}", seuil, src);
                case EffectType.AttackReduction:
                    return LocalizationManager.Get("buff_atkredflat_line", $"{cpe.value:0}", seuil, src);
                default:
                    return $"{cpe.type} ({seuil})\n  via {src}";
            }
        }
    }
}
