using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum.UI
{
    /// <summary>
    /// Gère l'affichage de la collection dans Panel_MyCards.
    /// À attacher directement sur Panel_MyCards.
    /// </summary>
    public class CollectionManager : MonoBehaviour
    {
        public static CollectionManager Instance;

        [Header("Grille")]
        [Tooltip("Content du ScrollView (doit avoir un GridLayoutGroup).")]
        public RectTransform cardGrid;
        [Tooltip("Prefab de carte à afficher (CardPrefab2 + CollectionCardEntry).")]
        public GameObject cardEntryPrefab;

        [Header("Taille des cartes")]
        [Tooltip("Taille d'affichage des cartes. Si (0,0), lue depuis le RectTransform du prefab.")]
        public Vector2 cardSize = Vector2.zero;
        [Tooltip("Espacement entre les cartes.")]
        public Vector2 cardSpacing = new Vector2(10f, 10f);
        [Tooltip("Marges internes de la grille (left, right, top, bottom).")]
        public Vector4 gridPadding = new Vector4(16, 16, 16, 16);

        [Header("Compteur")]
        public TMP_Text counterText;

        [Header("Filtres — optionnels")]
        public TMP_Dropdown filterRarity;
        public TMP_Dropdown filterElement;

        private List<GameObject> spawnedCards;
        private int currentRarityFilter  = 0;
        private int currentElementFilter = 0;
        private GridLayoutGroup grid;
        private ContentSizeFitter sizeFitter;

        private void Awake()
        {
            Instance = this;
            spawnedCards = new List<GameObject>();
            SetupGrid();
            PopulateDropdowns();
        }

        private void OnEnable()
        {
            Astraleum.LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            Populate();
        }

        private void OnDisable()
        {
            Astraleum.LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            PopulateDropdowns();
            Populate();
        }

        // ── Configuration GridLayoutGroup ─────────────────────────────

        private void SetupGrid()
        {
            if (cardGrid == null || cardEntryPrefab == null) return;

            // GridLayoutGroup
            grid = cardGrid.GetComponent<GridLayoutGroup>();
            if (grid == null)
                grid = cardGrid.gameObject.AddComponent<GridLayoutGroup>();

            // Taille des cartes : depuis l'inspecteur ou depuis le RectTransform du prefab
            Vector2 size = cardSize;
            if (size == Vector2.zero)
            {
                var prefabRect = cardEntryPrefab.GetComponent<RectTransform>();
                if (prefabRect != null && prefabRect.sizeDelta.x > 0)
                    size = prefabRect.sizeDelta;
                else
                    size = new Vector2(200f, 280f); // fallback
            }

            grid.cellSize       = size;
            grid.spacing        = cardSpacing;
            grid.padding        = new RectOffset((int)gridPadding.x, (int)gridPadding.y,
                                               (int)gridPadding.z, (int)gridPadding.w);
            grid.startCorner    = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis      = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint     = GridLayoutGroup.Constraint.Flexible;

            // ContentSizeFitter : le Content grandit verticalement selon le nombre de cartes
            sizeFitter = cardGrid.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null)
                sizeFitter = cardGrid.gameObject.AddComponent<ContentSizeFitter>();

            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Le Content s'étire sur toute la largeur du viewport
            cardGrid.anchorMin = new Vector2(0f, 1f);
            cardGrid.anchorMax = new Vector2(1f, 1f);
            cardGrid.pivot     = new Vector2(0.5f, 1f);
            cardGrid.offsetMin = new Vector2(0f, cardGrid.offsetMin.y);
            cardGrid.offsetMax = new Vector2(0f, cardGrid.offsetMax.y);
        }

        // ── Peuplement ────────────────────────────────────────────────

        public void Populate()
        {
            ClearGrid();

            if (cardEntryPrefab == null || cardGrid == null)
            {
                Debug.LogWarning("[CollectionManager] cardEntryPrefab ou cardGrid non assigné.");
                return;
            }

            List<Astraleum.CardData> allCards;
            if (Astraleum.CardDatabase.Instance != null)
            {
                allCards = Astraleum.CardDatabase.Instance.GetAllCards();
            }
            else
            {
                allCards = Resources.LoadAll<Astraleum.CardData>("Cards").ToList();
                if (allCards.Count == 0)
                {
                    Debug.LogWarning("[CollectionManager] Aucune carte trouvée dans Resources/Cards/");
                    return;
                }
            }

            allCards.Sort((a, b) => a.cardNumber.CompareTo(b.cardNumber));

            int ownedCount = 0;

            foreach (var card in allCards)
            {
                if (!PassesFilter(card)) continue;

                bool owned = Astraleum.PlayerCollection.Instance == null
                          || Astraleum.PlayerCollection.Instance.OwnsCard(card.cardNumber);

                if (owned) ownedCount++;

                var go    = Instantiate(cardEntryPrefab, cardGrid);
                var entry = go.GetComponent<CollectionCardEntry>();
                entry?.Setup(card, owned);
                spawnedCards.Add(go);
            }

            UpdateCounter(ownedCount, allCards.Count);

            // Force Unity à recalculer le layout immédiatement
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardGrid);
        }

        private void ClearGrid()
        {
            foreach (var go in spawnedCards)
                if (go != null) Destroy(go);
            spawnedCards.Clear();
        }

        // ── Dropdowns ─────────────────────────────────────────────────

        private void PopulateDropdowns()
        {
            if (filterRarity != null)
            {
                int prevRarity = filterRarity.value;
                filterRarity.ClearOptions();
                var options = new List<TMP_Dropdown.OptionData>
                {
                    new TMP_Dropdown.OptionData(Astraleum.LocalizationManager.Get("collection_filter_all"))
                };
                foreach (Astraleum.CardRarity r in System.Enum.GetValues(typeof(Astraleum.CardRarity)))
                    options.Add(new TMP_Dropdown.OptionData(Astraleum.LocalizationManager.Get($"ui_rarity_{r.ToString().ToLower()}")));

                filterRarity.AddOptions(options);
                filterRarity.value = prevRarity;
                filterRarity.onValueChanged.RemoveAllListeners();
                filterRarity.onValueChanged.AddListener(OnRarityFilterChanged);
            }

            if (filterElement != null)
            {
                int prevElement = filterElement.value;
                filterElement.ClearOptions();
                var options = new List<TMP_Dropdown.OptionData>
                {
                    new TMP_Dropdown.OptionData(Astraleum.LocalizationManager.Get("ui_element_all"))
                };
                foreach (Astraleum.Element e in System.Enum.GetValues(typeof(Astraleum.Element)))
                    options.Add(new TMP_Dropdown.OptionData(Astraleum.LocalizationManager.Get($"ui_element_{e.ToString().ToLower()}")));

                filterElement.AddOptions(options);
                filterElement.value = prevElement;
                filterElement.onValueChanged.RemoveAllListeners();
                filterElement.onValueChanged.AddListener(OnElementFilterChanged);
            }
        }

        // ── Filtres ───────────────────────────────────────────────────

        private bool PassesFilter(Astraleum.CardData card)
        {
            if (currentRarityFilter != 0)
            {
                var target = (Astraleum.CardRarity)(currentRarityFilter - 1);
                if (card.rarity != target) return false;
            }
            if (currentElementFilter != 0)
            {
                var target = (Astraleum.Element)(currentElementFilter - 1);
                if (card.element != target) return false;
            }
            return true;
        }

        public void OnRarityFilterChanged(int value)
        {
            currentRarityFilter = value;
            Populate();
        }

        public void OnElementFilterChanged(int value)
        {
            currentElementFilter = value;
            Populate();
        }

        // ── Compteur ──────────────────────────────────────────────────

        private void UpdateCounter(int owned, int total)
        {
            if (counterText == null) return;
            counterText.text = Astraleum.LocalizationManager.Get("collection_counter", owned, total);
        }
    }
}
