using UnityEngine;
using UnityEngine.SceneManagement;

namespace Astraleum
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [Header("État de la partie")]
        public int winnerPlayerID = -1;

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
                var nm = Mirror.NetworkManager.singleton;
                if (nm != null)
                {
                    if (Mirror.NetworkServer.active) nm.StopHost();
                    else                             nm.StopClient();
                }
            }
            LoadScene("MainMenu");
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