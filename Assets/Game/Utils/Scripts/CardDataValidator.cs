using UnityEngine;

namespace Astraleum
{
    public class CardDataValidator : MonoBehaviour
    {
        private void Start()
        {
            if (CardDatabase.Instance == null) return;

            foreach (var card in CardDatabase.Instance.GetAllCards())
            {
                ValidateSkill(card, card.skillOne,  "Skill1");
                ValidateSkill(card, card.skillTwo,  "Skill2");
            }
        }

        private void ValidateSkill(CardData card, CardSkill skill, string label)
        {
            if (skill == null) return;
            foreach (var eff in skill.effects)
            {
                if (IsPercentEffect(eff.type) && eff.value > 1f)
                {
                    Debug.LogError(
                        $"[Validation] {card.cardName} — {label} ({skill.skillName}) : " +
                        $"effet {eff.type} a value={eff.value} " +
                        $"→ devrait être {eff.value / 100f:F2}");
                }
            }
        }

        private bool IsPercentEffect(EffectType type)
        {
            return type == EffectType.DamageAmplify    ||
                   type == EffectType.DamageReduction  ||
                   type == EffectType.AttackBoost      ||
                   type == EffectType.HealOverTime     ||
                   type == EffectType.ImmediateHeal    ||
                   type == EffectType.ArmorIgnore      ||
                   type == EffectType.Poison;
        }
    }
}