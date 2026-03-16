using UnityEngine;

/// RoboWorker Unit - Unlocked by "Fully Mechanical Workforce" tech.
/// Robotic construction unit with no maintenance cost and high mobility.
/// Can be produced in Worker Factories for faster deployment.
public class RoboWorker : Unit
{
    public int buildRange = 2; // Better range than regular builders
    public int buildsRemaining = 6; // More charges
    public int maxBuilds = 6;

    public override int CurrentCharges { get => buildsRemaining; set => buildsRemaining = value; }
    public override int MaxCharges => maxBuilds;

    public bool canConstructTower = true;

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
            maxBuilds = 12;
            buildsRemaining = 12; // Mass production builds
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

        if (statName == "BuildCharges" || statName == "Actions")
        {
            buildsRemaining += (int)amount;
            Debug.Log($"RoboWorker received +{(int)amount} Build Charges");
        }
    }

    public override void UnlockSkill(string skillName)
    {
        base.UnlockSkill(skillName);
    }

    public void ConstructAdjacentInfrastructure()
    {
        if (!canConstructTower && !testingMode)
        {
            Debug.Log("[RoboWorker] Construction not available!");
            return;
        }

        if (!canAct && !testingMode)
        {
            Debug.Log("[RoboWorker] Cannot act (turn/action used)");
            return;
        }

        TowerNode targetTower = null;
        StructureNode targetStructure = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedTower != null && neighbor.placedTower.state == TowerNode.TowerState.Hologram)
            {
                targetTower = neighbor.placedTower;
                break;
            }
            if (neighbor.placedStructure != null && !neighbor.placedStructure.IsBuilt)
            {
                targetStructure = neighbor.placedStructure;
                break;
            }
        }

        if (targetTower == null && targetStructure == null)
        {
            Debug.Log("[RoboWorker] No hologram infrastructure adjacent! Neighbors checked: " + GridManager.Instance.GetNeighbors(currentTile).Count);
            return;
        }


        int buildCost = GetBuildingCost();
        if (owner.resources < buildCost)
        {
            Debug.Log($"[RoboWorker] Not enough gold to build! Need {buildCost}, have {owner.resources}");
            return;
        }

        owner.resources -= buildCost;
        if (targetTower != null) targetTower.Build();
        else if (targetStructure != null) targetStructure.Build();


        if (ShouldConsumeCharge())
            buildsRemaining = Mathf.Max(0, buildsRemaining - 1);
            
        ConsumeAction();
        Debug.Log($"[RoboWorker] Construction complete (cost: {buildCost}). Builds left: {buildsRemaining}");

        if (buildsRemaining <= 0)
        {
            Die();
            return;
        }

        if (!owner.isAI)
        {
            SetSelected(false);
            if (PlayerInput.Instance != null) PlayerInput.Instance.ClearHighlights();
            if (BuildingUIManager.Instance != null) BuildingUIManager.Instance.Close();
        }
    }

    // Changed from private to public so UnitActionPanel can read the cost for display
    public int GetBuildingCost()
    {
        int baseCost = 100;
        if (TechManager.Instance != null)
        {
            float multiplier = TechManager.Instance.GetInfraMultiplier(owner, "BuildingCost");
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