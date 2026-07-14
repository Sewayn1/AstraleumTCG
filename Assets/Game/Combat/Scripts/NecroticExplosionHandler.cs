using System.Linq;

namespace Astraleum
{
    // Nécrotique : à la mort d'une carte portant un effet Nécrose actif (peu importe la cause —
    // coup direct ou tick de DoT), inflige 5% de son EffectiveMaxHP à ses cartes adjacentes et leur
    // applique une nouvelle instance de Nécrose à valeur fixe (3 DGT/tour, 1 tour, sans bonus mineur
    // ni héritage des stacks de la carte source). Les réactions en chaîne (une explosion qui tue une
    // carte adjacente elle-même sous Nécrose) sont un effet de bord accepté.
    public static class NecroticExplosionHandler
    {
        private const float EXPLOSION_DAMAGE_PERCENT = 0.05f;
        private const float PROPAGATED_NECROSE_VALUE = 3f;
        private const int PROPAGATED_NECROSE_DURATION = 1;

        public static void TriggerExplosionIfApplicable(CardInstance dying)
        {
            if (dying == null || dying.data == null || BoardManager.Instance == null) return;
            if (!dying.activeEffects.Any(e => e.type == EffectType.Necrose)) return;

            int explosionDmg = UnityEngine.Mathf.Max(1,
                UnityEngine.Mathf.RoundToInt(dying.EffectiveMaxHP * EXPLOSION_DAMAGE_PERCENT));

            foreach (var adj in BoardManager.Instance.GetAdjacentCards(dying))
            {
                if (!adj.IsAlive) continue;

                int actual = adj.TakeDamage(explosionDmg);
                adj.GetComponent<CombatPopupHandler>()?.ShowDamagePopup(actual);
                adj.ApplyEffect(new ActiveEffect
                {
                    type = EffectType.Necrose,
                    value = PROPAGATED_NECROSE_VALUE,
                    remainingTurns = PROPAGATED_NECROSE_DURATION,
                    sourceName = dying.data.cardName,
                    sourceSkillName = "Explosion Nécrotique",
                });

                CombatLogManager.Instance?.AddEntry(
                    $"{dying.data.cardName} explose : -{actual} DGT + Nécrose à {adj.data.cardName}",
                    playerID: dying.ownerPlayerID);

                // Chaîne acceptée : si l'explosion tue un adjacent lui-même sous Nécrose,
                // son propre chemin de mort (HandleCardDeath) rejoue explosion/revive normalement.
                if (!adj.IsAlive)
                    SkillExecutor.HandleCardDeath(adj, dying);
            }
        }
    }
}
