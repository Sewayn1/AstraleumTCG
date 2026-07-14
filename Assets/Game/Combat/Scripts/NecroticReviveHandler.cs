using UnityEngine;

namespace Astraleum
{
    // Nécrotique majeur 3/5 : une carte alliée Nécrotique détruite au combat (toute cause,
    // y compris DoT) a une chance de ressusciter à 10% de son EffectiveMaxHP au lieu de mourir.
    // Palier remplaçant (5 stacks ne s'ajoute pas au palier 3, il le remplace — voir
    // StackManager.GetNecroticReviveChance).
    public static class NecroticReviveHandler
    {
        private const float REVIVE_HP_PERCENT = 0.10f;

        // Retourne true si la carte a été ressuscitée (sa mort doit être annulée par l'appelant).
        public static bool TryRevive(CardInstance dying)
        {
            if (dying == null || dying.data == null) return false;
            if (dying.data.element != Element.Necrotique) return false;
            if (StackManager.Instance == null) return false;

            float chance = StackManager.Instance.GetNecroticReviveChance(dying.ownerPlayerID);
            if (chance <= 0f || Random.value >= chance) return false;

            dying.currentHP = Mathf.Max(1, Mathf.RoundToInt(dying.EffectiveMaxHP * REVIVE_HP_PERCENT));
            CombatLogManager.Instance?.AddEntry(
                $"{dying.data.cardName} renaît (Nécrotique majeur) !", playerID: dying.ownerPlayerID);
            dying.GetComponent<CardVisualUpdater>()?.UpdateVisuals();
            return true;
        }
    }
}
