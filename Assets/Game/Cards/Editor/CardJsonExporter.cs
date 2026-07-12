using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Astraleum.Editor
{
    public static class CardJsonExporter
    {
        [MenuItem("Astraleum/Exporter cards.json")]
        public static void Export()
        {
            string outputPath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "AstraleumCore",
                "Astraleum.Server", "CardDefinitions", "cards.json"));

            var cards = Resources.LoadAll<CardData>("Cards/FR");
            System.Array.Sort(cards, (a, b) => a.cardNumber.CompareTo(b.cardNumber));

            var entries = new List<string>(cards.Length);
            foreach (var c in cards)
                entries.Add(CardToJson(c));

            string json = "[\n" + string.Join(",\n", entries) + "\n]";

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, json, new UTF8Encoding(false));

            Debug.Log($"[CardExporter] {cards.Length} cartes exportées → {outputPath}");
        }

        // ── Card ──────────────────────────────────────────────────────────────────────

        static string CardToJson(CardData c)
        {
            var lines = new List<string>
            {
                L(4, "cardName",            JS(c.cardName)),
                L(4, "cardTitle",           JS(c.cardTitle)),
                L(4, "cardNumber",          c.cardNumber.ToString()),
                L(4, "element",             EI(c.element)),
                L(4, "rarity",              EI(c.rarity)),
                L(4, "maxHP",               c.maxHP.ToString()),
                L(4, "armorPoints",         c.armorPoints.ToString()),
                L(4, "critChance",          JF(c.critChance)),
                L(4, "bonusActionsGranted", c.bonusActionsGranted.ToString()),
                L(4, "skillOne",            SkillToJson(c.skillOne,  4)),
                L(4, "skillTwo",            SkillToJson(c.skillTwo,  4)),
                L(4, "passive",             PassiveToJson(c.passive, 4)),
                L(4, "loreText",            JS(c.loreText)),
                L(4, "loreQuote",           JS(c.loreQuote)),
            };
            return P(2) + "{\n" + string.Join(",\n", lines) + "\n" + P(2) + "}";
        }

        // ── Skill ─────────────────────────────────────────────────────────────────────

        static string SkillToJson(CardSkill s, int indent)
        {
            if (s == null || string.IsNullOrEmpty(s.skillName))
                return "null";

            int c = indent + 2;
            var lines = new List<string>
            {
                L(c, "skillType",             EI(s.skillType)),
                L(c, "skillName",             JS(s.skillName)),
                L(c, "description",           JS(s.description)),
                L(c, "damage",                s.damage.ToString()),
                L(c, "cooldownTurns",         s.cooldownTurns.ToString()),
                L(c, "targetType",            EI(s.targetType)),
                L(c, "adjacentDamagePercent", JF(s.adjacentDamagePercent)),
                L(c, "isIncantation",         s.isIncantation ? "true" : "false"),
                L(c, "castDelayTurns",        s.castDelayTurns.ToString()),
                L(c, "effects",               EffectsToJson(s.effects, c)),
                L(c, "branches",              BranchesToJson(s.branches, c)),
            };
            return "{\n" + string.Join(",\n", lines) + "\n" + P(indent) + "}";
        }

        // ── Passive ───────────────────────────────────────────────────────────────────

        static string PassiveToJson(CardPassive p, int indent)
        {
            if (p == null || string.IsNullOrEmpty(p.passiveDescription))
                return "null";

            int c = indent + 2;
            var lines = new List<string>
            {
                L(c, "passiveName",        JS(p.passiveName)),
                L(c, "passiveDescription", JS(p.passiveDescription)),
                L(c, "trigger",            EI(p.trigger)),
                L(c, "triggerElement",     EI(p.triggerElement)),
                L(c, "stacksPerTrigger",   p.stacksPerTrigger ? "true" : "false"),
                L(c, "maxTriggerStacks",   p.maxTriggerStacks.ToString()),
                L(c, "effects",            EffectsToJson(p.effects, c)),
            };
            return "{\n" + string.Join(",\n", lines) + "\n" + P(indent) + "}";
        }

        // ── Effects ───────────────────────────────────────────────────────────────────

        static string EffectsToJson(List<CardEffect> effects, int indent)
        {
            if (effects == null || effects.Count == 0) return "[]";

            int c = indent + 2;
            int cc = indent + 4;
            var items = new List<string>();
            foreach (var e in effects)
            {
                var lines = new List<string>
                {
                    L(cc, "type",          EI(e.type)),
                    L(cc, "value",         JF(e.value)),
                    L(cc, "durationTurns", e.durationTurns.ToString()),
                    L(cc, "effectTarget",  EI(e.effectTarget)),
                };
                items.Add(P(c) + "{\n" + string.Join(",\n", lines) + "\n" + P(c) + "}");
            }
            return "[\n" + string.Join(",\n", items) + "\n" + P(indent) + "]";
        }

        // ── Branches ──────────────────────────────────────────────────────────────────

        static string BranchesToJson(List<ConditionalBranch> branches, int indent)
        {
            if (branches == null || branches.Count == 0) return "[]";

            int c  = indent + 2;
            int cc = indent + 4;
            int ccc = indent + 6;
            var items = new List<string>();
            foreach (var b in branches)
            {
                var condLines = new List<string>
                {
                    L(ccc, "conditionType",    EI(b.condition.conditionType)),
                    L(ccc, "compareOp",        EI(b.condition.compareOp)),
                    L(ccc, "threshold",        JF(b.condition.threshold)),
                    L(ccc, "effectType",       EI(b.condition.effectType)),
                    L(ccc, "conditionElement", EI(b.condition.conditionElement)),
                };
                string condJson = "{\n" + string.Join(",\n", condLines) + "\n" + P(cc) + "}";

                var bLines = new List<string>
                {
                    L(cc, "condition",    condJson),
                    L(cc, "effectType",   EI(b.effectType)),
                    L(cc, "target",       EI(b.target)),
                    L(cc, "valueMode",    EI(b.valueMode)),
                    L(cc, "valuePercent", JF(b.valuePercent)),
                    L(cc, "valueFlat",    b.valueFlat.ToString()),
                    L(cc, "durationTurns", b.durationTurns.ToString()),
                };
                items.Add(P(c) + "{\n" + string.Join(",\n", bLines) + "\n" + P(c) + "}");
            }
            return "[\n" + string.Join(",\n", items) + "\n" + P(indent) + "]";
        }

        // ── Primitive helpers ─────────────────────────────────────────────────────────

        static string P(int n) => new string(' ', n);

        static string L(int indent, string key, string value) =>
            P(indent) + "\"" + key + "\": " + value;

        static string EI(System.Enum e) =>
            System.Convert.ToInt32(e).ToString();

        static string JS(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\")
                           .Replace("\"",  "\\\"")
                           .Replace("\n",  "\\n")
                           .Replace("\r",  "\\r")
                           .Replace("\t",  "\\t") + "\"";
        }

        static string JF(float f) =>
            f.ToString("G", CultureInfo.InvariantCulture);
    }
}
