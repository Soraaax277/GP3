using UnityEngine;

/// <summary>
/// Highlights the HexTile that owns the selected building using the FresnelGlow
/// material (via HighlightUtil). Only called for the current human player's
/// buildings — see BuildingSelectionManager.Select().
/// </summary>
public class SelectionRing : MonoBehaviour
{
    public static SelectionRing Instance { get; private set; }

    private HexTile _highlightedTile;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void Show(GameObject buildingObj, Color color)
    {
        Hide(); // remove any previous highlight first

        HexTile tile = ResolveHexTile(buildingObj);
        if (tile == null) return;

        _highlightedTile = tile;
        HighlightUtil.ApplyIdle(tile.gameObject, color);
    }

    public void Hide()
    {
        if (_highlightedTile == null) return;
        HighlightUtil.Remove(_highlightedTile.gameObject);
        _highlightedTile = null;
    }

    /// <summary>
    /// Resolves the HexTile that owns a building GameObject by checking each
    /// known node type's typed tile reference, avoiding any GetComponent cast overhead.
    /// </summary>
    private static HexTile ResolveHexTile(GameObject obj)
    {
        if (obj == null) return null;

        TowerNode tower = obj.GetComponentInParent<TowerNode>();
        if (tower != null && tower.tile != null) return tower.tile;

        SignalNode signal = obj.GetComponentInParent<SignalNode>();
        if (signal != null && signal.tile != null) return signal.tile;

        StructureNode structure = obj.GetComponentInParent<StructureNode>();
        if (structure != null && structure.ParentTile != null) return structure.ParentTile;

        return null;
    }
}