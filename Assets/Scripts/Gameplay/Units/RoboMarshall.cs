using UnityEngine;

/// RoboMarshall Unit - Unlocked by "Fully Mechanical Workforce" tech.
/// Advanced robotic repair unit with no upkeep and high efficiency.
/// Can repair both towers and wires with superior performance.
public class RoboMarshall : Unit
{
    public int repairCharges = 5;
    public int maxRepairCharges = 5;

    public override int CurrentCharges { get => repairCharges; set => repairCharges = value; }
    public override int MaxCharges => maxRepairCharges;

    public float repairEfficiency = 1.5f; // Superior efficiency
    public bool canRepairWires = true;
    public bool canRepairTowers = true;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        goldUpkeep = 0; 
        base.Initialize(spawnTile, player);
    }

    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        // 1. ERA SPECIFIC UPGRADES (Futuristic)
        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            repairEfficiency = 3.0f; // AI-Optimized Nanobots
            maxRepairCharges = 10;
            repairCharges = Mathf.Max(repairCharges, 10);
            moveRange = 6;
        }
        else
        {
            moveRange = 4;
        }
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "RepairCharges" || statName == "Actions")
        {
            repairCharges += (int)amount;
            Debug.Log($"RoboMarshall received +{(int)amount} Repair Charges");
        }
        else if (statName == "RepairEfficiency")
        {
            repairEfficiency += amount;
            Debug.Log($"RoboMarshall: Repair efficiency increased by {amount * 100}% (now {repairEfficiency * 100}%)");
        }
    }

    public override void UnlockSkill(string skillName)
    {
        base.UnlockSkill(skillName);
    }

    public void RepairAdjacentStructure()
    {
        if (!canAct && !testingMode)
        {
            Debug.Log("[RoboMarshall] Cannot act (turn/action used)");
            return;
        }

        // Try to find a tower first
        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedTower != null && neighbor.placedTower.IsDestroyed())
            {
                targetTower = neighbor.placedTower;
                break;
            }
        }

        // If no tower, try to find a wire
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

        if (targetTower == null && targetWire == null)
        {
            Debug.Log("[RoboMarshall] No damaged structure adjacent!");
            return;
        }

        int repairCost = GetRepairCost();
        if (owner.resources < repairCost)
        {
            Debug.Log($"[RoboMarshall] Not enough gold to repair! Need {repairCost}, have {owner.resources}");
            return;
        }

        owner.resources -= repairCost;

        // Check for full restore chance (UntestedStimulants tech)
        bool fullRestore = false;
        if (TechManager.Instance != null && TechManager.Instance.IsFeatureUnlocked("UntestedStimulants"))
        {
            if (Random.value <= 0.10f)
            {
                fullRestore = true;
                Debug.Log("[RoboMarshall] Untested Stimulants triggered - FULL RESTORE!");
            }
        }

        if (targetTower != null)
        {
            targetTower.Repair(fullRestore ? 100.0f : repairEfficiency);
            ActionLogUI.PostFiltered(owner, "Robo-Marshall repaired a Tower.", ActionLogUI.Colors.Unit);
            Debug.Log($"[RoboMarshall] Tower repair complete (efficiency: {repairEfficiency * 100}%, cost: {repairCost}, full restore: {fullRestore}).");
        }
        else if (targetWire != null)
        {
            if (fullRestore)
                targetWire.currentDurability = targetWire.MaxDurability;
            else
            {
                float healAmount = targetWire.MaxDurability * repairEfficiency;
                targetWire.currentDurability = Mathf.Min(targetWire.currentDurability + healAmount, targetWire.MaxDurability);
            }
            ActionLogUI.PostFiltered(owner, "Robo-Marshall repaired a Wire.", ActionLogUI.Colors.Unit);
            Debug.Log($"[RoboMarshall] Wire repair complete (cost: {repairCost}, full restore: {fullRestore}).");
        }

        if (ShouldConsumeCharge())
            repairCharges--;
            
        ConsumeAction();
        Debug.Log($"[RoboMarshall] Charges left: {repairCharges}");

        if (repairCharges <= 0)
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

    // Changed from private to public so UnitActionPanel can read the cost for display
    public int GetRepairCost()
    {
        int baseCost = 30; // Cheaper than human units
        if (TechManager.Instance != null)
        {
            float multiplier = TechManager.Instance.GetInfraMultiplier(owner, "RepairCost");
            return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
        }
        return baseCost;
    }

    public override void Die()
    {
        if (currentTile != null) currentTile.placedUnit = null;
        
        if (TurnManager.Instance != null)
            TurnManager.Instance.UnregisterUnit(this);
        
        Destroy(gameObject);
    }
}