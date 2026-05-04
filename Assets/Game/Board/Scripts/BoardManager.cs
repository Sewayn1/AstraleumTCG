using System.Collections.Generic;
using UnityEngine;

namespace Astraleum
{
    public class BoardManager : MonoBehaviour
    {
        public static BoardManager Instance;

        [Header("Slots Joueur 1 (bas)")]
        public SlotController[] player1Slots = new SlotController[5];

        [Header("Slots Joueur 2 (haut)")]
        public SlotController[] player2Slots = new SlotController[5];

        private List<CardInstance> allCards = new List<CardInstance>();

        private void Awake() => Instance = this;

        // ── Placement et destruction ──────────────────────────────────

        public void PlaceCard(CardInstance card)
        {
            if (card == null) return;
            if (!allCards.Contains(card))
                allCards.Add(card);
        }

        public void DestroyCard(CardInstance card)
        {
            if (card == null) return;
            card.gameObject.SetActive(false);
            StackManager.Instance?.RefreshPermanentStacks();
            PassiveManager.Instance?.OnCardDestroyed(card);
        }

        // ── Récupération des cartes ───────────────────────────────────

        public List<CardInstance> GetAliveCards(int playerID)
        {
            return allCards.FindAll(c => c != null
                                      && c.IsAlive
                                      && c.gameObject.activeSelf
                                      && c.ownerPlayerID == playerID);
        }

        public List<CardInstance> GetAllCards()
            => new List<CardInstance>(allCards);

        // ── Navigation spatiale ───────────────────────────────────────

        public List<CardInstance> GetAdjacentCards(CardInstance card)
        {
            var result = new List<CardInstance>();
            var allies = GetAliveCards(card.ownerPlayerID);

            foreach (var ally in allies)
            {
                if (ally == card) continue;
                int dist = Mathf.Abs(ally.slotIndex - card.slotIndex);
                if (dist == 1)
                    result.Add(ally);
            }
            return result;
        }

        public CardInstance GetNearestAlly(CardInstance card)
        {
            var allies = GetAliveCards(card.ownerPlayerID);
            CardInstance nearest = null;
            int minDist = int.MaxValue;

            foreach (var ally in allies)
            {
                if (ally == card) continue;
                int dist = Mathf.Abs(ally.slotIndex - card.slotIndex);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = ally;
                }
            }
            return nearest;
        }

        public CardInstance GetCardAtSlot(int playerID, int slotIndex)
        {
            return allCards.Find(c => c != null
                                    && c.IsAlive
                                    && c.gameObject.activeSelf
                                    && c.ownerPlayerID == playerID
                                    && c.slotIndex == slotIndex);
        }

        // ── Carte à gauche (pour Astral) ──────────────────────────────

        public CardInstance GetCardToTheLeft(CardInstance card)
        {
            if (card == null) return null;

            // Cherche une carte vivante sur le slot immédiatement à gauche
            return allCards.Find(c => c != null
                                     && c.IsAlive
                                     && c.gameObject.activeSelf
                                     && c.ownerPlayerID == card.ownerPlayerID
                                     && c.slotIndex == card.slotIndex - 1);
        }

        // ── Vérification victoire ─────────────────────────────────────

        public bool CheckVictory(int attackerPlayerID)
        {
            // Ne jamais déclencher si le plateau n'est pas initialisé
            if (allCards.Count == 0) return false;

            int enemyID = attackerPlayerID == 0 ? 1 : 0;
            return GetAliveCards(enemyID).Count == 0;
        }

        // ── Slots ─────────────────────────────────────────────────────

        public SlotController GetSlot(int playerID, int slotIndex)
        {
            var slots = playerID == 0 ? player1Slots : player2Slots;
            if (slotIndex < 0 || slotIndex >= slots.Length) return null;
            return slots[slotIndex];
        }
    }
}