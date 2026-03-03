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

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Unit unit = hit.collider.GetComponentInParent<Unit>();
                if (unit != null)
                {
                    // BLOCK: Only select units if it's the player's turn 
                    if (TurnManager.Instance != null && TurnManager.Instance.currentPlayer != null && !TurnManager.Instance.currentPlayer.isAI)
                    {
                        SelectUnit(unit);
                        UnitActionPanel.Instance.Open(unit);
                        BuildUIManager.Instance.CloseBuildMenu();
                        return;
                    }
                    else
                    {
                        Debug.Log("[PlayerInput] Cannot select unit: It is currently the AI's turn!");
                        return;
                    }
                }

                SignalNode business = hit.collider.GetComponentInParent<SignalNode>();
                if (business != null)
                {
                    DeselectUnit();
                    BuildUIManager.Instance.OpenBuildMenu(business);
                    UnitActionPanel.Instance.Close();
                    return;
                }

                DeselectAndClose();
            }
            else
            {
                DeselectAndClose();
            }
        }
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (selectedUnit == null || !selectedUnit.CanAct)
                return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            HexTile tile = hit.collider.GetComponent<HexTile>();
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
            if (BuildUIManager.Instance != null) BuildUIManager.Instance.CloseBuildMenu();
            if (UnitActionPanel.Instance != null) UnitActionPanel.Instance.Close();
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

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            ClearHighlights();
            hoveredTile = null;
            return;
        }

        HexTile tile = hit.collider.GetComponent<HexTile>();
        if (tile == null || tile == hoveredTile)
            return;

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
                tile.HighlightWalkable(); 
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
