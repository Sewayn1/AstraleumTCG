using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astraleum
{
    public class CardDatabase : MonoBehaviour
    {
        public static CardDatabase Instance;

        private Dictionary<int, CardData> cardsByNumber = new Dictionary<int, CardData>();
        private Dictionary<CardRarity, List<CardData>> cardsByRarity = new Dictionary<CardRarity, List<CardData>>();

        private void Awake()
        {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllCards();
        }

        private void LoadAllCards()
        {
            // Charge tous les CardData depuis Assets/Resources/Cards/
            CardData[] allCards = Resources.LoadAll<CardData>("Cards");

            if (allCards.Length == 0)
            {
                Debug.LogWarning("CardDatabase : aucune carte trouvée dans Resources/Cards/");
                return;
            }

            foreach (var card in allCards)
            {
                // Index par numéro de carte
                if (!cardsByNumber.ContainsKey(card.cardNumber))
                    cardsByNumber.Add(card.cardNumber, card);
                else
                    Debug.LogWarning($"CardDatabase : numéro de carte dupliqué {card.cardNumber} ({card.cardName})");

                // Index par rareté
                if (!cardsByRarity.ContainsKey(card.rarity))
                    cardsByRarity[card.rarity] = new List<CardData>();
                cardsByRarity[card.rarity].Add(card);
            }

            Debug.Log($"CardDatabase : {allCards.Length} cartes chargées.");
        }

        // Récupère une carte par son numéro
        public CardData GetCard(int cardNumber)
        {
            if (cardsByNumber.TryGetValue(cardNumber, out var card))
                return card;
            Debug.LogWarning($"CardDatabase : carte {cardNumber} introuvable.");
            return null;
        }

        // Récupère toutes les cartes d'une rareté
        public List<CardData> GetCardsByRarity(CardRarity rarity)
        {
            if (cardsByRarity.TryGetValue(rarity, out var cards))
                return cards;
            return new List<CardData>();
        }

        // Récupère toutes les cartes (hors cartes internes marquées hiddenFromCollection)
        public List<CardData> GetAllCards()
            => cardsByNumber.Values.Where(c => !c.hiddenFromCollection).ToList();

        // Toutes les cartes visibles, que CardDatabase soit initialisé ou non (fallback Resources.LoadAll
        // si la scène n'a pas encore de CardDatabase, ex. MainMenu avant tout combat). À utiliser partout
        // où une liste de cartes "joueur" est nécessaire (Collection, DeckEditor, pool IA...) plutôt que de
        // dupliquer le ternaire Instance != null / Resources.LoadAll, qui oublierait facilement le filtre.
        public static List<CardData> LoadVisibleCards()
        {
            if (Instance != null) return Instance.GetAllCards();
            return Resources.LoadAll<CardData>("Cards").Where(c => c != null && !c.hiddenFromCollection).ToList();
        }

        // Récupère une carte aléatoire d'une rareté donnée
        public CardData GetRandomCard(CardRarity rarity)
        {
            var cards = GetCardsByRarity(rarity);
            if (cards.Count == 0) return null;
            return cards[Random.Range(0, cards.Count)];
        }
    }
}