using UnityEngine;

/// RoboWorker Unit - Unlocked by "Fully Mechanical Workforce" tech.
/// Robotic construction unit with no maintenance cost and high mobility.
/// Can be produced in Worker Factories for faster deployment.
public class RoboWorker : Unit
{
    public int buildRange = 2; // Better range than regular builders
    public int buildsRemaining = 6; // More charges
    public bool canConstructTower = true;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        SetMoveRange(4); // Highly mobile
        goldUpkeep = 0; // ZERO upkeep - key advantage of robots
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

    public void ConstructAdjacentTower()
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
            Debug.Log("[RoboWorker] No hologram tower adjacent to construct!");
            return;
        }

        // Deduct building cost with tech modifier
        int buildCost = GetBuildingCost();
        if (owner.resources < buildCost)
        {
            Debug.Log($"[RoboWorker] Not enough gold to build! Need {buildCost}, have {owner.resources}");
            return;
        }

        owner.resources -= buildCost;
        targetTower.Build();

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
            if (BuildUIManager.Instance != null) BuildUIManager.Instance.CloseBuildMenu();
        }
    }

    private int GetBuildingCost()
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