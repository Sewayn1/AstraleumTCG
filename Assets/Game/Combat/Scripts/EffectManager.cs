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

            // Régénération armure Terre
            if (element == Element.Terre)
            {
                int armorRegen = StackManager.Instance.GetEarthArmorRegen(playerID);
                if (armorRegen > 0)
                {
                    var allies = BoardManager.Instance.GetAliveCards(playerID);
                    foreach (var ally in allies)
                        ally.RestoreArmor(armorRegen);
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
                        int heal = Mathf.RoundToInt(ally.data.maxHP * hotPercent);
                        ally.Heal(heal, false);
                    }
                }
            }
        }
    }
}