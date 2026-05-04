using System.Collections.Generic;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Gère la sauvegarde et le chargement de plusieurs decks nommés via PlayerPrefs.
    /// </summary>
    public class DeckSaveSystem : MonoBehaviour
    {
        public static DeckSaveSystem Instance;

        /// <summary>Déclenché immédiatement après chaque sauvegarde ou suppression.</summary>
        public static event System.Action OnDecksChanged;

        private const string SAVE_KEY   = "Astraleum_Decks";
        private const int    MAX_DECKS  = 10;

        private DeckSaveData saveData = new DeckSaveData();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // ── Sauvegarde ────────────────────────────────────────────────

        public bool SaveDeck(string deckName, List<int> cardNumbers, int slotIndex = -1, int dominantElementIndex = -1)
        {
            if (string.IsNullOrWhiteSpace(deckName))
            {
                Debug.LogWarning("[DeckSaveSystem] Nom de deck vide.");
                return false;
            }

            // Met à jour si le slot existe déjà, sinon cherche par nom
            var existing = slotIndex >= 0
                ? saveData.decks.Find(d => d.slotIndex == slotIndex)
                : saveData.decks.Find(d => d.deckName  == deckName);

            if (existing != null)
            {
                existing.deckName             = deckName;
                existing.cardNumbers          = new List<int>(cardNumbers);
                existing.slotIndex            = slotIndex;
                existing.dominantElementIndex = dominantElementIndex;
            }
            else
            {
                if (saveData.decks.Count >= MAX_DECKS)
                {
                    Debug.LogWarning("[DeckSaveSystem] Limite de decks sauvegardés atteinte (10).");
                    return false;
                }
                saveData.decks.Add(new SavedDeck
                {
                    deckName             = deckName,
                    cardNumbers          = new List<int>(cardNumbers),
                    slotIndex            = slotIndex,
                    dominantElementIndex = dominantElementIndex
                });
            }

            Persist();
            return true;
        }

        public void DeleteDeck(string deckName)
        {
            saveData.decks.RemoveAll(d => d.deckName == deckName);
            Persist();
        }

        /// <summary>Retourne le deck assigné à un slot précis, ou null.</summary>
        public SavedDeck GetDeckBySlot(int slotIndex)
            => saveData.decks.Find(d => d.slotIndex == slotIndex);

        // ── Chargement ────────────────────────────────────────────────

        public List<int> LoadDeck(string deckName)
        {
            var deck = saveData.decks.Find(d => d.deckName == deckName);
            return deck != null ? new List<int>(deck.cardNumbers) : null;
        }

        public List<string> GetSavedDeckNames()
        {
            var names = new List<string>();
            foreach (var d in saveData.decks)
                names.Add(d.deckName);
            return names;
        }

        public bool HasDecks() => saveData.decks.Count > 0;

        // ── Persistance ───────────────────────────────────────────────

        private void Persist()
        {
            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
            OnDecksChanged?.Invoke();
        }

        private void Load()
        {
            if (!PlayerPrefs.HasKey(SAVE_KEY)) return;
            string json = PlayerPrefs.GetString(SAVE_KEY);
            saveData = JsonUtility.FromJson<DeckSaveData>(json) ?? new DeckSaveData();
        }
    }

    // ── Structures de données ─────────────────────────────────────────

    [System.Serializable]
    public class SavedDeck
    {
        public string    deckName;
        public List<int> cardNumbers         = new List<int>();
        public int       slotIndex           = -1;
        public int       dominantElementIndex = -1; // cast vers Astraleum.Element, -1 = inconnu
    }

    [System.Serializable]
    public class DeckSaveData
    {
        public List<SavedDeck> decks = new List<SavedDeck>();
    }
}
