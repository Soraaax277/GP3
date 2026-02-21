using UnityEngine;

public class WireSpecialist : Unit
{
    public int wiresRemaining = 8;
    public bool canRepairTowers = false; // Unlocked by Versatile Repairmen tech
    public float repairEfficiency = 1.0f;

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "WireCharges" || statName == "Actions")
        {
            wiresRemaining += (int)amount;
            Debug.Log($"WireSpecialist received +{(int)amount} Wire Charges");
        }
        else if (statName == "WireLength")
        {
            // Wire Length is handled globally by TechManager via GetInfraFlatBonus("WireLength")
            // This is already implemented in WireNode.GetMaxWireLength() and WirePlacementManager.MaxWireLength
            Debug.Log($"WireSpecialist: Wire Length upgraded by +{(int)amount}");
        }
        else if (statName == "WireDurability")
        {
            // Wire Durability is handled globally by TechManager via GetInfraMultiplier("WireDurability")
            // This is already implemented in WireNode.MaxDurability
            Debug.Log($"WireSpecialist: Wire Durability upgraded by {amount * 100}%");
        }
        else if (statName == "RepairEfficiency")
        {
            repairEfficiency += amount;
            Debug.Log($"WireSpecialist: Repair efficiency increased by {amount * 100}% (now {repairEfficiency * 100}%)");
        }
    }

    public override void UnlockSkill(string skillName)
    {
        base.UnlockSkill(skillName);
        
        if (skillName == "CanRepairTowers" || skillName == "VersatileRepairmen")
        {
            canRepairTowers = true;
            Debug.Log("WireSpecialist learned to repair towers!");
        }
    }

    public void BuildWire(HexTile tile, float yRotation = 0f)
    {
        if (!canAct && !testingMode) return;

        if (tile == null || tile.IsOccupied() || tile.HasWire()) return;

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
        
        wireNode.Initialize(tile, owner);
        
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RegisterWire(wireNode);
        }

        wiresRemaining--;
        ConsumeAction();

        if (wiresRemaining <= 0)
        {
            Die();
        }
    }

    public void RepairAdjacentTower()
    {
        if (!canRepairTowers && !testingMode)
        {
            Debug.Log("[WireSpecialist] Tower repair capability not unlocked yet - need 'Versatile Repairmen and Electricians' tech!");
            return;
        }

        if (!canAct && !testingMode)
        {
            Debug.Log("[WireSpecialist] Cannot act (turn/action used)");
            return;
        }

        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedTower != null && neighbor.placedTower.IsDestroyed())
            {
                targetTower = neighbor.placedTower;
                break;
            }
        }

        if (targetTower == null)
        {
            Debug.Log("[WireSpecialist] No destroyed tower adjacent!");
            return;
        }

        // Deduct repair cost
        int repairCost = GetRepairCost();
        if (owner.resources < repairCost)
        {
            Debug.Log($"[WireSpecialist] Not enough gold to repair! Need {repairCost}, have {owner.resources}");
            return;
        }

        owner.resources -= repairCost;
        targetTower.Repair(repairEfficiency);

        wiresRemaining = Mathf.Max(0, wiresRemaining - 1); // Uses wire charges
        ConsumeAction();
        Debug.Log($"[WireSpecialist] Tower repair complete with {repairEfficiency * 100}% efficiency (cost: {repairCost}). Wires left: {wiresRemaining}");

        if (wiresRemaining <= 0)
        {
            Die();
            return;
        }

        if (!owner.isAI)
        {
            SetSelected(false);
            if (PlayerInput.Instance != null) PlayerInput.Instance.ClearHighlights();
        }
    }

    private int GetRepairCost()
    {
        int baseCost = 45; // Base repair cost for electricians
        if (TechManager.Instance != null)
        {
            float multiplier = TechManager.Instance.GetInfraMultiplier("RepairCost");
            return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
        }
        return baseCost;
    }

    void Die()
    {
        if (currentTile != null) currentTile.placedUnit = null;
        Destroy(gameObject);
    }
}