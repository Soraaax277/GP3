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
        if (GridManager.Instance == null) return;

        int sourceCount = sourceNodes.Count;
        int poweredCount = 0;

        // Reset all infrastructure power states first
        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (tile.placedTower is IPowerable pTower) pTower.IsPowered = false;
            if (tile.placedWire is IPowerable pWire) pWire.IsPowered = false;
        }

        Queue<HexTile> frontier = new Queue<HexTile>();
        HashSet<HexTile> visited = new HashSet<HexTile>();

        foreach (var source in sourceNodes)
        {
            if (source != null && source.tile != null)
            {
                frontier.Enqueue(source.tile);
                visited.Add(source.tile);
            }
        }

        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();
            UpdatePowerOnTile(current, true);
            poweredCount++;

            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(current))
            {
                if (visited.Contains(neighbor)) continue;

                // Check if this neighbor has anything that CAN receive power
                if (neighbor.placedTower != null || neighbor.placedWire != null || neighbor.placedSignalNode != null)
                {
                    // If it's conductive, it goes in the queue to pass power forward
                    if (HasConductiveInfrastructure(neighbor))
                    {
                        visited.Add(neighbor);
                        frontier.Enqueue(neighbor);
                    }
                    else
                    {
                        // It's not conductive (e.g. unbuilt tower or unactivated wire), 
                        // but it IS adjacent to power, so it should still be "powered".
                        visited.Add(neighbor);
                        UpdatePowerOnTile(neighbor, true);
                        poweredCount++;
                    }
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

        if (poweredCount <= sourceCount && sourceCount > 0)
        {
            Debug.LogWarning("[PowerGrid] Warning: Power is NOT leaving the HQ. Check if any adjacent wires/towers have been activated by a Technician.");
        }

        Debug.Log($"[PowerGrid] Refresh Complete. Sources: {sourceCount} | Total Powered Tiles: {poweredCount}");

        // SIGNAL & INFLUENCE SYNC
        // Now that the power grid is refreshed, we must recalculate signal and influence totals
        if (TurnManager.Instance != null && TurnManager.Instance.players != null && InfluenceManager.Instance != null)
        {
            foreach (PlayerData p in TurnManager.Instance.players)
            {
                foreach (SignalNode node in p.ownedNodes)
                {
                    if (node != null) node.PropagateSignal();
                }
            }
            InfluenceManager.Instance.RecalculateGlobalInfluence(TurnManager.Instance.players);
            TurnManager.Instance.NotifyStatusChanged();
        }
    }

    private bool HasConductiveInfrastructure(HexTile tile)
    {
        if (tile.placedSignalNode != null) return true; // HQs are always conductive
        
        // Wires MUST be activated by a Technician to conduct
        if (tile.placedWire != null && tile.placedWire.IsTechnicianActivated) return true;

        // Towers conduct IF they have been finished by a Builder (even if not currently powered)
        if (tile.placedTower != null && tile.placedTower.IsBuilt()) return true;
        
        return false;
    }

    private void UpdatePowerOnTile(HexTile tile, bool powered)
    {
        if (tile.placedTower is IPowerable pTower) pTower.UpdatePowerState(powered);
        if (tile.placedWire is IPowerable pWire) pWire.UpdatePowerState(powered);
    }
}
