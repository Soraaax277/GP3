using UnityEngine;
using System.Linq;

public class Technician : Unit
{
    public int repairCharges = 2;
    public float repairEfficiency = 1.0f; // Multiplier for durability restored
    public bool canRepairWires = false; // Unlocked by Versatile Repairmen tech

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "RepairCharges" || statName == "Actions")
        {
            repairCharges += (int)amount;
            Debug.Log($"Technician received +{(int)amount} Repair Charges");
        }
        else if (statName == "RepairEfficiency")
        {
            // +50% Durability restored from Repairs, etc.
            repairEfficiency += amount;
            Debug.Log($"Technician: Repair efficiency increased by {amount * 100}% (now {repairEfficiency * 100}%)");
        }
        else if (statName == "CanRepairWires")
        {
            // Versatile Repairman: can now do repairs to wiring
            Debug.Log($"Technician: Can now repair wires (feature upgrade)");
        }
    }
    
    public override void UnlockSkill(string skillName)
    {
        base.UnlockSkill(skillName);
        
        if (skillName == "CanRepairWires" || skillName == "VersatileRepairmen")
        {
            canRepairWires = true;
            Debug.Log("Technician learned to repair wires!");
        }
    }

    public void RepairAdjacentStructure()
    {
        if (!canAct && !testingMode)
        {
            Debug.Log("[Technician] Cannot act (turn/action used)");
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

        // If no tower, try to find a wire (if skill is unlocked)
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
            Debug.Log("[Technician] No damaged structure adjacent!");
            return;
        }

        // Deduct repair cost with tech modifier
        int repairCost = GetRepairCost();
        if (owner.resources < repairCost)
        {
            Debug.Log($"[Technician] Not enough gold to repair! Need {repairCost}, have {owner.resources}");
            return;
        }

        owner.resources -= repairCost;

        // Check for full restore chance (Untested Stimulants tech)
        bool fullRestore = false;
        if (TechManager.Instance != null && TechManager.Instance.IsFeatureUnlocked("UntestedStimulants"))
        {
            if (Random.value <= 0.10f) // 10% chance
            {
                fullRestore = true;
                Debug.Log("[Technician] Untested Stimulants triggered - FULL RESTORE!");
            }
        }

        // Check for first-time repair bonus (Advanced Repair Tools tech)
        float finalEfficiency = repairEfficiency;
        bool isFirstTimeRepair = false;
        
        if (targetTower != null)
        {
            // Check if this tower has been repaired before
            if (TechManager.Instance != null && TechManager.Instance.IsFeatureUnlocked("AdvancedRepairTools"))
            {
                if (!targetTower.HasBeenRepairedBefore())
                {
                    finalEfficiency += 0.25f; // +25% bonus for first repair
                    isFirstTimeRepair = true;
                    targetTower.MarkAsRepaired(); // Track that it's been repaired
                    Debug.Log("[Technician] First-time repair bonus applied (+25%)!");
                }
            }

            // Apply repair
            if (fullRestore)
            {
                // Full restore: repair with maximum efficiency to fully heal
                targetTower.Repair(100.0f); // Very high multiplier ensures full HP
            }
            else
            {
                targetTower.Repair(finalEfficiency);
            }
            Debug.Log($"[Technician] Tower repair complete with {finalEfficiency * 100}% efficiency (cost: {repairCost}, first-time: {isFirstTimeRepair}, full restore: {fullRestore}).");
        }
        else if (targetWire != null)
        {
            if (fullRestore)
            {
                targetWire.currentDurability = targetWire.MaxDurability;
            }
            else
            {
                float healAmount = targetWire.MaxDurability * finalEfficiency;
                targetWire.currentDurability = Mathf.Min(targetWire.currentDurability + healAmount, targetWire.MaxDurability);
            }
            Debug.Log($"[Technician] Wire repair complete, restored {finalEfficiency * 100}% HP (cost: {repairCost}, full restore: {fullRestore}).");
        }

        repairCharges--;
        ConsumeAction();
        Debug.Log($"[Technician] Charges left: {repairCharges}");

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

    private int GetRepairCost()
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
        Destroy(gameObject);
    }
}