using System;
using System.Collections.Generic;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Gestionnaire de localisation. Charge fr.json / en.json depuis Resources/Localization/.
    /// Compatible Weblate (format Simple JSON plat).
    /// </summary>
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        // ── Langues supportées ────────────────────────────────────────
        public enum Language { FR, EN }

        public static Language CurrentLanguage { get; private set; } = Language.FR;

        /// <summary>Déclenché après chaque changement de langue.</summary>
        public static event Action OnLanguageChanged;

        private static Dictionary<string, string> _table = new Dictionary<string, string>();
        private static bool _initialized = false;

        private const string PlayerPrefsKey = "AstraleumLanguage";

        // ── Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Language saved = (Language)PlayerPrefs.GetInt(PlayerPrefsKey, (int)Language.FR);
            LoadLanguage(saved);
        }

        /// <summary>
        /// Garantit que la table est chargée même sans MonoBehaviour dans la scène.
        /// Utile quand on lance directement une scène autre que MainMenu en éditeur.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            Language saved = (Language)PlayerPrefs.GetInt(PlayerPrefsKey, (int)Language.FR);
            LoadLanguage(saved);
        }

        // ── API publique ──────────────────────────────────────────────

        /// <summary>Retourne la traduction pour une clé UI. Fallback = clé brute.</summary>
        public static string Get(string key)
        {
            EnsureInitialized();

            if (_table.TryGetValue(key, out string value))
                return value;

            Debug.LogWarning($"[Localization] Clé manquante : '{key}'");
            return key;
        }

        /// <summary>Traduction avec arguments (String.Format). Ex: Get("ui_dmg", 42) → "Inflige 42 dégâts"</summary>
        public static string Get(string key, params object[] args)
        {
            string raw = Get(key);
            try   { return string.Format(raw, args); }
            catch { return raw; }
        }

        /// <summary>
        /// Retourne le texte localisé d'un champ de carte.
        /// Champs valides : name, title, skill1_name, skill1_desc, skill2_name, skill2_desc,
        ///                  passive_name, passive_desc, lore, quote.
        /// Fallback = chaîne vide (l'appelant peut alors utiliser la valeur du ScriptableObject).
        /// </summary>
        public static string GetCard(int cardNumber, string field)
        {
            string key = $"card_{cardNumber}_{field}";
            if (_table.TryGetValue(key, out string value))
                return value;
            return string.Empty;   // signal "non traduit → utiliser SO"
        }

        /// <summary>
        /// Retourne le texte localisé d'un champ de carte, avec fallback automatique sur la valeur SO.
        /// </summary>
        public static string GetCard(int cardNumber, string field, string fallback)
        {
            string result = GetCard(cardNumber, field);
            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        /// <summary>Change la langue active et recharge la table.</summary>
        public static void SetLanguage(Language lang)
        {
            if (lang == CurrentLanguage) return;
            LoadLanguage(lang);
            PlayerPrefs.SetInt(PlayerPrefsKey, (int)lang);
            PlayerPrefs.Save();
            OnLanguageChanged?.Invoke();
        }

        // ── Chargement JSON ───────────────────────────────────────────

        private static void LoadLanguage(Language lang)
        {
            CurrentLanguage = lang;
            _initialized    = true;

            string filename = lang == Language.FR ? "fr" : "en";
            TextAsset asset = Resources.Load<TextAsset>($"Localization/{filename}");

            if (asset == null)
            {
                Debug.LogError($"[Localization] Fichier manquant : Resources/Localization/{filename}.json");
                return;
            }

            _table = ParseJson(asset.text);
            Debug.Log($"[Localization] Langue chargée : {lang} ({_table.Count} clés)");
        }

        /// <summary>
        /// Parser JSON minimaliste (objet plat uniquement).
        /// Unity's JsonUtility ne supporte pas les dictionnaires — on parse manuellement
        /// pour éviter une dépendance externe. Compatible avec le format Simple JSON de Weblate.
        /// </summary>
        private static Dictionary<string, string> ParseJson(string json)
        {
            var dict = new Dictionary<string, string>();

            // Retire les accolades extérieures
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}"))   json = json.Substring(0, json.Length - 1);

            int i = 0;
            while (i < json.Length)
            {
                // Cherche la prochaine clé (délimitée par ")
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0) break;
                int keyEnd = json.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;
                string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);

                // Cherche le séparateur :
                int colon = json.IndexOf(':', keyEnd + 1);
                if (colon < 0) break;

                // Cherche la valeur (délimitée par ")
                int valStart = json.IndexOf('"', colon + 1);
                if (valStart < 0) break;

                // Parcourt la valeur caractère par caractère pour gérer les échappements
                int valEnd = valStart + 1;
                while (valEnd < json.Length)
                {
                    if (json[valEnd] == '\\') { valEnd += 2; continue; }
                    if (json[valEnd] == '"')  break;
                    valEnd++;
                }
                string value = json.Substring(valStart + 1, valEnd - valStart - 1);

                // Décode les séquences d'échappement courantes
                value = value.Replace("\\n", "\n")
                             .Replace("\\t", "\t")
                             .Replace("\\\"", "\"")
                             .Replace("\\\\", "\\");

                dict[key] = value;
                i = valEnd + 1;
            }

            return dict;
        }
    }
}
