using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

        private GameObject currentPanel;
        private Stack<GameObject> navigationHistory = new Stack<GameObject>();

        private void Awake() => Instance = this;

        private void Start()
        {
            ShowPanel(panelMainMenu);

            var btnQuit = GameObject.Find("Btn_Quit")?.GetComponent<Button>();
            if (btnQuit != null)
                btnQuit.onClick.AddListener(QuitGame);
        }

        public void ShowPanel(GameObject target)
        {
            if (currentPanel != null) {
                navigationHistory.Push(currentPanel);
                currentPanel.SetActive(false);
            }
            currentPanel = target;
            currentPanel.SetActive(true);
        }

        public void GoBack()
        {
            if (navigationHistory.Count == 0) return;
            currentPanel.SetActive(false);
            currentPanel = navigationHistory.Pop();
            currentPanel.SetActive(true);
        }

        public void OpenHowToPlay()          => ShowPanel(panelHowToPlay);
        public void OpenPlay()              => ShowPanel(panelPlay);
        public void OpenChangelogs()              => ShowPanel(panelChangelogs);
        public void OpenCollection()         => ShowPanel(panelCollection);
        public void OpenShop()           => ShowPanel(panelShop);
        public void OpenLadder()         => ShowPanel(panelLadder);
        public void OpenProfile()             => ShowPanel(panelProfile);
        public void OpenSettings()         => ShowPanel(panelSettings);
        public void OpenMatchmakingRanked()  => ShowPanel(panelMatchmakingRanked);
        public void OpenMatchmakingUnranked()   => ShowPanel(panelMatchmakingUnranked);
        public void OpenSelectionDeck()      => ShowPanel(panelSelectionDeck);
        public void OpenMyCards()          => ShowPanel(panelMyCards);
        public void OpenDeckEditor()        => ShowPanel(panelDeckEditor);
        public void OpenMyRank()            => ShowPanel(panelMyRank);
        public void OpenLeaderboard()        => ShowPanel(panelLeaderboard);
        public void OpenHistory()         => ShowPanel(panelHistory);
        public void OpenStatistics()       => ShowPanel(panelStatistics);
        public void OpenSeasons()            => ShowPanel(panelSeasons);
        public void OpenAchievements()             => ShowPanel(panelAchievements);
        public void OpenPersonnalisation()   => ShowPanel(panelPersonnalisation);
        public void OpenTaux()               => panelTaux.SetActive(true);
        public void CloseTaux()              => panelTaux.SetActive(false);

        public void QuitGame()               => GameManager.Instance?.QuitGame();

        public void OpenGraphics()         => SwitchSettingsPanel(panelGraphics);
        public void OpenAudio()              => SwitchSettingsPanel(panelAudio);
        public void OpenAccess()      => SwitchSettingsPanel(panelAccess);

        private void SwitchSettingsPanel(GameObject target)
        {
            panelGraphics.SetActive(false);
            panelAudio.SetActive(false);
            panelAccess.SetActive(false);
            target.SetActive(true);
        }
    }
}