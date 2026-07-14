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
        [Tooltip("Prefab dédié au Boss (3 compétences) — utilisé uniquement pour la carte du Boss, jamais pour les cartes du joueur.")]
        public GameObject bossCardPrefab;

        [Header("Pour les tests")]
        public bool useTestDeck = true;
        public bool useElementTest = false; // P1 = Air, P2 = Ténèbres

        [Header("Cartes personnalisées (Playtest)")]
        [Tooltip("Active la sélection manuelle des cartes ci-dessous. Prioritaire sur useTestDeck et useElementTest.")]
        public bool useCustomTestCards = false;
        [Tooltip("Cartes du Joueur 1 (max 5). Glisser les CardData depuis Assets/Resources/Cards/.")]
        public List<CardData> testCardsP1 = new List<CardData>();
        [Tooltip("Cartes du Joueur 2 (max 5). Glisser les CardData depuis Assets/Resources/Cards/.")]
        public List<CardData> testCardsP2 = new List<CardData>();

        private void Awake() => Instance = this;

        private void Start()
        {
            // En réseau, le spawn est déclenché par NetworkGameController
            // après réception de NetMsgBothDecks (les decks des deux joueurs).
            // En mode IA, le spawn est déclenché par LocalAIGameController (SpawnAllCardsVsAI).
            if (NetworkBridge.IsActive || AI.GameModeContext.IsAIMatch) return;

            if (useCustomTestCards && DeckManager.Instance != null)
                DeckManager.Instance.LoadCustomDeck(testCardsP1);
            else if (useTestDeck && DeckManager.Instance != null)
                DeckManager.Instance.LoadTestDeck();

            SpawnAllCards();
        }

        /// <summary>
        /// Appelé par NetworkGameController une fois les decks des deux joueurs connus.
        /// p1Numbers / p2Numbers : numéros de cartes (cardNumber) dans l'ordre de placement.
        /// </summary>
        // localNumbers = deck du joueur local, opponentNumbers = deck de l'adversaire
        // (ordre identique à ce que NetworkGameController reçoit via GameSetupMessage)
        public void SpawnAllCardsNetwork(List<int> localNumbers, List<int> opponentNumbers)
        {
            if (cardPrefab == null || CardDatabase.Instance == null)
            {
                Debug.LogError("[BoardSpawner] Dépendances manquantes pour le spawn réseau !");
                return;
            }

            var localCards    = NumbersToCardData(localNumbers);
            var opponentCards = NumbersToCardData(opponentNumbers);

            if (localCards.Count == 0 || opponentCards.Count == 0)
            {
                Debug.LogError($"[BoardSpawner] Deck réseau invalide — local:{localCards.Count} opponent:{opponentCards.Count}");
                return;
            }

            // player1Slots (bas) → joueur LOCAL, player2Slots (haut) → adversaire
            // GetCardAtSlot cherche par (ownerPlayerID, slotIndex) → cohérence réseau garantie
            int localID    = NetworkBridge.LocalPlayerID;
            int opponentID = 1 - localID;

            Debug.Log($"[BoardSpawner] Spawn — LocalID:{localID} | local:{localCards[0].cardName} opp:{opponentCards[0].cardName}");
            SpawnCardsForPlayer(localCards,    localID,    BoardManager.Instance.player1Slots);
            SpawnCardsForPlayer(opponentCards, opponentID, BoardManager.Instance.player2Slots);

            StartCoroutine(RefreshStacksNextFrame());
        }

        /// <summary>
        /// Spawn pour le mode solo vs IA. Le joueur humain est toujours en player1Slots (bas),
        /// l'IA toujours en player2Slots (haut) — pas de dépendance à NetworkBridge.LocalPlayerID.
        /// </summary>
        public void SpawnAllCardsVsAI(List<int> playerNumbers, List<int> aiNumbers)
        {
            if (cardPrefab == null || CardDatabase.Instance == null)
            {
                Debug.LogError("[BoardSpawner] Dépendances manquantes pour le spawn vs IA !");
                return;
            }

            var playerCards = NumbersToCardData(playerNumbers);
            var aiCards     = NumbersToCardData(aiNumbers);

            if (playerCards.Count == 0 || aiCards.Count == 0)
            {
                Debug.LogError($"[BoardSpawner] Deck vs IA invalide — joueur:{playerCards.Count} IA:{aiCards.Count}");
                return;
            }

            SpawnCardsForPlayer(playerCards, 0, BoardManager.Instance.player1Slots);
            SpawnCardsForPlayer(aiCards,     1, BoardManager.Instance.player2Slots);

            StartCoroutine(RefreshStacksNextFrame());
        }

        /// <summary>
        /// Spawn pour un combat contre un Boss (ex. Voragoth) : le joueur humain reçoit son deck
        /// normalement, le Boss occupe SEUL le slot 3 (index 2) — les slots 1,2,4,5 (index 0,1,3,4)
        /// restent volontairement vides. Ne passe pas par NumbersToCardData côté Boss (les 3 CardData
        /// de phase partagent toutes cardNumber=0, une CardData directe évite toute ambiguïté).
        /// </summary>
        public void SpawnBossEncounter(List<int> playerNumbers, CardData bossPhase1Data)
        {
            if (cardPrefab == null || CardDatabase.Instance == null || bossPhase1Data == null)
            {
                Debug.LogError("[BoardSpawner] Dépendances manquantes pour le spawn Boss !");
                return;
            }

            var playerCards = NumbersToCardData(playerNumbers);
            if (playerCards.Count == 0)
            {
                Debug.LogError("[BoardSpawner] Deck joueur invalide pour le combat Boss !");
                return;
            }

            SpawnCardsForPlayer(playerCards, 0, BoardManager.Instance.player1Slots);

            var bossSlotCards = new List<CardData> { null, null, bossPhase1Data, null, null };
            var bossPrefab = bossCardPrefab != null ? bossCardPrefab : cardPrefab;
            SpawnCardsForPlayer(bossSlotCards, 1, BoardManager.Instance.player2Slots, bossPrefab);

            StartCoroutine(RefreshStacksNextFrame());
        }

        /// <summary>
        /// Spawn pour le combat Vaelthor (Phase 1 uniquement — 3 cartes côté Boss) :
        /// Vaelthor au centre (slot 3, index 2, prefab Boss 3 compétences), Faucheur des Âmes
        /// et Gardien des Âmes en soutien (slots 2/4, index 1/3, prefab standard CardPrefab2 —
        /// ce sont des cartes 2 compétences normales, pas des "Boss"). Les slots 1 et 5
        /// (index 0/4) restent vides. Ne passe pas par NumbersToCardData côté Boss (les CardData
        /// de Vaelthor/gardiens partagent toutes cardNumber=0).
        /// </summary>
        public void SpawnVaelthorEncounter(List<int> playerNumbers, CardData vaelthorData, CardData faucheurData, CardData gardienData)
        {
            if (cardPrefab == null || CardDatabase.Instance == null || vaelthorData == null || faucheurData == null || gardienData == null)
            {
                Debug.LogError("[BoardSpawner] Dépendances manquantes pour le spawn Vaelthor !");
                return;
            }

            var playerCards = NumbersToCardData(playerNumbers);
            if (playerCards.Count == 0)
            {
                Debug.LogError("[BoardSpawner] Deck joueur invalide pour le combat Vaelthor !");
                return;
            }

            SpawnCardsForPlayer(playerCards, 0, BoardManager.Instance.player1Slots);

            var bossPrefab = bossCardPrefab != null ? bossCardPrefab : cardPrefab;
            var p2Slots = BoardManager.Instance.player2Slots;

            // Gardiens — cartes standard 2 compétences (CardPrefab2)
            SpawnCardsForPlayer(new List<CardData> { null, faucheurData, null, null, null }, 1, p2Slots, cardPrefab);
            SpawnCardsForPlayer(new List<CardData> { null, null, null, gardienData, null }, 1, p2Slots, cardPrefab);
            // Vaelthor — carte Boss 3 compétences (CardPrefabBoss)
            SpawnCardsForPlayer(new List<CardData> { null, null, vaelthorData, null, null }, 1, p2Slots, bossPrefab);

            StartCoroutine(RefreshStacksNextFrame());
        }

        private List<CardData> NumbersToCardData(List<int> numbers)
        {
            var cards = new List<CardData>();
            foreach (var num in numbers)
            {
                // num <= 0 = slot volontairement vide (ex. AIDeckBuilder.BuildTrainingDeck) —
                // on ajoute null pour préserver l'index (correspondance avec les slots du plateau).
                if (num <= 0) { cards.Add(null); continue; }

                CardData card = CardDatabase.Instance != null
                    ? CardDatabase.Instance.GetCard(num)
                    : null;

                if (card == null)
                    foreach (var c in Resources.LoadAll<CardData>("Cards"))
                        if (c.cardNumber == num) { card = c; break; }

                cards.Add(card);
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

            if (useCustomTestCards)
            {
                var p1 = testCardsP1.FindAll(c => c != null);
                var p2 = testCardsP2.FindAll(c => c != null);
                if (p1.Count > 0) SpawnCardsForPlayer(p1, 0);
                if (p2.Count > 0) SpawnCardsForPlayer(p2, 1);
            }
            else if (useElementTest)
            {
                var airCards  = GetCardsByElement(Element.Air,     5);
                var darkCards = GetCardsByElement(Element.Tenebres, 5);
                if (airCards.Count  > 0) SpawnCardsForPlayer(airCards,  0);
                if (darkCards.Count > 0) SpawnCardsForPlayer(darkCards, 1);
            }
            else
            {
                var deckCards = DeckManager.Instance.GetDeckCards();
                if (deckCards.Count == 0)
                {
                    Debug.LogWarning("BoardSpawner : le deck est vide !");
                    return;
                }
                SpawnCardsForPlayer(deckCards, 0);
                SpawnCardsForPlayer(deckCards, 1);
            }

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

        private List<CardData> GetCardsByElement(Element element, int maxCount)
        {
            var result   = new List<CardData>();
            var allCards = Resources.LoadAll<CardData>("Cards");
            foreach (var card in allCards)
            {
                if (card != null && card.element == element)
                {
                    result.Add(card);
                    if (result.Count >= maxCount) break;
                }
            }
            return result;
        }

        private void SpawnCardsForPlayer(List<CardData> cards, int playerID,
                                          SlotController[] slotsOverride = null,
                                          GameObject prefabOverride = null)
        {
            var slots = slotsOverride ?? (playerID == 0
                ? BoardManager.Instance.player1Slots
                : BoardManager.Instance.player2Slots);
            var prefab = prefabOverride != null ? prefabOverride : cardPrefab;

            for (int i = 0; i < cards.Count && i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                if (cards[i] == null) continue; // slot volontairement vide (ex. mode Entraînement)

                // Instancie le prefab sous le slot
                var cardGO = Instantiate(prefab, slot.transform);
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
                hpCurrent.text = data.isTrainingDummy ? "" : data.maxHP.ToString();

            // HPMax
            var hpMax = cardGO.transform.Find("HPMax")?.GetComponent<TMP_Text>();
            if (hpMax != null)
                hpMax.text = data.isTrainingDummy ? "" : data.maxHP.ToString();

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