using UnityEngine;

public class Technician : Unit
{
    public int actionCharges = 10;
    public int maxCharges = 10;

    public override int CurrentCharges { get => actionCharges; set => actionCharges = value; }
    public override int MaxCharges => maxCharges;

    public float repairEfficiency = 1.0f;
    public bool canRepairWires = false;
    public bool isResearching = false;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
    }

    public override void OnTurnStart(PlayerData activePlayer)
    {
        base.OnTurnStart(activePlayer);
        if (owner == activePlayer && isResearching)
        {
             // Researching units cannot act or move
             canAct = false;
             movementRemaining = 0;
             Debug.Log($"[Research] {name} is busy with a project and skips its turn.");
        }
    }

    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            repairEfficiency = 2.0f;
            maxCharges = 15;
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
        if (isResearching)
        {
            Debug.Log("[Technician] Unit is busy with research project!");
            return;
        }
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
        
        if (owner != null && !owner.isAI && AudioManager.Instance != null && AudioManager.Instance.powerSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.powerSFX);

        ActionLogUI.PostFiltered(owner, "Technician powered up the grid!", ActionLogUI.Colors.Unit);

        if (FeedbackController.Instance != null)
            FeedbackController.Instance.PlayTechnicianAction(targetWire.transform.position);

        if (ShouldConsumeCharge())
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
        if (isResearching)
        {
            Debug.Log("[Technician] Unit is busy with research project!");
            return;
        }
        if (!canAct && !testingMode) return;

        TowerNode targetTower = null;
        StructureNode targetStructure = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedTower != null && neighbor.placedTower.IsDestroyed())
            {
                targetTower = neighbor.placedTower;
                break;
            }
            if (neighbor.placedStructure != null && neighbor.placedStructure.IsBroken)
            {
                targetStructure = neighbor.placedStructure;
                break;
            }
        }

        WireNode targetWire = null;
        if (targetTower == null && targetStructure == null && canRepairWires)
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

        if (targetTower == null && targetWire == null && targetStructure == null) return;

        int repairCost = GetRepairCost();
        if (owner.resources < repairCost) return;

        owner.resources -= repairCost;
        if (targetTower != null)
        {
            if (owner != null && !owner.isAI && AudioManager.Instance != null && AudioManager.Instance.repairSFX != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.repairSFX);

            targetTower.Repair(repairEfficiency);
            ActionLogUI.PostFiltered(owner, "Technician repaired Tower", ActionLogUI.Colors.Unit);
        }
        else if (targetStructure != null)
        {
            targetStructure.Repair(20f * repairEfficiency);
            ActionLogUI.PostFiltered(owner, "Technician repaired Structure", ActionLogUI.Colors.Unit);
        }
        else if (targetWire != null)
        {
            float healAmount = targetWire.MaxDurability * repairEfficiency;
            targetWire.currentDurability = Mathf.Min(targetWire.currentDurability + healAmount, targetWire.MaxDurability);
            ActionLogUI.PostFiltered(owner, "Technician repaired Wire", ActionLogUI.Colors.Unit);
        }

        if (ShouldConsumeCharge())
            actionCharges--;
            
        ConsumeAction();
        if (actionCharges <= 0) Die();
    }

    public int GetRepairCost()
    {
        int baseCost = 50;
        if (TechManager.Instance != null)
        {
            float multiplier = TechManager.Instance.GetInfraMultiplier(owner, "RepairCost");
            return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
        }
        return baseCost;
    }
    
    public void StartResearchProject(string techID)
    {
        if (isResearching) return;

        if (owner != null && !owner.isAI && AudioManager.Instance != null && AudioManager.Instance.researchSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.researchSFX);

        if (ResearchProjectHandler.Instance != null)
        {
            ResearchProjectHandler.Instance.StartProject(this, techID);
        }
    }
    
    public bool IsAtBase()
    {
        if (currentTile == null) return false;
        // Search for Hub/Base buildings. Many structures exist, let's look for SignalNode counterparts
        if (currentTile.placedNode != null) return true; // SignalNodes are 'Bases'
        return false;
    }
}
