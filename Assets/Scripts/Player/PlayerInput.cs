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
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (!Physics.Raycast(ray, out _))
            {
                DeselectUnit();
            }
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (selectedUnit == null || !selectedUnit.CanAct)
                return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            TowerNode tower = hit.collider.GetComponent<TowerNode>();
            if (tower != null && selectedUnit is BuilderUnit builder)
            {
                builder.BuildTower(tower);
                return;
            }

            HexTile tile = hit.collider.GetComponent<HexTile>();
            if (tile != null)
            {
                int range = (selectedUnit is BuilderUnit b) ? b.moveRange : 1;
                selectedUnit.MoveTo(tile, range);
            }
        }
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

        int range = (selectedUnit is BuilderUnit b) ? b.moveRange : 1;

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

                if (next.IsOccupied() || next.HasTower())
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
