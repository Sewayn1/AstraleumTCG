using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astraleum.AI
{
    /// <summary>Construit le deck de 5 cartes de l'IA selon le profil de difficulté.</summary>
    public static class AIDeckBuilder
    {
        private const int DECK_SIZE = 5;
        private const int MAX_SUPREME = 1;
        private const int MAX_LEGENDAIRE = 1;

        // Card_AITraining ("Entraîneur de Combat") — carte interne réservée au mode Bac à sable,
        // masquée de la Collection (CardData.hiddenFromCollection) et exclue du pool de deckbuilding.
        private const int TRAINING_CARD_NUMBER = 1000;
        private const int EMPTY_SLOT = 0;

        public static List<int> BuildDeck(GameMode mode, List<int> playerDeckNumbers)
        {
            switch (mode)
            {
                case GameMode.AISandbox:
                    return BuildTrainingDeck();
                case GameMode.AIEasy:
                    return BuildEasyDeck();
                case GameMode.AIMedium:
                    return BuildMediumDeck(playerDeckNumbers);
                case GameMode.AIHard:
                    return BuildHardDeck(playerDeckNumbers);
                default:
                    return BuildEasyDeck();
            }
        }

        /// <summary>Mode Bac à sable : seule Card_AITraining occupe le terrain adverse, au centre (slot 3).
        /// Les autres emplacements restent vides (BoardSpawner.NumbersToCardData traite EMPTY_SLOT comme un slot vide).</summary>
        public static List<int> BuildTrainingDeck()
        {
            return new List<int> { EMPTY_SLOT, EMPTY_SLOT, TRAINING_CARD_NUMBER, EMPTY_SLOT, EMPTY_SLOT };
        }

        /// <summary>Pioche aléatoire dans le pool complet, respecte les contraintes de rareté du deck.</summary>
        public static List<int> BuildEasyDeck()
        {
            var pool = GetPool();
            Shuffle(pool);
            return SelectDeckRespectingRarity(pool).Select(c => c.cardNumber).ToList();
        }

        /// <summary>Deck au même niveau de puissance que le deck joueur : cartes du pool les plus proches
        /// de la puissance moyenne du deck joueur (heuristique dégâts/PV/armure, pas de branches/passifs).</summary>
        public static List<int> BuildMediumDeck(List<int> playerDeckNumbers)
        {
            var pool = GetPool();
            var playerCards = ResolveCards(playerDeckNumbers, pool);
            if (playerCards.Count == 0) return BuildEasyDeck();

            float targetPower = playerCards.Average(PowerScore);
            var ranked = pool.OrderBy(c => Mathf.Abs(PowerScore(c) - targetPower)).ToList();

            return SelectDeckRespectingRarity(ranked).Select(c => c.cardNumber).ToList();
        }

        /// <summary>Contre-pick heuristique : maximise l'écart (tours pour tuer l'adversaire) −
        /// (tours pour être tué), sommé contre les 5 cartes connues du deck joueur.
        /// Approximation volontaire (CardData brut, sans branches/passifs/stacks) — V1.</summary>
        public static List<int> BuildHardDeck(List<int> playerDeckNumbers)
        {
            var pool = GetPool();
            var playerCards = ResolveCards(playerDeckNumbers, pool);
            if (playerCards.Count == 0) return BuildEasyDeck();

            var ranked = pool.OrderByDescending(c => MatchupScore(c, playerCards)).ToList();
            return SelectDeckRespectingRarity(ranked).Select(c => c.cardNumber).ToList();
        }

        private static float MatchupScore(CardData candidate, List<CardData> playerCards)
        {
            float total = 0f;
            foreach (var enemy in playerCards)
                total += TurnsToKill(enemy, candidate) - TurnsToKill(candidate, enemy);
            return total;
        }

        // Nombre de tours pour que "attacker" tue "defender" avec sa meilleure compétence de dégâts.
        private static float TurnsToKill(CardData attacker, CardData defender)
        {
            int bestDamage = System.Math.Max(attacker.skillOne?.damage ?? 0, attacker.skillTwo?.damage ?? 0);
            int hpDamage = System.Math.Max(1, bestDamage - defender.armorPoints);
            return (float)defender.maxHP / hpDamage;
        }

        private static float PowerScore(CardData c)
        {
            int bestDamage = System.Math.Max(c.skillOne?.damage ?? 0, c.skillTwo?.damage ?? 0);
            return c.maxHP + c.armorPoints * 2f + bestDamage * 3f;
        }

        private static List<CardData> GetPool() => CardDatabase.LoadVisibleCards();

        private static List<CardData> ResolveCards(List<int> numbers, List<CardData> pool)
        {
            if (numbers == null) return new List<CardData>();
            return numbers
                .Select(n => pool.FirstOrDefault(c => c.cardNumber == n))
                .Where(c => c != null)
                .ToList();
        }

        /// <summary>Prend les 5 premières cartes de la liste (déjà ordonnée par priorité), en
        /// respectant les contraintes max 1 Suprême / 1 Légendaire du deckbuilder joueur.</summary>
        private static List<CardData> SelectDeckRespectingRarity(List<CardData> orderedCandidates)
        {
            var deck = new List<CardData>();
            int supremeCount = 0;
            int legendaireCount = 0;

            foreach (var card in orderedCandidates)
            {
                if (deck.Count >= DECK_SIZE) break;
                if (card.rarity == CardRarity.Supreme && supremeCount >= MAX_SUPREME) continue;
                if (card.rarity == CardRarity.Legendaire && legendaireCount >= MAX_LEGENDAIRE) continue;

                deck.Add(card);
                if (card.rarity == CardRarity.Supreme) supremeCount++;
                if (card.rarity == CardRarity.Legendaire) legendaireCount++;
            }

            return deck;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
