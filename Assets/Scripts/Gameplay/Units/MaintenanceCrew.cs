using UnityEngine;

// Maintenance Crew Unit - Unlocked by "Company Service Centers" tech.
// A specialized worker that combines building and repair capabilities.
// Can repair towers after "Versatile Repairmen and Electricians" tech.
public class MaintenanceCrew : Unit
{
    public int maintenanceCharges = 4; // Higher than basic units
    public int maxMaintenanceCharges = 4;

    public override int CurrentCharges { get => maintenanceCharges; set => maintenanceCharges = value; }
    public override int MaxCharges => maxMaintenanceCharges;

    public float repairEfficiency = 1.0f;
    public bool canRepairTowers = false;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        SetMoveRange(2);
        goldUpkeep = 15;
        
        CheckTechStatus();
    }

    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        // 1. ERA SPECIFIC UPGRADES (Futuristic)
        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            repairEfficiency = 2.0f; // Rapid nanite maintenance
            maxMaintenanceCharges = 8;
            maintenanceCharges = Mathf.Max(maintenanceCharges, 8); // Ultra-long-life fuel cells
        }

        // 2. TECH TREE FEATURES
        if (TechManager.Instance.IsFeatureUnlocked("VersatileRepairmen"))
        {
            canRepairTowers = true;
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

        int repairCost = GetRepairCost(targetTower);
        if (owner.resources < repairCost)
        {
            Debug.Log($"[MaintenanceCrew] Not enough gold to repair! Need {repairCost}, have {owner.resources}");
            return;
        }

        owner.resources -= repairCost;
        targetTower.Repair(repairEfficiency);

        if (ShouldConsumeCharge())
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

    // Parameterless overload — used by UnitActionPanel to display cost before a target is known.
    // Returns the base repair cost with tech modifier applied, same as the tower-specific version.
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

    // Tower-specific overload — kept for use inside PerformMaintenance() in case
    // per-tower cost logic is added in the future.
    private int GetRepairCost(TowerNode tower)
    {
        return GetRepairCost(); // Delegates to the parameterless version
    }

    void Die()
    {
        if (currentTile != null) currentTile.placedUnit = null;
        
        if (TurnManager.Instance != null)
            TurnManager.Instance.UnregisterUnit(this);
        
        Destroy(gameObject);
    }
}