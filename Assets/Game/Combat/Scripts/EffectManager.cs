using UnityEngine;

namespace Astraleum
{
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance;
        private void Awake() => Instance = this;

        public void ApplyStackEffect(int playerID, Element element, int stacks)
        {
            if (StackManager.Instance == null) return;

            // Armure Terre majeur (permanente)
            if (element == Element.Terre)
            {
                int armorBonus = StackManager.Instance.GetEarthArmorRegen(playerID);
                var allies = BoardManager.Instance.GetAliveCards(playerID);
                if (armorBonus > 0)
                {
                    foreach (var ally in allies)
                        ally.ApplyEffect(new ActiveEffect
                        {
                            type            = EffectType.GiveArmor,
                            value           = armorBonus,
                            remainingTurns  = -1,
                            sourceName      = "Terre",
                            sourceSkillName = "majeur",
                        });
                }
                else
                {
                    foreach (var ally in allies)
                        ally.activeEffects.RemoveAll(e =>
                            e.type == EffectType.GiveArmor &&
                            e.sourceName == "Terre" &&
                            e.sourceSkillName == "majeur");
                }
            }

            // Régénération soins Lumière
            if (element == Element.Lumiere)
            {
                float hotPercent = StackManager.Instance.GetLightHoTPercent(playerID);
                if (hotPercent > 0f)
                {
                    var allies = BoardManager.Instance.GetAliveCards(playerID);
                    foreach (var ally in allies)
                    {
                        int heal = Mathf.RoundToInt(ally.EffectiveMaxHP * hotPercent);
                        ally.Heal(heal, false);
                    }
                }
            }
        }
    }
}