using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astraleum
{
    public class DeckManager : MonoBehaviour
    {
        public static DeckManager Instance;

        [Header("Deck actuel")]
        public List<int> deckCardNumbers = new List<int>();

        private const int MAX_DECK_SIZE = 5;
        private const int MAX_SUPREME = 1;
        private const int MAX_LEGENDAIRE = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Retourne les CardData du deck actuel
        public List<CardData> GetDeckCards()
        {
            var cards = new List<CardData>();
            foreach (var num in deckCardNumbers)
            {
                var card = GetCardData(num);
                if (card != null) cards.Add(card);
            }
            return cards;
        }

        private CardData GetCardData(int cardNumber)
        {
            if (CardDatabase.Instance != null)
                return CardDatabase.Instance.GetCard(cardNumber);
            // Fallback hors scène Combat
            foreach (var c in Resources.LoadAll<CardData>("Cards"))
                if (c.cardNumber == cardNumber) return c;
            return null;
        }

        // Ajoute une carte au deck
        public bool TryAddCard(int cardNumber)
        {
            if (deckCardNumbers.Count >= MAX_DECK_SIZE)
            {
                Debug.LogWarning("DeckManager : deck plein (5 cartes max).");
                return false;
            }
            if (deckCardNumbers.Contains(cardNumber))
            {
                Debug.LogWarning("DeckManager : carte déjà dans le deck.");
                return false;
            }

            var card = GetCardData(cardNumber);
            if (card == null) return false;

            // Vérification des restrictions
            var currentCards = GetDeckCards();

            if (card.rarity == CardRarity.Supreme &&
                currentCards.Count(c => c.rarity == CardRarity.Supreme) >= MAX_SUPREME)
            {
                Debug.LogWarning("DeckManager : max 1 Supreme par deck.");
                return false;
            }
            if (card.rarity == CardRarity.Legendaire &&
                currentCards.Count(c => c.rarity == CardRarity.Legendaire) >= MAX_LEGENDAIRE)
            {
                Debug.LogWarning("DeckManager : max 1 Légendaire par deck.");
                return false;
            }

            deckCardNumbers.Add(cardNumber);
            return true;
        }

        // Retire une carte du deck
        public bool TryRemoveCard(int cardNumber)
        {
            if (!deckCardNumbers.Contains(cardNumber)) return false;
            deckCardNumbers.Remove(cardNumber);
            return true;
        }

        // Vérifie si le deck est valide pour jouer
        public bool IsDeckValid()
            => deckCardNumbers.Count == MAX_DECK_SIZE;

        // Vide le deck
        public void ClearDeck()
            => deckCardNumbers.Clear();

        // Valide les restrictions sans modifier le deck
        public DeckValidationResult ValidateDeck()
        {
            var result = new DeckValidationResult();
            var cards = GetDeckCards();

            result.isComplete = deckCardNumbers.Count == MAX_DECK_SIZE;
            result.supremeCount = cards.Count(c => c.rarity == CardRarity.Supreme);
            result.legendaireCount = cards.Count(c => c.rarity == CardRarity.Legendaire);
            result.isValid = result.isComplete
                                && result.supremeCount <= MAX_SUPREME
                                && result.legendaireCount <= MAX_LEGENDAIRE;
            return result;
        }

        // Deck de test par défaut (pour le développement)
        public void LoadTestDeck()
        {
            ClearDeck();
            TryAddCard(9);  // Dragon de Foudre (Épique) — Foudre Directe = AdjacentEnemies 40%
            TryAddCard(28); // Chevalier de Liotnar (Épique)
            TryAddCard(13); // Dragon de Lumière (Épique)
            TryAddCard(19); // Lyoness (Rare)
            TryAddCard(14); // Nyolung (Suprême)
            Debug.Log($"DeckManager : deck de test chargé ({deckCardNumbers.Count} cartes).");
        }
    }

    public class DeckValidationResult
    {
        public bool isValid;
        public bool isComplete;
        public int supremeCount;
        public int legendaireCount;
    }
}