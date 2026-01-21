using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Instance;

    private Unit selectedUnit;
    private List<HexTile> highlightedTiles = new List<HexTile>();
    private HexTile hoveredTile;

    private void Awake() => Instance = this;

    private void Update()
    {
        HandleHover();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Unit unit = hit.collider.GetComponentInParent<Unit>();
                if (unit != null)
                {
                    SelectUnit(unit);
                    UnitActionPanel.Instance.Open(unit);
                    BuildUIManager.Instance.CloseBuildMenu();
                    return;
                }

                SignalNode business = hit.collider.GetComponentInParent<SignalNode>();
                if (business != null)
                {
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
                int range = GetUnitMoveRange(selectedUnit);
                selectedUnit.MoveTo(tile, range);
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
        if (unit is BuilderUnit b) return b.moveRange;
        if (unit is WireSpecialist w) return w.moveRange;
        return 1;
    }

    void HandleHover()
    {
        if (selectedUnit == null)
            return;

        if (!selectedUnit.CanAct || selectedUnit.IsFresh)
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

        int range = GetUnitMoveRange(selectedUnit);

        List<HexTile> path = FindPath(
            selectedUnit.currentTile,
            target,
            range
        );

        if (path == null || path.Count == 0)
        {
            target.HighlightBlocked();
            highlightedTiles.Add(target);
            return;
        }

        for (int i = 0; i < path.Count; i++)
        {
            HexTile tile = path[i];

            if (i <= range)
                tile.HighlightWalkable(); 
            else
                tile.HighlightBlocked(); 

            highlightedTiles.Add(tile);
        }
    }

    List<HexTile> FindPath(HexTile start, HexTile goal, int maxRange)
    {
        Queue<HexTile> frontier = new Queue<HexTile>();
        Dictionary<HexTile, HexTile> cameFrom = new Dictionary<HexTile, HexTile>();

        frontier.Enqueue(start);
        cameFrom[start] = null;

        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();

            if (current == goal)
                break;

            foreach (HexTile next in GridManager.Instance.GetNeighbors(current))
            {
                if (cameFrom.ContainsKey(next))
                    continue;

                if (!next.IsWalkable())
                    continue;

                frontier.Enqueue(next);
                cameFrom[next] = current;
            }
        }

        if (!cameFrom.ContainsKey(goal))
            return null;

        List<HexTile> path = new List<HexTile>();
        HexTile step = goal;

        while (step != null)
        {
            path.Add(step);
            step = cameFrom[step];
        }

        path.Reverse();
        return path;
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
