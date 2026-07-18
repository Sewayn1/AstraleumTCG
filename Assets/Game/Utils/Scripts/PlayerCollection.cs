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

        // Cartes de récompense (ex. Voragoth - Dernière Calamité) : jamais concernées par
        // ALPHA_ALL_OWNED, leur possession est toujours conditionnée à un vrai déblocage persisté.
        private static readonly HashSet<int> REWARD_CARD_NUMBERS = new HashSet<int> { 48, 49, 50, 51, 57 };
        private const string UNLOCK_KEY_PREFIX = "Unlock_Card_";

        private HashSet<int> ownedCardNumbers = new HashSet<int>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Chargé en Awake (pas Start) : d'autres composants (ex. RaidPanelController.OnEnable)
            // appellent OwnsCard() dès leur propre OnEnable, qui s'exécute AVANT le Start() de tous
            // les scripts de la frame — un chargement en Start() lirait alors un état pas encore prêt
            // (ex. trophée de boss vaincu invisible au premier affichage du panneau).
            GrantAllCards();
            LoadUnlockedRewardCards();
        }

        private void GrantAllCards()
        {
            var cards = CardDatabase.LoadVisibleCards();

            foreach (var card in cards)
            {
                if (REWARD_CARD_NUMBERS.Contains(card.cardNumber)) continue; // débloquées séparément
                ownedCardNumbers.Add(card.cardNumber);
            }
        }

        private void LoadUnlockedRewardCards()
        {
            foreach (int cardNumber in REWARD_CARD_NUMBERS)
                if (PlayerPrefs.GetInt(UNLOCK_KEY_PREFIX + cardNumber, 0) == 1)
                    ownedCardNumbers.Add(cardNumber);
        }

        public bool OwnsCard(int cardNumber)
        {
            // Les cartes de récompense ignorent toujours ALPHA_ALL_OWNED — seul un vrai déblocage compte.
            if (REWARD_CARD_NUMBERS.Contains(cardNumber))
                return ownedCardNumbers.Contains(cardNumber);

            // En alpha, toutes les autres cartes sont possédées
            // Remplacer par : return ownedCardNumbers.Contains(cardNumber);
            // quand ALPHA_ALL_OWNED passera à false
            return ALPHA_ALL_OWNED || ownedCardNumbers.Contains(cardNumber);
        }

        public void AddCard(int cardNumber)
        {
            ownedCardNumbers.Add(cardNumber);
        }

        /// <summary>
        /// Débloque une carte de récompense (ex. victoire contre un Boss), persisté en PlayerPrefs.
        /// Retourne true uniquement si ce déblocage est nouveau (permet d'afficher un message "Nouvelle carte" une seule fois).
        /// </summary>
        public bool UnlockRewardCard(int cardNumber)
        {
            bool alreadyUnlocked = PlayerPrefs.GetInt(UNLOCK_KEY_PREFIX + cardNumber, 0) == 1;
            ownedCardNumbers.Add(cardNumber);
            if (alreadyUnlocked) return false;

            PlayerPrefs.SetInt(UNLOCK_KEY_PREFIX + cardNumber, 1);
            PlayerPrefs.Save();
            return true;
        }

        public int OwnedCount => ownedCardNumbers.Count;

        /// <summary>Texte d'indication d'obtention affiché en tooltip sur une carte de récompense verrouillée (clé loc "collection_unlock_hint_{cardNumber}").</summary>
        public static string GetUnlockHint(int cardNumber) => LocalizationManager.Get($"collection_unlock_hint_{cardNumber}");
    }
}
