using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Instance;

    public Unit selectedUnit;
    private List<HexTile> highlightedTiles = new List<HexTile>();
    private HexTile hoveredTile;

    private void Awake() => Instance = this;

    private void Update()
    {
        if (PauseMenuUI.GameIsPaused) return;
        HandleHover();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if ((TowerPlacementManager.Instance != null && TowerPlacementManager.Instance.IsPlacing) ||
                (WirePlacementManager.Instance != null && WirePlacementManager.Instance.IsPlacing))
                return;

            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                DeselectUnit();
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            if (hits.Length > 0)
            {
                // 1. Prioritize Units first if they are anywhere in the click ray
                foreach (var hit in hits)
                {
                    Unit unit = hit.collider.GetComponentInParent<Unit>();
                    if (unit != null)
                    {
                        if (TurnManager.Instance != null && TurnManager.Instance.currentPlayer != null && !TurnManager.Instance.currentPlayer.isAI)
                        {
                            if (UnitActionPanel.Instance != null) UnitActionPanel.Instance.Open(unit);
                            BuildingUIManager.Instance.Close();
                            if (DetailPanel.Instance != null) DetailPanel.Instance.ShowUnit(unit);
                            if (selectedUnit != unit) SelectUnit(unit);
                            return;
                        }
                        return;
                    }
                }

                // 2. Then try to find specific buildings being clicked directly
                foreach (var hit in hits)
                {
                    SignalNode business = hit.collider.GetComponentInParent<SignalNode>();
                    if (business != null) { DeselectUnit(); BuildingUIManager.Instance.Open(business); UnitActionPanel.Instance.Close(); return; }

                    StructureNode structure = hit.collider.GetComponentInParent<StructureNode>();
                    if (structure != null) { DeselectUnit(); BuildingUIManager.Instance.Open(structure); UnitActionPanel.Instance.Close(); return; }

                    TowerNode tower = hit.collider.GetComponentInParent<TowerNode>();
                    if (tower != null) { DeselectUnit(); BuildingUIManager.Instance.Open(tower); UnitActionPanel.Instance.Close(); return; }
                }

                // 3. Finally, fallback to interacting with the HexTile base
                foreach (var hit in hits)
                {
                    HexTile tile = hit.collider.GetComponent<HexTile>();
                    if (tile != null)
                    {
                        if (tile.placedStructure != null) { DeselectUnit(); BuildingUIManager.Instance.Open(tile.placedStructure); UnitActionPanel.Instance.Close(); return; }
                        if (tile.placedTower != null) { DeselectUnit(); BuildingUIManager.Instance.Open(tile.placedTower); UnitActionPanel.Instance.Close(); return; }
                        if (tile.placedSignalNode != null) { DeselectUnit(); BuildingUIManager.Instance.Open(tile.placedSignalNode); UnitActionPanel.Instance.Close(); return; }
                        
                        // Hit an empty tile
                        DeselectAndClose();
                        return;
                    }
                }
            }
            else
            {
                // Clicking into nothingness (void)
                DeselectAndClose();
            }
        }
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (selectedUnit == null || !selectedUnit.CanAct)
                return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            HexTile tile = null;
            foreach (var hit in hits)
            {
                tile = hit.collider.GetComponent<HexTile>();
                if (tile != null) break;
            }

            if (tile != null)
            {
                if (selectedUnit.CanMoveTo(tile, selectedUnit.movementRemaining))
                {
                    selectedUnit.MoveTo(tile, selectedUnit.movementRemaining);
                }
                else
                {
                    Debug.Log($"[PlayerInput] Cannot move {selectedUnit.name} to {tile.name} (blocked or too far)");
                }
            }
        }
    }

    private void DeselectAndClose()
    {
        if (TowerPlacementManager.Instance != null && TowerPlacementManager.Instance.IsPlacing)
        {
            TowerPlacementManager.Instance.SendMessage("CancelPlacement", SendMessageOptions.DontRequireReceiver);
        }
        else if (WirePlacementManager.Instance != null && WirePlacementManager.Instance.IsPlacing)
        {
            WirePlacementManager.Instance.SendMessage("CancelPlacement", SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            DeselectUnit();
            if (BuildingUIManager.Instance != null) BuildingUIManager.Instance.Close();
            if (UnitActionPanel.Instance != null) UnitActionPanel.Instance.Close();
            if (DetailPanel.Instance != null) DetailPanel.Instance.Close();
        }
    }

    int GetUnitMoveRange(Unit unit)
    {
        return unit.movementRemaining;
    }

    void HandleHover()
    {
        if (selectedUnit == null)
            return;

        if (!selectedUnit.CanAct || selectedUnit.movementRemaining <= 0)
        {
            ClearHighlights();
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        HexTile tile = null;
        foreach (var hit in hits)
        {
            tile = hit.collider.GetComponent<HexTile>();
            if (tile != null) break;
        }

        if (tile == null || tile == hoveredTile)
        {
            if (tile == null)
            {
                ClearHighlights();
                hoveredTile = null;
            }
            return;
        }

        hoveredTile = tile;
        PreviewPath(tile);
    }

    void PreviewPath(HexTile target)
    {
        ClearHighlights();

        int range = selectedUnit.movementRemaining;

        List<HexTile> path = GridManager.Instance.FindPath(selectedUnit.currentTile, target);

        if (path == null || path.Count <= 1)
        {
            if (target != selectedUnit.currentTile)
            {
                target.HighlightBlocked();
                highlightedTiles.Add(target);
            }
            return;
        }

        for (int i = 0; i < path.Count; i++)
        {
            HexTile tile = path[i];

            if (i == 0)
            {
                tile.HighlightWalkable();
            }
            else if (i <= range)
            {
                // UI Fix: Must not highlight a tile as valid target if we literally can't stop on it (e.g. occupied by unit)
                if (i == path.Count - 1 && tile.placedUnit != null && tile.placedUnit != selectedUnit)
                {
                    tile.HighlightBlocked();
                }
                else
                {
                    tile.HighlightWalkable(); 
                }
            }
            else
            {
                tile.HighlightBlocked(); 
            }

            highlightedTiles.Add(tile);
        }
    }


    public void SelectUnit(Unit unit)
    {
        if (selectedUnit != null)
            selectedUnit.SetSelected(false);

        selectedUnit = unit;
        selectedUnit.SetSelected(true);

        ClearHighlights();

        // FIX: Ensure the detail panel always reveals itself when selecting a unit
        if (DetailPanel.Instance != null && unit != null)
        {
            DetailPanel.Instance.ShowUnit(unit);
        }
    }

    public void DeselectUnit()
    {
        if (selectedUnit == null) return;

        selectedUnit.SetSelected(false);
        selectedUnit = null;
        ClearHighlights();
    }

    public void ClearHighlights()
    {
        foreach (HexTile tile in highlightedTiles)
            tile.ClearHighlight();

        highlightedTiles.Clear();
    }
}