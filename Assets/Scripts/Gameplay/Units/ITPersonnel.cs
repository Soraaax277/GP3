using UnityEngine;

// IT Personnel Unit - Unlocked by "Repair Specialization" tech.
// Elite repair specialist with enhanced efficiency and capabilities.
public class ITPersonnel : Unit
{
    public int repairCharges = 3;
    public int maxRepairCharges = 3;

    public override int CurrentCharges { get => repairCharges; set => repairCharges = value; }
    public override int MaxCharges => maxRepairCharges;

    public float repairEfficiency = 1.5f; // Starts at +50% efficiency
    public bool canRepairWires = true; // Can repair both towers and wires
    public bool canRepairTowers = true;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        goldUpkeep = 18; 
    }

    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        // 1. ERA SPECIFIC UPGRADES (Futuristic)
        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            repairEfficiency = 2.5f; // Cryo-repair technology
            maxRepairCharges = 6;
            repairCharges = Mathf.Max(repairCharges, 6);
            moveRange = 5;
        }
        else
        {
            moveRange = 3;
        }
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "RepairCharges" || statName == "Actions")
        {
            repairCharges += (int)amount;
            Debug.Log($"ITPersonnel received +{(int)amount} Repair Charges");
        }
        else if (statName == "RepairEfficiency")
        {
            repairEfficiency += amount;
            Debug.Log($"ITPersonnel: Repair efficiency increased by {amount * 100}% (now {repairEfficiency * 100}%)");
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
            Debug.Log("[ITPersonnel] Cannot act (turn/action used)");
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
            Debug.Log("[ITPersonnel] No damaged structure adjacent!");
            return;
        }

        int repairCost = GetRepairCost();
        if (owner.resources < repairCost)
        {
            Debug.Log($"[ITPersonnel] Not enough gold to repair! Need {repairCost}, have {owner.resources}");
            return;
        }

        owner.resources -= repairCost;

        if (targetTower != null)
        {
            targetTower.Repair(repairEfficiency);
            Debug.Log($"[ITPersonnel] Tower repair complete with {repairEfficiency * 100}% efficiency (cost: {repairCost}).");
        }
        else if (targetWire != null)
        {
            float healAmount = targetWire.MaxDurability * repairEfficiency;
            targetWire.currentDurability = Mathf.Min(targetWire.currentDurability + healAmount, targetWire.MaxDurability);
            Debug.Log($"[ITPersonnel] Wire repair complete, restored {healAmount} HP (cost: {repairCost}).");
        }

        if (ShouldConsumeCharge())
            repairCharges--;
            
        ConsumeAction();
        Debug.Log($"[ITPersonnel] Charges left: {repairCharges}");

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
        int baseCost = 40;
        if (TechManager.Instance != null)
        {
            float multiplier = TechManager.Instance.GetInfraMultiplier(owner, "RepairCost");
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