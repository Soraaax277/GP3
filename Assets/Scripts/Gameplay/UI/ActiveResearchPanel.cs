using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Attach this to any empty GameObject (e.g. "UIManager").
// Assign the panel, ScrollRect, entryPrefab, and emptyLabel in the inspector.
// Call Refresh() from anywhere the queue changes (e.g. after purchasing a tech).
public class ActiveResearchPanel : MonoBehaviour
{
    public static ActiveResearchPanel Instance;
    [Header("Panel Reference")]
    [Tooltip("The panel GameObject that contains the ScrollRect.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Scroll Setup")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("Entry Prefab")]
    [Tooltip("Must have exactly two TMP labels as children: first = tech name, second = turns label.")]
    [SerializeField] private GameObject entryPrefab;

    [Header("Empty State")]
    [Tooltip("Optional: shown when no research is in progress.")]
    [SerializeField] private GameObject emptyLabel;

    [Header("Turn Label Format")]
    [Tooltip("{0} = count, {1} = plural suffix. Example: \"{0} turn{1} left\"")]
    [SerializeField] private string turnsFormat = "{0} turn{1} left";

    [Header("Panel Visibility")]
    [Tooltip("Auto-hide the panel when the queue is empty.")]
    [SerializeField] private bool hideWhenEmpty = false;

    private List<ResearchEntryRow> _pool          = new List<ResearchEntryRow>();
    private int                    _activeRowCount = 0;
    private const int              MaxPoolSize     = 15;

    // -------------------------------------------------------------------------
    // Start — guaranteed all managers exist here
    // -------------------------------------------------------------------------
    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        // Subscribe here, not in OnEnable, because TurnManager.Instance
        // is null at scene-start OnEnable when this lives on a persistent GO.
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted += OnTurnStarted;
        else
            Debug.LogWarning("[ActiveResearchPanel] TurnManager not ready at Start — OnTurnStarted not subscribed.");

        Refresh();
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    private void OnTurnStarted(PlayerData _) => Refresh();

    // -------------------------------------------------------------------------
    //  PUBLIC API
    // -------------------------------------------------------------------------

    public void Show()   { if (panelRoot != null) panelRoot.SetActive(true);  }
    public void Hide()   { if (panelRoot != null) panelRoot.SetActive(false); }
    public void Toggle() { if (panelRoot != null) panelRoot.SetActive(!panelRoot.activeSelf); }

    /// <summary>
    /// Rebuilds the list from TechManager's current queue for the human player.
    /// Call this any time the research queue changes — e.g. right after purchasing a tech.
    /// </summary>
    public void Refresh()
    {
        if (scrollRect == null || entryPrefab == null) return;

        PlayerData humanPlayer = GetHumanPlayer();

        if (humanPlayer == null)
        {
            Debug.LogWarning("[ActiveResearchPanel] Refresh: human player is null.");
            return;
        }

        if (TechManager.Instance == null)
        {
            Debug.LogWarning("[ActiveResearchPanel] Refresh: TechManager not ready.");
            return;
        }

        Dictionary<TechNode, int> queue = TechManager.Instance.GetActiveResearchFor(humanPlayer);

        SetAllRowsHidden();
        _activeRowCount = 0;

        foreach (var kvp in queue)
        {
            TechNode node      = kvp.Key;
            int      turnsLeft = kvp.Value;

            ResearchEntryRow row = GetOrCreateRow(_activeRowCount);
            row.gameObject.SetActive(true);
            row.Set(node.techName, FormatTurns(turnsLeft));
            _activeRowCount++;
        }

        bool isEmpty = _activeRowCount == 0;

        if (emptyLabel != null)
            emptyLabel.SetActive(isEmpty);

        // NOTE: Do NOT call panelRoot.SetActive here.
        // ActionLogBox owns the visibility of this panel entirely.
        // Calling SetActive here fights with ActionLogBox and breaks it.

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        // Notify the box so it can resize/reorder
        ActionLogBox.Instance?.NotifyResearchChanged(!isEmpty);
    }

    // -------------------------------------------------------------------------
    //  INTERNAL HELPERS — identical to original
    // -------------------------------------------------------------------------

    private void SetAllRowsHidden()
    {
        foreach (var row in _pool)
            if (row != null) row.gameObject.SetActive(false);
    }

    private ResearchEntryRow GetOrCreateRow(int index)
    {
        // Cap pool at MaxPoolSize — never create more than 15 rows
        int clampedIndex = Mathf.Min(index, MaxPoolSize - 1);

        while (_pool.Count <= clampedIndex)
        {
            GameObject       obj = Instantiate(entryPrefab, scrollRect.content);
            ResearchEntryRow row = obj.GetComponent<ResearchEntryRow>();

            if (row == null)
            {
                row = obj.AddComponent<ResearchEntryRow>();
                var labels = obj.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (labels.Length >= 1) row.nameLabel  = labels[0];
                if (labels.Length >= 2) row.turnsLabel = labels[1];
            }

            _pool.Add(row);
        }

        return _pool[clampedIndex];
    }

    private string FormatTurns(int turns)
    {
        return string.Format(turnsFormat, turns, turns == 1 ? "" : "s");
    }

    private PlayerData GetHumanPlayer()
    {
        return GameManager.Instance != null && GameManager.Instance.players.Count > 0
            ? GameManager.Instance.players[0]
            : null;
    }
}