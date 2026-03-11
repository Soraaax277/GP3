using UnityEngine;
using System.Collections.Generic;

public class PowerGridManager : MonoBehaviour
{
    public static PowerGridManager Instance;

    private List<SignalNode> sourceNodes = new List<SignalNode>();
    
    // TACTICAL VIEW (Phase 2)
    // HexTile (Child) -> HexTile (Parent source)
    public Dictionary<HexTile, HexTile> powerFlowMap = new Dictionary<HexTile, HexTile>();
    // HexTile -> Number of nodes powered downstream through this tile
    public Dictionary<HexTile, int> powerLoad = new Dictionary<HexTile, int>();

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
            if (tile.placedStructure is IPowerable pStruct) pStruct.IsPowered = false;
        }

        powerFlowMap.Clear();
        powerLoad.Clear();

        Queue<HexTile> frontier = new Queue<HexTile>();
        HashSet<HexTile> visited = new HashSet<HexTile>();

        foreach (var source in sourceNodes)
        {
            if (source != null && source.tile != null)
            {
                frontier.Enqueue(source.tile);
                visited.Add(source.tile);
                powerLoad[source.tile] = 0; // Initialize root load
            }
        }

        List<HexTile> traversalOrder = new List<HexTile>();

        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();
            traversalOrder.Add(current);
            UpdatePowerOnTile(current, true);
            poweredCount++;

            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(current))
            {
                if (visited.Contains(neighbor)) continue;

                // Check if this neighbor has anything that CAN receive power
                if (neighbor.placedTower != null || neighbor.placedWire != null || neighbor.placedSignalNode != null || neighbor.placedStructure != null)
                {
                    // If it's conductive, it goes in the queue to pass power forward
                    if (HasConductiveInfrastructure(neighbor))
                    {
                        visited.Add(neighbor);
                        frontier.Enqueue(neighbor);
                        powerFlowMap[neighbor] = current; // DACTICAL FLOW
                        powerLoad[neighbor] = 0; 
                    }
                    else
                    {
                        // It's not conductive but it IS adjacent to power
                        visited.Add(neighbor);
                        UpdatePowerOnTile(neighbor, true);
                        poweredCount++;
                        powerFlowMap[neighbor] = current; // Labeled leaf
                    }
                }
            }
        }

        // TWEAK: BOTTLE-NECK ANALYSIS (Propagate load upwards)
        // Reverse traversal ensures we count all children before adding to parents
        for (int i = traversalOrder.Count - 1; i >= 0; i--)
        {
            HexTile current = traversalOrder[i];
            if (powerFlowMap.ContainsKey(current))
            {
                HexTile parent = powerFlowMap[current];
                if (!powerLoad.ContainsKey(parent)) powerLoad[parent] = 0;
                
                // Add this tile and all its downstream descendants to its parent's load
                powerLoad[parent] += (1 + (powerLoad.ContainsKey(current) ? powerLoad[current] : 0));
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

        // TACTICAL VIEW REFRESH (Phase 2)
        if (PowerGridOverlay.Instance != null && PowerGridOverlay.Instance.isEnabled)
        {
            PowerGridOverlay.Instance.Refresh();
        }
    }

    public bool HasTesseract(PlayerData player)
    {
        if (TurnManager.Instance == null) return false;
        foreach (var structure in TurnManager.Instance.GetAllStructures())
        {
            if (structure is Tesseract && structure.owner == player) return true;
        }
        return false;
    }

    private bool HasConductiveInfrastructure(HexTile tile)
    {
        if (tile.placedSignalNode != null) return true; // HQs are always conductive
        
        // Wires MUST be activated by a Technician to conduct, UNLESS a Tesseract is active
        if (tile.placedWire != null)
        {
            if (tile.placedWire.IsTechnicianActivated) return true;
            if (HasTesseract(tile.placedWire.owner)) return true;
        }

        // Towers conduct IF they have been finished by a Builder (even if not currently powered)
        if (tile.placedTower != null && tile.placedTower.IsBuilt()) return true;
        
        return false;
    }

    private void UpdatePowerOnTile(HexTile tile, bool powered)
    {
        if (tile.placedTower is IPowerable pTower) pTower.UpdatePowerState(powered);
        if (tile.placedStructure is IPowerable pStruct) pStruct.UpdatePowerState(powered);
        if (tile.placedWire is IPowerable pWire) 
        {
            // If Tesseract is active, force IsTechnicianActivated to true visually as well
            if (powered && HasTesseract(tile.placedWire.owner)) pWire.IsTechnicianActivated = true;
            pWire.UpdatePowerState(powered);
        }
    }
}
