using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum
{
    public class BoardSpawner : MonoBehaviour
    {
        public static BoardSpawner Instance;

        [Header("Prefab")]
        public GameObject cardPrefab;

        [Header("Pour les tests")]
        public bool useTestDeck = true;

        private void Awake() => Instance = this;

        private void Start()
        {
            // En réseau, le spawn est déclenché par NetworkGameController
            // après réception de NetMsgBothDecks (les decks des deux joueurs).
            if (NetworkBridge.IsActive) return;

            if (useTestDeck && DeckManager.Instance != null)
                DeckManager.Instance.LoadTestDeck();

            SpawnAllCards();
        }

        /// <summary>
        /// Appelé par NetworkGameController une fois les decks des deux joueurs connus.
        /// p1Numbers / p2Numbers : numéros de cartes (cardNumber) dans l'ordre de placement.
        /// </summary>
        public void SpawnAllCardsNetwork(List<int> p1Numbers, List<int> p2Numbers)
        {
            if (cardPrefab == null || CardDatabase.Instance == null)
            {
                Debug.LogError("[BoardSpawner] Dépendances manquantes pour le spawn réseau !");
                return;
            }

            var p1Cards = NumbersToCardData(p1Numbers);
            var p2Cards = NumbersToCardData(p2Numbers);

            if (p1Cards.Count == 0 || p2Cards.Count == 0)
            {
                Debug.LogError($"[BoardSpawner] Deck réseau invalide — J1:{p1Cards.Count} J2:{p2Cards.Count}");
                return;
            }

            // Spawn basé sur la perspective locale :
            //   player1Slots (bas) → cartes du joueur LOCAL, quel que soit son playerID
            //   player2Slots (haut) → cartes de l'adversaire
            // GetCardAtSlot cherche par (ownerPlayerID, slotIndex) dans allCards,
            // indépendamment de la position physique → cohérence réseau garantie.
            int localID    = NetworkBridge.LocalPlayerID; // 0 ou 1
            int opponentID = 1 - localID;

            List<CardData> localCards    = localID == 0 ? p1Cards : p2Cards;
            List<CardData> opponentCards = localID == 0 ? p2Cards : p1Cards;

            Debug.Log($"[BoardSpawner] Spawn perspective — LocalID:{localID} | Local P1Slots[0]={BoardManager.Instance.player1Slots[0]?.name}");
            SpawnCardsForPlayer(localCards,    localID,    BoardManager.Instance.player1Slots);
            SpawnCardsForPlayer(opponentCards, opponentID, BoardManager.Instance.player2Slots);

            StartCoroutine(RefreshStacksNextFrame());
        }

        private List<CardData> NumbersToCardData(List<int> numbers)
        {
            var cards = new List<CardData>();
            foreach (var num in numbers)
            {
                CardData card = CardDatabase.Instance != null
                    ? CardDatabase.Instance.GetCard(num)
                    : null;

                if (card == null)
                    foreach (var c in Resources.LoadAll<CardData>("Cards"))
                        if (c.cardNumber == num) { card = c; break; }

                if (card != null) cards.Add(card);
            }
            return cards;
        }

        public void SpawnAllCards()
        {
            if (cardPrefab == null || DeckManager.Instance == null || CardDatabase.Instance == null)
            {
                Debug.LogError("BoardSpawner : dépendances manquantes !");
                return;
            }

            var deckCards = DeckManager.Instance.GetDeckCards();
            if (deckCards.Count == 0)
            {
                Debug.LogWarning("BoardSpawner : le deck est vide !");
                return;
            }

            SpawnCardsForPlayer(deckCards, 0);
            SpawnCardsForPlayer(deckCards, 1);

            // ← Appel après un frame pour s'assurer que tout est initialisé
            StartCoroutine(RefreshStacksNextFrame());
        }

        private System.Collections.IEnumerator RefreshStacksNextFrame()
        {
            yield return null;

            StackManager.Instance?.RefreshPermanentStacks();

            // Bonus Terre mineur : appliqué UNE SEULE FOIS au début du combat
            StackManager.Instance?.ApplyEarthMinorBonusOnGameStart(0);
            StackManager.Instance?.ApplyEarthMinorBonusOnGameStart(1);

            // Frame supplémentaire : garantit que tous les composants sont prêts
            yield return null;

            // Passifs OnTurnStart appliqués dès le début du combat (ex. Invisible)
            // Les deux joueurs reçoivent leurs passifs pour un démarrage symétrique
            PassiveManager.Instance?.OnTurnStart(0);
            PassiveManager.Instance?.OnTurnStart(1);

            // Forcer la mise à jour visuelle immédiate après application des passifs
            if (BoardManager.Instance != null)
                for (int p = 0; p < 2; p++)
                    foreach (var card in BoardManager.Instance.GetAliveCards(p))
                        card.GetComponent<CardVisualUpdater>()?.UpdateVisuals();

            EndGameHandler.Instance?.OnGameStarted();
        }

        private void SpawnCardsForPlayer(List<CardData> cards, int playerID,
                                          SlotController[] slotsOverride = null)
        {
            var slots = slotsOverride ?? (playerID == 0
                ? BoardManager.Instance.player1Slots
                : BoardManager.Instance.player2Slots);

            for (int i = 0; i < cards.Count && i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                // Instancie le prefab sous le slot
                var cardGO = Instantiate(cardPrefab, slot.transform);
                cardGO.name = $"Card_{cards[i].cardNumber}_{cards[i].cardName}_P{playerID}";

                // Positionne la carte au centre du slot
                var rect = cardGO.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }

                // Initialise le CardInstance
                var cardInstance = cardGO.GetComponent<CardInstance>();
                if (cardInstance != null)
                {
                    cardInstance.Initialize(cards[i], i, playerID);
                    BoardManager.Instance.PlaceCard(cardInstance); // ← VÉRIFIEZ que cette ligne existe
                }

                // Place la carte sur le slot
                slot.PlaceCard(cardInstance);

                // Met à jour le visuel
                UpdateCardVisual(cardGO, cards[i]);
            }
        }

        private void UpdateCardVisual(GameObject cardGO, CardData data)
        {
            // Artwork — image principale de la carte
            var cardImage = cardGO.GetComponent<Image>();
            if (cardImage != null && data.artwork != null)
                cardImage.sprite = data.artwork;

            // HPCurrent
            var hpCurrent = cardGO.transform.Find("HPCurrent")?.GetComponent<TMP_Text>();
            if (hpCurrent != null)
                hpCurrent.text = data.maxHP.ToString();

            // HPMax
            var hpMax = cardGO.transform.Find("HPMax")?.GetComponent<TMP_Text>();
            if (hpMax != null)
                hpMax.text = data.maxHP.ToString();

            // ArmorText
            var armorText = cardGO.transform.Find("ArmorText")?.GetComponent<TMP_Text>();
            if (armorText != null)
                armorText.text = data.armorPoints > 0 ? data.armorPoints.ToString() : "";

            // SkillZone — noms et dégâts des compétences
            var skill1Name = cardGO.transform.Find("SkillZone/Skill1_Row/Skill1_Name")
                                             ?.GetComponent<TMP_Text>();
            var skill1DMG = cardGO.transform.Find("SkillZone/Skill1_Row/Skill1_DMG")
                                             ?.GetComponent<TMP_Text>();
            var skill2Name = cardGO.transform.Find("SkillZone/Skill2_Row/Skill2_Name")
                                             ?.GetComponent<TMP_Text>();
            var skill2DMG = cardGO.transform.Find("SkillZone/Skill2_Row/Skill2_DMG")
                                             ?.GetComponent<TMP_Text>();

            if (skill1Name != null && data.skillOne != null)
                skill1Name.text = data.skillOne.skillName;
            if (skill1DMG != null && data.skillOne != null)
                skill1DMG.text = data.skillOne.damage > 0 ? $"{data.skillOne.damage}" : "";
            if (skill2Name != null && data.skillTwo != null)
                skill2Name.text = data.skillTwo.skillName;
            if (skill2DMG != null && data.skillTwo != null)
                skill2DMG.text = data.skillTwo.damage > 0 ? $"{data.skillTwo.damage}" : "";
        }
    }
}