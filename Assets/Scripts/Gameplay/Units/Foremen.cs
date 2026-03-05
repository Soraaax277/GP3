using UnityEngine;

// Foremen Unit - Unlocked by "Increased Workforce Size" tech.
// Specialized management unit that can oversee construction projects.
// Higher efficiency but more expensive than basic builders.
public class Foremen : Unit
{
    public int buildRange = 1;
    public int buildsRemaining = 5; // More than regular builders
    public bool canConstructTower = true; // Starts with construction ability

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        goldUpkeep = 20; 
        base.Initialize(spawnTile, player);
    }

    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        // 1. ERA SPECIFIC UPGRADES (Futuristic)
        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            buildsRemaining = 10; // AI Management boost
            moveRange = 5; // Hyper-commuter pods
        }
        else
        {
            moveRange = 3;
        }

        // 2. TECH TREE FEATURES (Foremen already has tower construction)
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "BuildCharges" || statName == "Actions")
        {
            buildsRemaining += (int)amount;
            Debug.Log($"Foremen received +{(int)amount} Build Charges");
        }
    }

    public override void UnlockSkill(string skillName)
    {
        base.UnlockSkill(skillName);
    }

    public void ConstructAdjacentTower()
    {
        if (!canConstructTower && !testingMode)
        {
            Debug.Log("[Foremen] Construction not available!");
            return;
        }

        if (!canAct && !testingMode)
        {
            Debug.Log("[Foremen] Cannot act (turn/action used)");
            return;
        }

        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedTower != null && neighbor.placedTower.state == TowerNode.TowerState.Hologram)
            {
                targetTower = neighbor.placedTower;
                break;
            }
        }

        if (targetTower == null)
        {
            Debug.Log("[Foremen] No hologram tower adjacent to construct!");
            return;
        }

        int buildCost = GetBuildingCost();
        if (owner.resources < buildCost)
        {
            Debug.Log($"[Foremen] Not enough gold to build! Need {buildCost}, have {owner.resources}");
            return;
        }

        owner.resources -= buildCost;
        targetTower.Build();

        buildsRemaining = Mathf.Max(0, buildsRemaining - 1);
        ConsumeAction();
        Debug.Log($"[Foremen] Construction complete (cost: {buildCost}). Builds left: {buildsRemaining}");

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
            float multiplier = TechManager.Instance.GetInfraMultiplier("BuildingCost");
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