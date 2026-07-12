using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Astraleum
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [Header("État de la partie")]
        public int winnerPlayerID = -1;

        // Positionné avant un retour au MainMenu suite à déconnexion adverse mid-combat ;
        // consommé par Panel_LeaveGame.Start() (persiste via ce singleton DontDestroyOnLoad).
        public static bool ShowLeaveGameNotice = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadScene(string sceneName)
            => SceneManager.LoadScene(sceneName);

        public void LoadScene(int sceneIndex)
            => SceneManager.LoadScene(sceneIndex);

        public void EndGame(int winnerID)
        {
            winnerPlayerID = winnerID;
            EndGameHandler.Instance?.ShowEndGame(winnerID);
        }

        public void ReturnToMainMenu()
        {
            if (NetworkBridge.IsActive)
            {
                _ = SignalRGameClient.Instance?.DisconnectAsync();
                NetworkBridge.Reset();
            }
            AI.GameModeContext.Reset();
            LoadScene("MainMenu");
        }

        /// <summary>
        /// Retourne au menu principal après un délai.
        /// Coroutine hébergée sur GameManager (DontDestroyOnLoad) pour survivre
        /// à la destruction des objets réseau lors de StopClient().
        /// </summary>
        public void ReturnToMainMenuDelayed(float delay = 1f)
        {
            StartCoroutine(DelayedReturn(delay));
        }

        private IEnumerator DelayedReturn(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToMainMenu();
        }

        public void GiveUp()
        {
            if (NetworkBridge.IsActive)
                NetworkBridge.OnGiveUpRequested?.Invoke(NetworkBridge.LocalPlayerID);
            else
                ReturnToMainMenu();
        }

        public void QuitGame()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}