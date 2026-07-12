#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;

public static class CreateCriticalHitAnnouncer
{
    [MenuItem("Tools/Create CriticalHitAnnouncer in Combat")]
    public static void Create()
    {
        // Trouve le Canvas dans la scène active
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[CriticalHitAnnouncer] Aucun Canvas trouvé dans la scène active.");
            return;
        }

        // Supprime l'ancien si existant
        var old = canvas.transform.Find("CriticalHitAnnouncer");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        // Panel principal — recouvre tout l'écran
        var panelGO = new GameObject("CriticalHitAnnouncer");
        panelGO.transform.SetParent(canvas.transform, false);

        var rt = panelGO.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;

        // Script
        var script = panelGO.AddComponent<Astraleum.CriticalHitAnnouncer>();

        // Texte "Coup Critique!" centré
        var textGO = new GameObject("CritText");
        textGO.transform.SetParent(panelGO.transform, false);

        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin        = new Vector2(0.5f, 0.5f);
        trt.anchorMax        = new Vector2(0.5f, 0.5f);
        trt.pivot            = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta        = new Vector2(600f, 120f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Coup Critique!";
        tmp.fontSize  = 56f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = new Color(0.95f, 0.15f, 0.15f);
        tmp.alignment = TextAlignmentOptions.Center;

        // Lier la référence dans le script
        script.critText = tmp;

        // Le GO doit démarrer actif (pour que Awake() s'exécute et assigne Instance)
        // Awake() le désactivera immédiatement
        panelGO.SetActive(true);

        EditorUtility.SetDirty(panelGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[CriticalHitAnnouncer] Créé avec succès sous le Canvas !");
    }
}
#endif
