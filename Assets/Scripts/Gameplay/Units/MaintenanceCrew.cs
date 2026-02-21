using UnityEngine;

// Maintenance Crew Unit - Unlocked by "Company Service Centers" tech.
// A specialized worker that combines building and repair capabilities.
// Can repair towers after "Versatile Repairmen and Electricians" tech.
public class MaintenanceCrew : Unit
{
    public int maintenanceCharges = 4; // Higher than basic units
    public float repairEfficiency = 1.0f;
    public bool canRepairTowers = false;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        SetMoveRange(2);
        goldUpkeep = 15; // Mid-range upkeep
        
        CheckTechStatus();
    }

    private void CheckTechStatus()
    {
        if (TechManager.Instance != null)
        {
            // Check if Versatile Repairmen tech is researched
            if (TechManager.Instance.IsFeatureUnlocked("VersatileRepairmen"))
            {
                canRepairTowers = true;
            }
        }
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "MaintenanceCharges" || statName == "Actions")
        {
            maintenanceCharges += (int)amount;
            Debug.Log($"MaintenanceCrew received +{(int)amount} Maintenance Charges");
        }
        else if (statName == "RepairEfficiency")
        {
            repairEfficiency += amount;
            Debug.Log($"MaintenanceCrew: Repair efficiency increased by {amount * 100}% (now {repairEfficiency * 100}%)");
        }
    }

    public override void UnlockSkill(string skillName)
    {
        base.UnlockSkill(skillName);
        
        if (skillName == "CanRepairTowers" || skillName == "VersatileRepairmen")
        {
            canRepairTowers = true;
            Debug.Log("MaintenanceCrew learned to repair towers!");
        }
    }

    public void PerformMaintenance()
    {
        if (!canAct && !testingMode)
        {
            Debug.Log("[MaintenanceCrew] Cannot act (turn/action used)");
            return;
        }

        if (!canRepairTowers)
        {
            Debug.Log("[MaintenanceCrew] Cannot repair towers yet - need 'Versatile Repairmen and Electricians' tech!");
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
            Debug.Log("[MaintenanceCrew] No destroyed tower adjacent!");
            return;
        }

        // Deduct repair cost with tech modifier
        int repairCost = GetRepairCost(targetTower);
        if (owner.resources < repairCost)
        {
            Debug.Log($"[MaintenanceCrew] Not enough gold to repair! Need {repairCost}, have {owner.resources}");
            return;
        }

        owner.resources -= repairCost;
        targetTower.Repair(repairEfficiency);

        maintenanceCharges--;
        ConsumeAction();
        Debug.Log($"[MaintenanceCrew] Maintenance complete with {repairEfficiency * 100}% efficiency (cost: {repairCost}). Charges left: {maintenanceCharges}");

        if (maintenanceCharges <= 0)
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

    private int GetRepairCost(TowerNode tower)
    {
        int baseCost = 50; // Base repair cost
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
        
        if (TurnManager.Instance != null)
            TurnManager.Instance.UnregisterUnit(this);
        
        Destroy(gameObject);
    }
}