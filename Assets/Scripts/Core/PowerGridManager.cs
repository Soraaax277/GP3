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
        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedTower is IPowerable pTower) pTower.IsPowered = false;
        }

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

            UpdatePowerOnTile(current, true);

            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(current))
            {
                if (visited.Contains(neighbor)) continue;

                if (HasConductiveInfrastructure(neighbor))
                {
                    visited.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }
        }

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
        return tile.placedNode != null || tile.placedTower != null || tile.placedWire != null;
    }

    private void UpdatePowerOnTile(HexTile tile, bool powered)
    {
        if (tile.placedTower is IPowerable pTower) pTower.UpdatePowerState(powered);
        if (tile.placedWire is IPowerable pWire) pWire.UpdatePowerState(powered);
    }
}
