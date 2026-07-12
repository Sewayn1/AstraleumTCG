using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace Astraleum
{
    /// <summary>
    /// Affiche le nombre de joueurs en ligne sur le serveur de matchmaking.
    /// À attacher sur un TMP_Text dans le menu principal.
    /// </summary>
    public class OnlinePlayersDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text lblOnline;
        [SerializeField] private float    pingInterval = 60f;

        private string _sessionId;

        private void Start()
        {
            if (Application.isBatchMode) return;

            // ID de session persistant par machine
            _sessionId = PlayerPrefs.GetString("OnlineSessionId", "");
            if (string.IsNullOrEmpty(_sessionId))
            {
                _sessionId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("OnlineSessionId", _sessionId);
                PlayerPrefs.Save();
            }

            StartCoroutine(PingLoop());
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        private IEnumerator PingLoop()
        {
            while (true)
            {
                yield return StartCoroutine(SendPing());
                yield return new WaitForSeconds(pingInterval);
            }
        }

        private IEnumerator SendPing()
        {
            string url       = NetworkConfig.Data.matchmakingUrl + "/players/ping";
            byte[] body      = System.Text.Encoding.UTF8.GetBytes(
                                   "{\"sessionId\":\"" + _sessionId + "\"}");

            UnityWebRequest req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout         = 10;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                PingResponse data = JsonUtility.FromJson<PingResponse>(req.downloadHandler.text);
                UpdateLabel(data.online);
            }
            else
            {
                UpdateLabel(-1);
            }

            req.Dispose();
        }

        private void UpdateLabel(int count)
        {
            if (lblOnline == null) return;
            lblOnline.text = count < 0
                ? LocalizationManager.Get("ui_online_offline")
                : LocalizationManager.Get("ui_online_count", count, count > 1 ? "s" : "");
        }

        [Serializable]
        private class PingResponse { public int online; }
    }
}
