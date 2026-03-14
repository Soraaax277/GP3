using UnityEngine;
using TMPro;

// Represents a single row in the ActiveResearchPanel scroll list.
// Attach to the entry prefab, or it will be added automatically by ActiveResearchPanel.
//
// PREFAB SETUP (two children with TMP labels, in this order):
//
//   [EntryPrefab]                  ← ResearchEntryRow lives here
//     ├── NameLabel                ← TextMeshProUGUI — tech name
//     └── TurnsLabel               ← TextMeshProUGUI — turns remaining

public class ResearchEntryRow : MonoBehaviour
{
    [Tooltip("Displays the tech name.")]
    public TextMeshProUGUI nameLabel;

    [Tooltip("Displays the turns remaining.")]
    public TextMeshProUGUI turnsLabel;

    public void Set(string techName, string turnsText)
    {
        if (nameLabel  != null) nameLabel.text  = techName;
        if (turnsLabel != null) turnsLabel.text = turnsText;
    }
}
