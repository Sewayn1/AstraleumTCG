using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Gère les cartes possédées par le joueur.
    /// En mode ALPHA_ALL_OWNED, toutes les cartes sont considérées comme possédées.
    /// </summary>
    public class PlayerCollection : MonoBehaviour
    {
        public static PlayerCollection Instance;

        // ── Flag Alpha ────────────────────────────────────────────────
        // Mettre à false dès qu'un système de progression est implémenté
        private const bool ALPHA_ALL_OWNED = true;

        private HashSet<int> ownedCardNumbers = new HashSet<int>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            GrantAllCards();
        }

        private void GrantAllCards()
        {
            var cards = CardDatabase.Instance != null
                ? CardDatabase.Instance.GetAllCards()
                : Resources.LoadAll<CardData>("Cards").ToList();

            foreach (var card in cards)
                ownedCardNumbers.Add(card.cardNumber);
        }

        public bool OwnsCard(int cardNumber)
        {
            // En alpha, toutes les cartes sont possédées
            // Remplacer par : return ownedCardNumbers.Contains(cardNumber);
            // quand ALPHA_ALL_OWNED passera à false
            return ALPHA_ALL_OWNED || ownedCardNumbers.Contains(cardNumber);
        }

        public void AddCard(int cardNumber)
        {
            ownedCardNumbers.Add(cardNumber);
        }

        public int OwnedCount => ownedCardNumbers.Count;
    }
}
