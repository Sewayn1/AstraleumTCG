using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Astraleum.UI;
using TMPro;

public static class DeckSlotsManagerSetup
{
    [MenuItem("Tools/Astraleum/Wire DeckSlotsManager")]
    public static void WireReferences()
    {
        var panel = GameObject.Find("Panel_DecksSlots");
        if (panel == null) { Debug.LogError("[Setup] Panel_DecksSlots not found."); return; }

        var mgr = panel.GetComponent<DeckSlotsManager>();
        if (mgr == null) { Debug.LogError("[Setup] DeckSlotsManager not found."); return; }

        var slotsContainer = panel.transform.Find("SlotsContainer");
        if (slotsContainer == null) { Debug.LogError("[Setup] SlotsContainer not found."); return; }

        var slots = new DeckCardSlot[8];
        for (int i = 0; i < 8; i++)
        {
            var child = slotsContainer.Find("Deck_Slot_" + (i + 1));
            if (child == null) { Debug.LogError("[Setup] Deck_Slot_" + (i + 1) + " not found."); return; }
            slots[i] = child.GetComponent<DeckCardSlot>();
        }
        mgr.deckSlots = slots;

        var editZoneTf = panel.transform.Find("EditZone");
        if (editZoneTf == null) { Debug.LogError("[Setup] EditZone not found."); return; }
        mgr.editZone = editZoneTf.GetComponent<RectTransform>();

        var cardSlotsRow = editZoneTf.Find("CardSlotsRow");
        if (cardSlotsRow == null) { Debug.LogError("[Setup] CardSlotsRow not found."); return; }

        var cardSlots = new CardSelectSlot[5];
        for (int i = 0; i < 5; i++)
        {
            var child = cardSlotsRow.Find("CardSlot_" + (i + 1));
            if (child != null) cardSlots[i] = child.GetComponent<CardSelectSlot>();
        }
        mgr.cardSelectSlots = cardSlots;

        var inputTf = editZoneTf.Find("DeckNameInput");
        if (inputTf != null)
        {
            var inputField = inputTf.GetComponent<TMP_InputField>();
            if (inputField != null)
            {
                SetupInputField(inputTf.gameObject, inputField);
                mgr.deckNameInput = inputField;
            }
        }

        var btnRow = editZoneTf.Find("ButtonRow");
        if (btnRow != null)
        {
            var s = btnRow.Find("BtnSave");
            if (s != null) mgr.btnSave = s.GetComponent<Button>();
            var d = btnRow.Find("BtnDelete");
            if (d != null) mgr.btnDelete = d.GetComponent<Button>();
        }

        var feedbackTf = editZoneTf.Find("FeedbackText");
        if (feedbackTf != null) mgr.feedbackText = feedbackTf.GetComponent<TMP_Text>();

        EditorUtility.SetDirty(mgr);
        Debug.Log("[Setup] DeckSlotsManager wired successfully.");
    }

    private static void SetupInputField(GameObject go, TMP_InputField inputField)
    {
        if (inputField.textComponent != null) return;

        var taGO = new GameObject("Text Area");
        taGO.transform.SetParent(go.transform, false);
        var taRT = taGO.AddComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero;
        taRT.anchorMax = Vector2.one;
        taRT.sizeDelta = new Vector2(-20f, -13f);
        taRT.anchoredPosition = Vector2.zero;
        var taImg = taGO.AddComponent<Image>();
        taImg.color = new Color(0, 0, 0, 0);
        var mask = taGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(taGO.transform, false);
        var phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.sizeDelta = Vector2.zero;
        phRT.anchoredPosition = Vector2.zero;
        var phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = "Nom du deck...";
        phTMP.color = new Color(1f, 1f, 1f, 0.4f);
        phTMP.fontSize = 16;
        phTMP.fontStyle = FontStyles.Italic;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(taGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.anchoredPosition = Vector2.zero;
        var textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.text = "";
        textTMP.color = Color.white;
        textTMP.fontSize = 16;

        inputField.textViewport = taRT;
        inputField.textComponent = textTMP;
        inputField.placeholder = phTMP;

        EditorUtility.SetDirty(go);
        Debug.Log("[Setup] TMP_InputField set up on " + go.name);
    }
}
