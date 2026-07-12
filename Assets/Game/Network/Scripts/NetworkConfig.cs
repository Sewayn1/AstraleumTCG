using System.IO;
using UnityEngine;

namespace Astraleum
{
    [System.Serializable]
    public class NetworkConfigData
    {
        public string matchmakingUrl = "http://localhost:5000";
        public string signalRUrl     = "http://localhost:5000/gamehub";
    }

    public static class NetworkConfig
    {
        private static NetworkConfigData _data;

        public static NetworkConfigData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        private static void Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "network_config.json");
            if (File.Exists(path))
            {
                _data = JsonUtility.FromJson<NetworkConfigData>(File.ReadAllText(path));
                Debug.Log($"[NetworkConfig] matchmaking={_data.matchmakingUrl} | signalR={_data.signalRUrl}");
            }
            else
            {
                _data = new NetworkConfigData();
                Debug.LogWarning("[NetworkConfig] network_config.json introuvable — valeurs par défaut utilisées.");
            }
        }
    }
}
