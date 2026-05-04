using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum
{

public class CombatLogManager : MonoBehaviour
{
    public static CombatLogManager Instance;

    [Header("Templates")]
    public GameObject logEntryTemplate;
    public GameObject logDeathTemplate;

    [Header("References")]
    public RectTransform logContent;
    public GameObject    logContentZone;
    public TMP_Text      toggleArrow;
    public ScrollRect    logScrollRect;

    [Header("Couleurs joueurs")]
    [SerializeField] private Color player1Color = new Color(0.30f, 0.85f, 0.40f, 1f); // vert
    [SerializeField] private Color player2Color = new Color(0.95f, 0.40f, 0.30f, 1f); // rouge-orangé
    [SerializeField] private Color neutralColor = new Color(0.85f, 0.85f, 0.85f, 1f); // gris clair
    [SerializeField] private Color deathColor   = new Color(1.00f, 0.25f, 0.25f, 1f); // rouge vif

    private bool isExpanded  = true;
    private int  currentTurn = 1;

    private const int MAX_ENTRIES = 50;
    private readonly List<GameObject> entries = new List<GameObject>();

    private void Awake() => Instance = this;

    public void ToggleLog()
    {
        isExpanded = !isExpanded;
        logContentZone.SetActive(isExpanded);
        toggleArrow.text = isExpanded ? "▲" : "▼";
    }

    // playerID : 0 = J1 (vert), 1 = J2 (rouge-orange), -1 = neutre
    public void AddEntry(string message, bool isDeathEntry = false, int playerID = -1)
    {
        if (logContent == null) return;

        var template = isDeathEntry ? logDeathTemplate : logEntryTemplate;
        if (template == null) return;

        var entry = Instantiate(template, logContent);
        entry.SetActive(true);

        var turnTxt  = entry.transform.Find("TurnNum")?.GetComponent<TMP_Text>();
        var entryTxt = entry.transform.Find("EntryText")?.GetComponent<TMP_Text>();

        if (turnTxt  != null) turnTxt.text = $"T{currentTurn}";
        if (entryTxt != null)
        {
            entryTxt.text  = message;
            entryTxt.color = isDeathEntry  ? deathColor   :
                             playerID == 0 ? player1Color :
                             playerID == 1 ? player2Color :
                             neutralColor;
        }

        entries.Add(entry);

        while (entries.Count > MAX_ENTRIES)
        {
            Destroy(entries[0]);
            entries.RemoveAt(0);
        }

        StartCoroutine(ScrollToBottom());
    }

    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        if (logScrollRect != null)
            logScrollRect.verticalNormalizedPosition = 0f;
    }

    public void OnTurnChanged(int newTurn) => currentTurn = newTurn;
}

} // namespace Astraleum
