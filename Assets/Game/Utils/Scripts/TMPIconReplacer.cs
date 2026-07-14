using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace Astraleum
{
    public static class TMPIconReplacer
    {
        static TMP_SpriteAsset _asset;

        static TMP_SpriteAsset Asset
        {
            get
            {
                if (_asset == null)
                    _asset = Resources.Load<TMP_SpriteAsset>("TMP_Icons/AstralanIcons");
                return _asset;
            }
        }

        static readonly (string keyword, string spriteName)[] _rules =
        {
            ("DGT",  "dgt"),
            ("PV",   "pv"),
            ("HEAL", "heal"),
            ("ARM",  "arm"),
            ("BURN",  "burn"),
            ("NEC",   "nec"),
        };

        // ── API publique ──────────────────────────────────────────────────────

        // Remplace les mots-clés → tags <sprite>
        public static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var (kw, name) in _rules)
                text = text.Replace(kw, $"<sprite name=\"{name}\">");
            return text;
        }

        // Assigne le sprite asset + texte transformé (sans coloration dynamique)
        public static void ApplyTo(TMP_Text field, string text)
        {
            if (field == null) return;
            if (Asset != null) field.spriteAsset = Asset;
            field.text = Apply(text);
        }

        // Assigne le sprite asset + texte + coloration dynamique des valeurs numériques
        // selon les modificateurs de la carte (DGT vert/rouge, HEAL vert/rouge).
        public static void ApplyWithModifiers(TMP_Text field, string text, CardInstance card)
        {
            if (field == null) return;
            if (Asset != null) field.spriteAsset = Asset;

            string processed = Apply(text);

            if (card != null)
            {
                const float threshold = 0.005f;

                float dgtMod  = DamageCalculator.GetAttackerModifier(card);
                float dgtFlat = DamageCalculator.GetAttackerFlatBonus(card);
                if (Mathf.Abs(dgtMod - 1f) > threshold || dgtFlat != 0f)
                    processed = ColorValuesBefore(processed, "dgt", dgtMod, dgtFlat);

                float healMod = DamageCalculator.GetHealModifier(card);
                if (Mathf.Abs(healMod - 1f) > threshold)
                    processed = ColorValuesBefore(processed, "heal", healMod);
            }

            field.text = processed;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // Remplace les entiers précédant un sprite par la valeur calculée (base × mod + flat), colorée.
        // Les pourcentages (ex : "15%") sont ignorés — ce sont des modificateurs, pas des DGT de base.
        static string ColorValuesBefore(string text, string spriteName, float modifier, float flatBonus = 0f)
        {
            string pattern = @"(\d+)\s*(<sprite name=""" + spriteName + @""">)";

            return Regex.Replace(text, pattern, m =>
            {
                if (!int.TryParse(m.Groups[1].Value, out int baseVal)) return m.Value;
                int calcVal = Mathf.RoundToInt(baseVal * modifier + flatBonus);
                if (calcVal == baseVal) return m.Value;
                string hex = calcVal > baseVal ? "2ECC71" : "E74C3C";
                return $"<color=#{hex}>{calcVal}</color> {m.Groups[2].Value}";
            });
        }
    }
}
