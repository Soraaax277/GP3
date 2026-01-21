using UnityEngine;

public class WireSpecialist : Unit
{
    public int moveRange = 3;
    public int wiresRemaining = 8;

    public void BuildWire(HexTile tile, float yRotation = 0f)
    {
        if (!canAct && !testingMode) return;

        if (tile == null || tile.IsOccupied()) return;

        int dist = GridManager.Instance.CubeDistance(currentTile.cubeCoords, tile.cubeCoords);
        if (dist > 1)
        {
            Debug.Log("[WireSpecialist] Too far to build wire");
            return;
        }

        bool carriesPower = false;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
        {
            if (neighbor.placedNode != null || neighbor.placedTower != null || neighbor.placedWire != null)
            {
                carriesPower = true;
                break;
            }
        }

        if (!carriesPower)
        {
            Debug.Log("[WireSpecialist] Wire must be adjacent to existing infrastructure");
            return;
        }

        GameObject wireObj;
        if (WirePlacementManager.Instance != null && WirePlacementManager.Instance.wirePrefab != null)
        {
            wireObj = Instantiate(WirePlacementManager.Instance.wirePrefab);
        }
        else
        {
            wireObj = new GameObject("Wire_" + tile.name);
        }

        wireObj.transform.position = tile.transform.position + Vector3.up * 0.84f;
        wireObj.transform.rotation = Quaternion.Euler(0, yRotation, 90);
        
        WireNode wireNode = wireObj.GetComponent<WireNode>();
        if (wireNode == null) wireNode = wireObj.AddComponent<WireNode>();
        
        wireNode.Initialize(tile);

        wiresRemaining--;
        ConsumeAction();

        if (wiresRemaining <= 0)
        {
            Debug.Log("[WireSpecialist] Charges depleted. Despawning...");
            Die();
        }
    }

    void Die()
    {
        if (currentTile != null) currentTile.placedUnit = null;
        Destroy(gameObject);
    }
}
