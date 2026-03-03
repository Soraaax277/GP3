using UnityEngine;

public class Technician : Unit
{
    public int actionCharges = 10;
    public float repairEfficiency = 1.0f;
    public bool canRepairWires = false;

    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            repairEfficiency = 2.0f;
            actionCharges = Mathf.Max(actionCharges, 15); // Bonus charges in futuristic era
        }

        if (TechManager.Instance.IsFeatureUnlocked("CanRepairWires") || 
            TechManager.Instance.IsFeatureUnlocked("VersatileRepairmen"))
        {
            canRepairWires = true;
        }
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "RepairCharges" || statName == "Actions")
        {
            actionCharges += (int)amount;
            Debug.Log($"Technician received +{(int)amount} Action Charges");
        }
    }

    public void PowerAdjacentStructure()
    {
        if (!canAct && !testingMode) return;

        WireNode targetWire = null;
        
        // 1. Check current tile first!
        if (currentTile != null && currentTile.placedWire != null && 
            currentTile.placedWire.owner == owner && !currentTile.placedWire.IsTechnicianActivated)
        {
            targetWire = currentTile.placedWire;
        }

        // 2. Check neighbors
        if (targetWire == null)
        {
            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
            {
                if (neighbor.placedWire != null && neighbor.placedWire.owner == owner && !neighbor.placedWire.IsTechnicianActivated)
                {
                    targetWire = neighbor.placedWire;
                    break;
                }
            }
        }

        if (targetWire == null)
        {
            Debug.Log("[Technician] No unactivated wire adjacent to power up!");
            return;
        }

        targetWire.IsTechnicianActivated = true;
        Debug.Log($"[Technician] Successfully activated Wire at {targetWire.ParentTile.cubeCoords}!");

        actionCharges--;
        ConsumeAction();

        if (PowerGridManager.Instance != null)
        {
            Debug.Log("[Technician] Refreshing Power Grid after wire activation...");
            PowerGridManager.Instance.RefreshGrid();
        }

        if (actionCharges <= 0) Die();
    }

    public void RepairAdjacentStructure()
    {
        if (!canAct && !testingMode) return;

        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedTower != null && neighbor.placedTower.IsDestroyed())
            {
                targetTower = neighbor.placedTower;
                break;
            }
        }

        WireNode targetWire = null;
        if (targetTower == null && canRepairWires)
        {
            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
            {
                if (neighbor.placedWire != null && neighbor.placedWire.currentDurability < neighbor.placedWire.MaxDurability)
                {
                    targetWire = neighbor.placedWire;
                    break;
                }
            }
        }

        if (targetTower == null && targetWire == null) return;

        int repairCost = GetRepairCost();
        if (owner.resources < repairCost) return;

        owner.resources -= repairCost;
        if (targetTower != null) targetTower.Repair(repairEfficiency);
        else if (targetWire != null)
        {
            float healAmount = targetWire.MaxDurability * repairEfficiency;
            targetWire.currentDurability = Mathf.Min(targetWire.currentDurability + healAmount, targetWire.MaxDurability);
        }

        actionCharges--;
        ConsumeAction();
        if (actionCharges <= 0) Die();
    }

    public int GetRepairCost()
    {
        int baseCost = 50;
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
        if (TurnManager.Instance != null) TurnManager.Instance.UnregisterUnit(this);
        Destroy(gameObject);
    }
}
