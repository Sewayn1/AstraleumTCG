using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Astraleum
{
    /// <summary>
    /// Vérifie au démarrage du MainMenu si une nouvelle version du jeu est disponible.
    /// Si oui, affiche Panel_UpdateAvailable et bloque la navigation.
    /// Attacher sur un GO actif dans la scène MainMenu (ex. SettingsInitializer).
    /// </summary>
    public class UpdateChecker : MonoBehaviour
    {
        private const string ManifestUrl =
            "https://raw.githubusercontent.com/Sewayn1/AstraleumTCG/main/manifest.json";

        private void Start()
        {
            StartCoroutine(CheckForUpdate());
        }

        private IEnumerator CheckForUpdate()
        {
            string url = $"{ManifestUrl}?_={System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Cache-Control", "no-cache");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[UpdateChecker] Impossible de vérifier les mises à jour : {req.error}");
                yield break;
            }

            var manifest = JsonUtility.FromJson<ManifestData>(req.downloadHandler.text);
            if (manifest == null || string.IsNullOrEmpty(manifest.version))
            {
                Debug.LogWarning("[UpdateChecker] Manifest invalide ou vide.");
                yield break;
            }

            string local = Application.version;
            if (IsNewer(manifest.version, local))
            {
                Debug.Log($"[UpdateChecker] Mise à jour disponible : {local} → {manifest.version}");
                PanelUpdateAvailable.Instance?.Show(manifest.version);
            }
            else
            {
                Debug.Log($"[UpdateChecker] Jeu à jour ({local}).");
            }
        }

        // Retourne true si remote est strictement plus récent que local.
        private static bool IsNewer(string remote, string local)
        {
            var r = remote?.Split('.') ?? System.Array.Empty<string>();
            var l = local?.Split('.')  ?? System.Array.Empty<string>();
            int len = Mathf.Max(r.Length, l.Length);
            for (int i = 0; i < len; i++)
            {
                int rv = i < r.Length ? ParseSegment(r[i]) : 0;
                int lv = i < l.Length ? ParseSegment(l[i]) : 0;
                if (rv > lv) return true;
                if (rv < lv) return false;
            }
            return false;
        }

        // Parse un segment de version en ignorant les suffixes texte (ex. "4a" → 4).
        private static int ParseSegment(string s)
        {
            int end = 0;
            while (end < s.Length && char.IsDigit(s[end])) end++;
            return end > 0 && int.TryParse(s[..end], out int v) ? v : 0;
        }

        [System.Serializable]
        private class ManifestData
        {
            public string version;
        }
    }
}
