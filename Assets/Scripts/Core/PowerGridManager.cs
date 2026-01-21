using UnityEngine;
using System.Collections.Generic;

public class PowerGridManager : MonoBehaviour
{
    public static PowerGridManager Instance;

    private List<SignalNode> sourceNodes = new List<SignalNode>();

    private void Awake()
    {
        Instance = this;
    }

    public void RegisterSource(SignalNode node)
    {
        if (!sourceNodes.Contains(node))
            sourceNodes.Add(node);
    }

    public void RefreshGrid()
    {
        // 1. Reset all powerable nodes
        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedTower is IPowerable pTower) pTower.IsPowered = false;
            // WireNode will also implement IPowerable when created
        }

        // 2. BFS from all SignalNodes
        Queue<HexTile> frontier = new Queue<HexTile>();
        HashSet<HexTile> visited = new HashSet<HexTile>();

        foreach (var source in sourceNodes)
        {
            frontier.Enqueue(source.tile);
            visited.Add(source.tile);
        }

        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();

            // Notify powerable components on this tile
            UpdatePowerOnTile(current, true);

            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(current))
            {
                if (visited.Contains(neighbor)) continue;

                // Only spread power through infrastructure
                if (HasConductiveInfrastructure(neighbor))
                {
                    visited.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }
        }

        // 3. Final pass to update visuals for anything that remained unpowered
        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (!visited.Contains(tile))
            {
                UpdatePowerOnTile(tile, false);
            }
        }
    }

    private bool HasConductiveInfrastructure(HexTile tile)
    {
        // SignalNodes, Towers, and Wires spread power
        return tile.placedNode != null || tile.placedTower != null || tile.placedWire != null;
    }

    private void UpdatePowerOnTile(HexTile tile, bool powered)
    {
        if (tile.placedTower is IPowerable pTower) pTower.UpdatePowerState(powered);
        if (tile.placedWire is IPowerable pWire) pWire.UpdatePowerState(powered);
    }
}
