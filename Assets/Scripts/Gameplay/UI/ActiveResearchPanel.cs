using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Displays the human player's in-progress research queue inside a ScrollRect.
// Attach this to the panel GameObject that contains the ScrollRect.
//
// HIERARCHY SETUP:
//
//   [ActiveResearchPanel]          ← this script lives here
//     └── ScrollRect               ← assign to scrollRect field
//           └── Viewport           ← Image + Mask component
//                 └── Content      ← Vertical Layout Group + Content Size Fitter
//
// Each entry is spawned from entryPrefab — a simple prefab with:
//   - TextMeshProUGUI  (tech name)
//   - TextMeshProUGUI  (turns remaining)
//
// Call Refresh() any time the queue changes, or let the script
// auto-refresh at the start of each turn via TurnManager.OnTurnStarted.
public class ActiveResearchPanel : MonoBehaviour
{
    [Header("Scroll Setup")]
    [Tooltip("The ScrollRect component. Its Content child should have a " +
             "Vertical Layout Group + Content Size Fitter (Vertical Fit = Preferred Size).")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("Entry Prefab")]
    [Tooltip("Prefab spawned for each in-progress tech. " +
             "Must have exactly two TMP labels as children: first = tech name, second = turns label.")]
    [SerializeField] private GameObject entryPrefab;

    [Header("Empty State")]
    [Tooltip("Optional: shown when no research is in progress. Hidden otherwise.")]
    [SerializeField] private GameObject emptyLabel;

    [Header("Turn Label Format")]
    [Tooltip("Format string for the turns remaining label. {0} is replaced with the turn count.\n" +
             "Example: \"{0} turns left\"  or  \"ETA: {0}\"")]
    [SerializeField] private string turnsFormat = "{0} turn{1} left";

    // Pooled rows — we reuse them instead of destroying/creating each refresh.
    private List<ResearchEntryRow> _pool = new List<ResearchEntryRow>();
    private int _activeRowCount = 0;

    // -------------------------------------------------------------------------
    private void OnEnable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted += OnTurnStarted;

        Refresh();
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    private void OnTurnStarted(PlayerData _) => Refresh();

    // -------------------------------------------------------------------------
    //  PUBLIC API
    // -------------------------------------------------------------------------

    /// Rebuilds the list from TechManager's current queue for the human player.
    public void Refresh()
    {
        if (scrollRect == null || entryPrefab == null) return;

        PlayerData humanPlayer = GetHumanPlayer();
        Dictionary<TechNode, int> queue = TechManager.Instance != null && humanPlayer != null
            ? TechManager.Instance.GetActiveResearchFor(humanPlayer)
            : new Dictionary<TechNode, int>();

        // Hide all rows first, then re-activate only what we need.
        SetAllRowsHidden();
        _activeRowCount = 0;

        foreach (var kvp in queue)
        {
            TechNode node    = kvp.Key;
            int turnsLeft    = kvp.Value;

            ResearchEntryRow row = GetOrCreateRow(_activeRowCount);
            row.gameObject.SetActive(true);
            row.Set(node.techName, FormatTurns(turnsLeft));
            _activeRowCount++;
        }

        // Show/hide the empty state label.
        if (emptyLabel != null)
            emptyLabel.SetActive(_activeRowCount == 0);

        // Force the Content rect to resize immediately so the scroll is correct.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
    }

    // -------------------------------------------------------------------------
    //  INTERNAL HELPERS
    // -------------------------------------------------------------------------

    private void SetAllRowsHidden()
    {
        foreach (var row in _pool)
            if (row != null) row.gameObject.SetActive(false);
    }

    private ResearchEntryRow GetOrCreateRow(int index)
    {
        // Grow the pool if needed.
        while (_pool.Count <= index)
        {
            GameObject obj = Instantiate(entryPrefab, scrollRect.content);
            ResearchEntryRow row = obj.GetComponent<ResearchEntryRow>();

            if (row == null)
            {
                // Auto-add the component if the prefab doesn't already have it.
                row = obj.AddComponent<ResearchEntryRow>();
                // Try to wire the two TMP labels automatically by child order.
                var labels = obj.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (labels.Length >= 1) row.nameLabel   = labels[0];
                if (labels.Length >= 2) row.turnsLabel  = labels[1];
            }

            _pool.Add(row);
        }

        return _pool[index];
    }

    private string FormatTurns(int turns)
    {
        string plural = turns == 1 ? "" : "s";
        return string.Format(turnsFormat, turns, plural);
    }

    private PlayerData GetHumanPlayer()
    {
        return (GameManager.Instance != null && GameManager.Instance.players.Count > 0)
            ? GameManager.Instance.players[0]
            : null;
    }
}
