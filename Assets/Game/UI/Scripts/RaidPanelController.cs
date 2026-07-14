using System.Collections.Generic;
using Astraleum.AI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Astraleum.UI
{
    /// <summary>
    /// À attacher sur Panel_Decks (enfant de Panel_Raid). Gère la sélection de deck et le
    /// lancement d'un combat Boss (Voragoth). Volontairement séparé de DeckSelectPanel
    /// (matchmaking PvP/IA, singleton statique) : Panel_Raid a sa propre instance de
    /// deck-slots, et partager DeckSelectPanel.Instance entre deux panels actifs dans la même
    /// scène créerait une collision (le plus récemment activé écrase la référence de l'autre).
    /// </summary>
    public class RaidPanelController : MonoBehaviour
    {
        public static RaidPanelController Instance;

        [Header("Slots — mêmes slotIndex que Panel_DeckEditor")]
        public DeckCardSlot[] deckSlots = new DeckCardSlot[8];

        [Header("Lancement")]
        [Tooltip("Btn_Search — actif après sélection d'un deck.")]
        public Button btnLaunch;

        [System.Serializable]
        public struct BossEncounterConfig
        {
            public Button button;
            [Tooltip("0 = Voragoth, 1 = Vaelthor — voir GameModeContext.BossID.")]
            public int bossID;
            [Tooltip("CardData Phase 1 du Boss (utilisée pour AIDisplayName + GameModeContext.BossEncounterData).")]
            public CardData phase1Data;
            [Tooltip("Clé de localisation du nom affiché du Boss (ex. ui_btn_voragoth) — utilisée pour le titre \"Raid contre {0}\".")]
            public string nameKey;
            [Tooltip("Panneau de description (ex. VoragothDesc/VaelthorDesc) — affiché seul, les autres sont masqués.")]
            public GameObject descPanel;
            [Tooltip("Trophée affiché si ce Boss a déjà été vaincu (ex. VoragothTrophy/VaelthorTrophy) — affiché seul, les autres sont masqués.")]
            public GameObject trophyObject;
            [Tooltip("Numéro de carte récompense débloquée à la victoire sur ce Boss (ex. 48 = Voragoth, 49 = Vaelthor) — sert à détecter si le Boss a déjà été vaincu via PlayerCollection.OwnsCard.")]
            public int rewardCardNumber;
        }

        [Header("Boss — sélecteur multi-boss (un bouton par entrée)")]
        public BossEncounterConfig[] bossConfigs;

        [Header("Titre dynamique (GamemodeTitle) — optionnel")]
        [Tooltip("Clé de localisation avec un {0} pour le nom du Boss (ex. \"Raid contre {0}\").")]
        public string gamemodeTitleKey = "ui_raid_gamemode_title";
        public TMP_Text gamemodeTitle;

        [Header("Avertissement dynamique (Warning) — optionnel")]
        [Tooltip("Clé de localisation avec un {0} pour le nom du Boss (ex. \"Attention ! {0} est un combat difficile...\").")]
        public string warningTextKey = "ui_raid_warning";
        public TMP_Text warningText;

        [Header("Feedback — optionnel")]
        public TMP_Text feedbackText;

        private DeckCardSlot selectedSlot;
        private BossEncounterConfig? selectedBoss;

        private void Awake()
        {
            Instance = this;
            btnLaunch?.onClick.AddListener(LaunchRaid);

            if (gamemodeTitle == null)
                gamemodeTitle = transform.Find("GamemodeTitle")?.GetComponent<TMP_Text>();
            if (warningText == null)
                warningText = transform.Find("Warning")?.GetComponent<TMP_Text>();

            if (bossConfigs != null)
            {
                foreach (var cfg in bossConfigs)
                {
                    var captured = cfg; // capture par valeur — évite le piège de closure sur la variable de boucle
                    captured.button?.onClick.AddListener(() => SelectBoss(captured));
                }
            }
        }

        private void SelectBoss(BossEncounterConfig config)
        {
            selectedBoss = config;
            ClearFeedback();

            // Un seul panneau de description visible à la fois — masque tous les autres.
            if (bossConfigs != null)
                foreach (var cfg in bossConfigs)
                    if (cfg.descPanel != null)
                        cfg.descPanel.SetActive(cfg.descPanel == config.descPanel);

            // Trophée : uniquement celui du Boss sélectionné, uniquement s'il a déjà été vaincu
            // (carte récompense déjà débloquée — voir PlayerCollection.REWARD_CARD_NUMBERS).
            bool defeated = config.rewardCardNumber > 0
                && PlayerCollection.Instance != null
                && PlayerCollection.Instance.OwnsCard(config.rewardCardNumber);
            if (bossConfigs != null)
                foreach (var cfg in bossConfigs)
                    if (cfg.trophyObject != null)
                        cfg.trophyObject.SetActive(cfg.trophyObject == config.trophyObject && defeated);

            if (gamemodeTitle != null && !string.IsNullOrEmpty(config.nameKey))
                gamemodeTitle.text = string.Format(LocalizationManager.Get(gamemodeTitleKey), LocalizationManager.Get(config.nameKey));

            if (warningText != null && !string.IsNullOrEmpty(config.nameKey))
                warningText.text = string.Format(LocalizationManager.Get(warningTextKey), LocalizationManager.Get(config.nameKey));

            MenuManager.Instance?.SetRaidBossBackground(config.bossID);
            MenuManager.Instance?.SetRaidBossMusic(config.bossID);
        }

        private void OnEnable()
        {
            DeckSaveSystem.OnDecksChanged += RefreshAllSlots;

            if (selectedSlot != null)
            {
                selectedSlot.SetSelectedForPlay(false);
                selectedSlot = null;
            }
            SetLaunchInteractable(false);
            RefreshAllSlots();

            // Présélectionne le premier Boss de la liste (Voragoth) — évite l'incohérence
            // où le panneau affiche visuellement son contenu par défaut sans que selectedBoss
            // ne soit réellement assigné (ce qui bloquait Lancer le raid avant tout clic).
            if (bossConfigs != null && bossConfigs.Length > 0)
                SelectBoss(bossConfigs[0]);
            else
                selectedBoss = null;
        }

        private void OnDisable()
        {
            DeckSaveSystem.OnDecksChanged -= RefreshAllSlots;
        }

        private void RefreshAllSlots()
        {
            if (DeckSaveSystem.Instance == null) return;

            foreach (var slot in deckSlots)
            {
                if (slot == null || slot.slotIndex < 0) continue;

                var saved = DeckSaveSystem.Instance.GetDeckBySlot(slot.slotIndex);
                if (saved != null && saved.cardNumbers != null && saved.cardNumbers.Count > 0)
                    slot.LoadFromSave(saved.deckName, saved.cardNumbers, saved.dominantElementIndex);
                else
                    slot.SetEmpty();
            }
        }

        public void OnSlotClicked(DeckCardSlot slot)
        {
            if (slot == null) return;

            if (slot.State != DeckSlotState.Saved)
            {
                ShowFeedback(LocalizationManager.Get("ui_deck_slot_empty_hint"), false);
                return;
            }

            if (selectedSlot != null && selectedSlot != slot)
                selectedSlot.SetSelectedForPlay(false);

            if (selectedSlot == slot)
            {
                selectedSlot.SetSelectedForPlay(false);
                selectedSlot = null;
                SetLaunchInteractable(false);
                ClearFeedback();
                return;
            }

            selectedSlot = slot;
            selectedSlot.SetSelectedForPlay(true);
            SetLaunchInteractable(true);
            ShowFeedback(string.Format(LocalizationManager.Get("ui_deck_selected"), slot.DeckName), true);
        }

        private void LaunchRaid()
        {
            if (selectedBoss == null || selectedBoss.Value.phase1Data == null)
            {
                ShowFeedback(LocalizationManager.Get("ui_raid_boss_not_configured"), false);
                return;
            }

            if (selectedSlot == null)
            {
                ShowFeedback(LocalizationManager.Get("ui_select_deck_prompt"), false);
                return;
            }

            var cardNumbers = selectedSlot.CardNumbers;
            if (cardNumbers == null || cardNumbers.Count == 0)
            {
                ShowFeedback(LocalizationManager.Get("ui_deck_empty_error"), false);
                return;
            }

            var boss = selectedBoss.Value;
            GameModeContext.Mode = GameMode.Boss;
            GameModeContext.PlayerDeckNumbers = new List<int>(cardNumbers);
            GameModeContext.BossEncounterData = boss.phase1Data;
            GameModeContext.BossID = boss.bossID;
            GameModeContext.AIDisplayName = boss.phase1Data.cardName;

            if (DeckManager.Instance != null)
            {
                DeckManager.Instance.ClearDeck();
                foreach (var n in cardNumbers)
                    DeckManager.Instance.TryAddCard(n);
            }

            SceneManager.LoadScene("Combat");
        }

        private void SetLaunchInteractable(bool interactable)
        {
            if (btnLaunch != null) btnLaunch.interactable = interactable;
        }

        private void ShowFeedback(string message, bool success)
        {
            if (feedbackText == null) return;
            feedbackText.text  = message;
            feedbackText.color = success
                ? new Color(0.4f, 0.9f, 0.4f)
                : new Color(1f, 0.4f, 0.4f);
        }

        private void ClearFeedback()
        {
            if (feedbackText != null) feedbackText.text = "";
        }
    }
}
