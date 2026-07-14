using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace Astraleum.UI
{
    public class MenuManager : MonoBehaviour
    {
        public static MenuManager Instance;

        [Header("Panels principaux")]
        public GameObject panelMainMenu;
        public GameObject panelPlay;
        public GameObject panelCollection;
        public GameObject panelShop;
        public GameObject panelLadder;
        public GameObject panelProfile;
        public GameObject panelSettings;
        public GameObject panelHowToPlay;
        public GameObject panelChangelogs;

        [Header("Panels Jouer")]
        public GameObject panelMatchmakingRanked;
        public GameObject panelMatchmakingUnranked;
        public GameObject panelSelectionDeck;
        public GameObject panelRaid;

        [Header("Panels Collection")]
        public GameObject panelMyCards;
        public GameObject panelDeckEditor;

        [Header("Panels Classement")]
        public GameObject panelMyRank;
        public GameObject panelLeaderboard;
        public GameObject panelHistory;

        [Header("Panels Profil")]
        public GameObject panelStatistics;
        public GameObject panelSeasons;
        public GameObject panelAchievements;
        public GameObject panelPersonnalisation;

        [Header("Panels Boutique")]
        public GameObject panelTaux;

        [Header("Panels Parametres")]
        public GameObject panelGraphics;
        public GameObject panelAudio;
        public GameObject panelAccess;

        [Header("Éléments visibles uniquement sur le Menu Principal")]
        [Tooltip("Ces GOs s'affichent sur le Menu Principal et se masquent sur tous les sous-panels.")]
        public GameObject[] mainMenuOnlyElements;

        [Header("Fond dynamique — Panel_Raid")]
        [Tooltip("Fond vidéo animé par défaut — coupé sur Panel_Raid pour économiser des performances.")]
        public GameObject bgAnimated;
        [Tooltip("Fond dédié à Voragoth — actif quand ce Boss est sélectionné sur Panel_Raid.")]
        public GameObject bgVoragoth;
        [Tooltip("Fond dédié à Vaelthor — actif quand ce Boss est sélectionné sur Panel_Raid.")]
        public GameObject bgVaelthor;
        [Tooltip("Durée du fondu enchaîné entre les fonds (secondes).")]
        public float bgFadeDuration = 1.5f;

        [Header("Musique — Panel_Raid")]
        [Tooltip("AudioSource unique du menu (Canvas) — sa piste est remplacée à l'entrée/sortie de Panel_Raid.")]
        public AudioSource musicSource;
        [Tooltip("Musique jouée sur Panel_Raid par défaut (Boss sans piste dédiée, ex. Voragoth).")]
        public AudioClip bossSelectionMusic;
        [Tooltip("Musique dédiée jouée quand Vaelthor est sélectionné sur Panel_Raid — remplace bossSelectionMusic.")]
        public AudioClip vaelthorSelectMusic;
        [Tooltip("Durée du fondu sortant avant la bascule de piste (secondes).")]
        public float musicFadeDuration = 1f;

        [Header("Transitions")]
        [SerializeField] private float panelDelay = 0.2f;

        private GameObject currentPanel;
        private Stack<GameObject> navigationHistory = new Stack<GameObject>();
        private AudioClip defaultMusic;
        private float defaultMusicVolume;
        private Coroutine musicFadeRoutine;
        private VideoPlayer bgAnimatedVP;
        private VideoPlayer bgVoragothVP;
        private VideoPlayer bgVaelthorVP;
        private Coroutine bgFadeRoutine;
        private bool isOnRaidBackground; // évite de relancer le fondu sur chaque changement de panel hors Raid
        private GameObject currentRaidBossBg;   // fond Boss actuellement actif sur Panel_Raid (bgVoragoth/bgVaelthor)
        private VideoPlayer currentRaidBossBgVP;
        private AudioClip currentRaidMusic;     // piste Boss actuellement sélectionnée sur Panel_Raid

        private void Awake() => Instance = this;

        private void Start()
        {
            // Auto-détection si non assignés en inspecteur
            if (mainMenuOnlyElements == null || mainMenuOnlyElements.Length == 0)
            {
                var list = new System.Collections.Generic.List<GameObject>();
                var version = GameObject.Find("Version");
                var lbl     = GameObject.Find("lblOnlinePlayers");
                if (version != null) list.Add(version);
                if (lbl     != null) list.Add(lbl);
                mainMenuOnlyElements = list.ToArray();
            }

            if (musicSource != null)
            {
                defaultMusic       = musicSource.clip;
                defaultMusicVolume = musicSource.volume;
            }

            bgAnimatedVP = bgAnimated != null ? bgAnimated.GetComponent<VideoPlayer>() : null;
            bgVoragothVP = bgVoragoth != null ? bgVoragoth.GetComponent<VideoPlayer>() : null;
            bgVaelthorVP = bgVaelthor != null ? bgVaelthor.GetComponent<VideoPlayer>() : null;

            // BossBG.mp4 (366 Mo) vit dans StreamingAssets/ plutôt qu'importé comme VideoClip —
            // évite qu'il soit fondu dans les fichiers sharedassetsN.resource du build (Application.
            // streamingAssetsPath n'est résolvable qu'au runtime, jamais en dur dans l'Inspector).
            if (bgVoragothVP != null)
            {
                bgVoragothVP.source = VideoSource.Url;
                bgVoragothVP.url = System.IO.Path.Combine(Application.streamingAssetsPath, "BossBG.mp4");
            }
            // bgVaelthorVP utilise directement son VideoClip assigné en Inspector (fichier plus léger,
            // pas besoin du contournement StreamingAssets).

            // Fond par défaut affiché en entrant sur Panel_Raid avant toute sélection — premier Boss
            // de la liste, cohérent avec le comportement historique (Voragoth était le seul Boss).
            currentRaidBossBg   = bgVoragoth;
            currentRaidBossBgVP = bgVoragothVP;
            currentRaidMusic    = bossSelectionMusic;

            ShowPanel(panelMainMenu);

            foreach (var btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (btn.name == "Btn_Quit")
                    btn.onClick.AddListener(QuitGame);
                if (btn.name == "Btn_Raid")
                    btn.onClick.AddListener(OpenRaid);
            }

            var logoBtn = GameObject.Find("LogoIMG")?.GetComponent<Button>();
            if (logoBtn != null)
                logoBtn.onClick.AddListener(() => ShowPanel(panelMainMenu));
        }

        // ── Navigation ────────────────────────────────────────────────

        public void ShowPanel(GameObject target)
        {
            if (target == currentPanel) return;

            DOVirtual.DelayedCall(panelDelay, () =>
            {
                if (currentPanel != null)
                {
                    navigationHistory.Push(currentPanel);
                    // panelMainMenu ne se désactive jamais — les boutons et le logo restent visibles
                    if (currentPanel != panelMainMenu)
                        currentPanel.SetActive(false);
                }

                currentPanel = target;

                if (target != panelMainMenu)
                    target.SetActive(true);

                SetMainMenuOnlyVisible(target == panelMainMenu);
                UpdateRaidBackground(target == panelRaid);
                UpdateRaidMusic(target == panelRaid);

            }, ignoreTimeScale: true);
        }

        public void GoBack()
        {
            if (navigationHistory.Count == 0) return;
            DOVirtual.DelayedCall(panelDelay, () =>
            {
                if (currentPanel != panelMainMenu)
                    currentPanel.SetActive(false);

                currentPanel = navigationHistory.Pop();

                if (currentPanel != panelMainMenu)
                    currentPanel.SetActive(true);

                SetMainMenuOnlyVisible(currentPanel == panelMainMenu);
                UpdateRaidBackground(currentPanel == panelRaid);
                UpdateRaidMusic(currentPanel == panelRaid);

            }, ignoreTimeScale: true);
        }

        private void SetMainMenuOnlyVisible(bool visible)
        {
            foreach (var go in mainMenuOnlyElements)
                if (go != null) go.SetActive(visible);
        }

        private void UpdateRaidBackground(bool onRaidPanel)
        {
            if (onRaidPanel == isOnRaidBackground) return; // déjà dans le bon état — pas de transition à jouer
            isOnRaidBackground = onRaidPanel;

            GameObject  fadeInGO  = onRaidPanel ? currentRaidBossBg   : bgAnimated;
            VideoPlayer fadeInVP  = onRaidPanel ? currentRaidBossBgVP : bgAnimatedVP;
            GameObject  fadeOutGO = onRaidPanel ? bgAnimated          : currentRaidBossBg;
            VideoPlayer fadeOutVP = onRaidPanel ? bgAnimatedVP        : currentRaidBossBgVP;

            if (bgFadeRoutine != null) StopCoroutine(bgFadeRoutine);
            bgFadeRoutine = StartCoroutine(FadeBackgrounds(fadeInGO, fadeInVP, fadeOutGO, fadeOutVP));
        }

        /// <summary>
        /// Appelé par RaidPanelController quand un Boss est sélectionné dans le sélecteur multi-boss
        /// — bascule le fond vidéo vers celui du Boss choisi, avec le même fondu enchaîné que
        /// l'entrée/sortie de Panel_Raid. Sans effet si on n'est pas actuellement sur Panel_Raid
        /// (le nouveau fond sera simplement celui utilisé à la prochaine entrée sur le panel).
        /// </summary>
        public void SetRaidBossBackground(int bossID)
        {
            GameObject  newBg = bossID == 1 ? bgVaelthor   : bgVoragoth;
            VideoPlayer newVP = bossID == 1 ? bgVaelthorVP : bgVoragothVP;
            if (newBg == currentRaidBossBg) return; // déjà actif, rien à faire

            GameObject  oldBg = currentRaidBossBg;
            VideoPlayer oldVP = currentRaidBossBgVP;
            currentRaidBossBg   = newBg;
            currentRaidBossBgVP = newVP;

            if (!isOnRaidBackground) return; // pas encore affiché — pris en compte à la prochaine UpdateRaidBackground(true)

            if (bgFadeRoutine != null) StopCoroutine(bgFadeRoutine);
            bgFadeRoutine = StartCoroutine(FadeBackgrounds(newBg, newVP, oldBg, oldVP));
        }

        private IEnumerator FadeBackgrounds(GameObject fadeInGO, VideoPlayer fadeInVP, GameObject fadeOutGO, VideoPlayer fadeOutVP)
        {
            if (fadeInGO != null)
            {
                fadeInGO.SetActive(true);
                if (fadeInVP != null) fadeInVP.targetCameraAlpha = 0f;
            }

            float t = 0f;
            while (t < bgFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float ratio = Mathf.Clamp01(t / bgFadeDuration);
                if (fadeInVP  != null) fadeInVP.targetCameraAlpha  = ratio;
                if (fadeOutVP != null) fadeOutVP.targetCameraAlpha = 1f - ratio;
                yield return null;
            }

            if (fadeInVP != null) fadeInVP.targetCameraAlpha = 1f;

            // Coupé après le fondu (perf) — alpha remis à 1 pour la prochaine activation. Ignoré si
            // fadeOutGO == fadeInGO (cas SetRaidBossBackground appelé avant toute entrée sur le panel).
            if (fadeOutGO != null && fadeOutGO != fadeInGO)
                fadeOutGO.SetActive(false);
            if (fadeOutVP != null) fadeOutVP.targetCameraAlpha = 1f;

            bgFadeRoutine = null;
        }

        private void UpdateRaidMusic(bool onRaidPanel)
        {
            if (musicSource == null) return;

            AudioClip target = onRaidPanel ? currentRaidMusic : defaultMusic;
            if (target == null || musicSource.clip == target) return;

            if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = StartCoroutine(FadeToMusic(target));
        }

        /// <summary>
        /// Appelé par RaidPanelController quand un Boss est sélectionné dans le sélecteur multi-boss
        /// — bascule la musique vers celle du Boss choisi (vaelthorSelectMusic pour Vaelthor,
        /// bossSelectionMusic générique sinon), avec le même fondu que l'entrée/sortie de Panel_Raid.
        /// Sans effet si on n'est pas actuellement sur Panel_Raid (prise en compte à la prochaine entrée).
        /// </summary>
        public void SetRaidBossMusic(int bossID)
        {
            AudioClip target = (bossID == 1 && vaelthorSelectMusic != null) ? vaelthorSelectMusic : bossSelectionMusic;
            if (target == currentRaidMusic) return; // déjà actif, rien à faire

            currentRaidMusic = target;

            if (!isOnRaidBackground || musicSource == null) return; // pas encore audible — pris en compte à la prochaine UpdateRaidMusic(true)
            if (target == null || musicSource.clip == target) return;

            if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = StartCoroutine(FadeToMusic(target));
        }

        private IEnumerator FadeToMusic(AudioClip target)
        {
            float startVolume = musicSource.volume;
            float t = 0f;
            while (t < musicFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, t / musicFadeDuration);
                yield return null;
            }

            musicSource.volume = 0f;
            musicSource.clip = target;
            musicSource.Play();
            musicSource.volume = defaultMusicVolume;

            musicFadeRoutine = null;
        }

        // ── Raccourcis ────────────────────────────────────────────────

        public void OpenHowToPlay()           => ShowPanel(panelHowToPlay);
        public void OpenPlay()                => ShowPanel(panelPlay);
        public void OpenChangelogs()          => ShowPanel(panelChangelogs);
        public void OpenCollection()          => ShowPanel(panelCollection);
        public void OpenShop()                => ShowPanel(panelShop);
        public void OpenLadder()              => ShowPanel(panelLadder);
        public void OpenProfile()             => ShowPanel(panelProfile);
        public void OpenSettings()            => ShowPanel(panelSettings);
        public void OpenMatchmakingRanked()   => ShowPanel(panelMatchmakingRanked);
        public void OpenMatchmakingUnranked() => ShowPanel(panelMatchmakingUnranked);
        public void OpenSelectionDeck()       => ShowPanel(panelSelectionDeck);
        public void OpenRaid()                => ShowPanel(panelRaid);
        public void OpenMyCards()             => ShowPanel(panelMyCards);
        public void OpenDeckEditor()          => ShowPanel(panelDeckEditor);
        public void OpenMyRank()              => ShowPanel(panelMyRank);
        public void OpenLeaderboard()         => ShowPanel(panelLeaderboard);
        public void OpenHistory()             => ShowPanel(panelHistory);
        public void OpenStatistics()          => ShowPanel(panelStatistics);
        public void OpenSeasons()             => ShowPanel(panelSeasons);
        public void OpenAchievements()        => ShowPanel(panelAchievements);
        public void OpenPersonnalisation()    => ShowPanel(panelPersonnalisation);
        public void OpenTaux()                => panelTaux.SetActive(true);
        public void CloseTaux()               => panelTaux.SetActive(false);

        public void QuitGame() => GameManager.Instance?.QuitGame();

        public void OpenGraphics() => SwitchSettingsPanel(panelGraphics);
        public void OpenAudio()    => SwitchSettingsPanel(panelAudio);
        public void OpenAccess()   => SwitchSettingsPanel(panelAccess);

        private void SwitchSettingsPanel(GameObject target)
        {
            panelGraphics.SetActive(false);
            panelAudio.SetActive(false);
            panelAccess.SetActive(false);
            target.SetActive(true);
        }
    }
}
