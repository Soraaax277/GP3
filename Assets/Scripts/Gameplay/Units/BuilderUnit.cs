using UnityEngine;

public class BuilderUnit : Unit
{
    public int buildRange      = 1;
    public int buildsRemaining = 3;

    public bool canConstructTower = false;
    public bool canRepairInfrastructure = false; // Unlocked by Versatile Builder Tool Kit
    public bool canSabotage = false;
    public bool canUseBombs = false;
    public float repairEfficiency = 1.0f;
    public float baseDamage = 10;
    public float damageMultiplier = 1.0f;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        SetMoveRange(2);
        CheckTechStatus();
    }

    private void CheckTechStatus()
    {
        if (canConstructTower) return;

        if (TechManager.Instance != null)
        {
            if (TechManager.Instance.IsFeatureUnlocked("MinimumWageContract") || 
                TechManager.Instance.IsFeatureUnlocked("Construction"))
            {
                UnlockConstruction();
            }

            if (TechManager.Instance.IsFeatureUnlocked("CanSabotage") ||
                TechManager.Instance.IsFeatureUnlocked("BrainwashedWorkforce"))
            {
                UnlockSabotage();
            }

            if (TechManager.Instance.IsFeatureUnlocked("NeutronBombs"))
            {
                UnlockBombs();
            }
        }
    }

    public void UnlockConstruction()
    {
        canConstructTower = true;
        Debug.Log("Builder learned Construction via Minimum Wage Contract!");
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "BuildCharges" || statName == "Actions")
        {
            buildsRemaining += (int)amount;
            Debug.Log($"Builder received +{(int)amount} Build Charges");
        }
        else if (statName == "BuildingCostReduction")
        {
            // Building cost reduction is handled globally via infrastructure upgrades
            // TechEffect: infraStatName="BuildingCost", isMultiplier=true, value=-0.2 (for -20%)
            Debug.Log($"Builder: Building costs reduced by {amount * 100}%");
        }
        else if (statName == "RepairEfficiency")
        {
            repairEfficiency += amount;
            Debug.Log($"Builder: Repair efficiency increased by {amount * 100}% (now {repairEfficiency * 100}%)");
        }
        else if (statName == "DamagePercent")
        {
            damageMultiplier += amount;
        }
    }

    public override void UnlockSkill(string skillName)
    {
        base.UnlockSkill(skillName);
        
        if (skillName == "Construction" || skillName == "MinimumWageContract")
        {
            UnlockConstruction();
        }
        else if (skillName == "CanRepair" || skillName == "VersatileBuilderToolKit")
        {
            canRepairInfrastructure = true;
            Debug.Log("Builder learned to repair infrastructure!");
        }
        else if (skillName == "CanSabotage" || skillName == "BrainwashedWorkforce")
        {
            UnlockSabotage();
        }
    }

    public void UnlockSabotage()
    {
        canSabotage = true;
        Debug.Log("Builder learned to sabotage");
    }

    public void UnlockBombs()
    {
        canUseBombs = true;
        Debug.Log("Builder now uses Neutron Bombs (20% to fully destroy on sabotage)!");
    }
    
    
    public void ConstructAdjacentTower()
    {
        CheckTechStatus();

        if (!canConstructTower && !testingMode)
        {
            Debug.Log("[Builder] Construction tech (Minimum Wage Contract) not yet researched!");
            return;
        }

        if (!canAct && !testingMode)
        {
            Debug.Log("[Builder] Cannot act (turn/action used)");
            return;
        }

        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            // TowerState.Unbuilt was renamed to TowerState.Hologram (System 3 three-phase build)
            if (neighbor.placedTower != null && neighbor.placedTower.state == TowerNode.TowerState.Hologram)
            {
                targetTower = neighbor.placedTower;
                break;
            }
        }

        if (targetTower == null)
        {
            Debug.Log("[Builder] No hologram tower adjacent to construct!");
            return;
        }

        // Deduct building cost with tech modifier
        int buildCost = GetBuildingCost();
        if (owner.resources < buildCost)
        {
            Debug.Log($"[Builder] Not enough gold to build! Need {buildCost}, have {owner.resources}");
            return;
        }

        owner.resources -= buildCost;
        targetTower.Build();

        buildsRemaining = Mathf.Max(0, buildsRemaining - 1);
        ConsumeAction();
        Debug.Log($"[Builder] Construction complete (cost: {buildCost}). Builds left: {buildsRemaining}");

        if (buildsRemaining <= 0)
        {
            Die();
            return;
        }

        if (!owner.isAI)
        {
            SetSelected(false);
            if (PlayerInput.Instance != null)     PlayerInput.Instance.ClearHighlights();
            if (BuildUIManager.Instance != null)  BuildUIManager.Instance.CloseBuildMenu();
        }
    }

    public int GetBuildingCost()
    {
        int baseCost = 100; // Base building cost
        if (TechManager.Instance != null)
        {
            float multiplier = TechManager.Instance.GetInfraMultiplier("BuildingCost");
            return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
        }
        return baseCost;
    }

    public void RepairAdjacentStructure()
    {
        if (!canRepairInfrastructure && !testingMode)
        {
            Debug.Log("[Builder] Repair capability not unlocked yet - need 'Versatile Builder Tool Kit' tech!");
            return;
        }

        if (!canAct && !testingMode)
        {
            Debug.Log("[Builder] Cannot act (turn/action used)");
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
            Debug.Log("[Builder] No destroyed tower adjacent!");
            return;
        }

        // Deduct repair cost
        int repairCost = GetRepairCost();
        if (owner.resources < repairCost)
        {
            Debug.Log($"[Builder] Not enough gold to repair! Need {repairCost}, have {owner.resources}");
            return;
        }

        owner.resources -= repairCost;
        targetTower.Repair(repairEfficiency);

        buildsRemaining = Mathf.Max(0, buildsRemaining - 1); // Uses build charges
        ConsumeAction();
        Debug.Log($"[Builder] Repair complete with {repairEfficiency * 100}% efficiency (cost: {repairCost}). Builds left: {buildsRemaining}");

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
    
    public void DamageAdjacentStructure()
    {
        if (!canSabotage && !testingMode)
        {
            Debug.Log("Sabotage Ability not unlocked");
        }
        if (!canAct && !testingMode)
        {
            Debug.Log("[Saboteur] Cannot act (turn/action used)");
            return;
        }

        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            //checks if target is owned by enemyAI
            if (neighbor.placedTower != null && neighbor.placedTower.owner.isAI)
            {
                targetTower = neighbor.placedTower;
                break;
            }
        }

        if (targetTower == null)
        {
            Debug.Log("No structure adjacent!");
            return;
        }

        float sabotageDamage = 0;
        if (canUseBombs) //if Neutron Bombs tech is active, roll destroy chance
        {
            int procInt = Random.Range(0, 5);
            if (procInt >= 4) //procInt has a 20% chance of being 4
            { 
                sabotageDamage = targetTower.currentDurability;
            }  
        }
        else
        {
            sabotageDamage = baseDamage *  damageMultiplier;
        }
        
        targetTower.TakeDamage(sabotageDamage);

        buildsRemaining = Mathf.Max(0, buildsRemaining - 1); // Uses build charges
        ConsumeAction();
        Debug.Log($"[Builder] Sabotage complete, dealing {sabotageDamage}. Actions left: {buildsRemaining}");

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

    public int GetRepairCost()
    {
        int baseCost = 60; // Base repair cost
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

        // Unregister BEFORE destroying so TurnManager never holds a dead reference.
        // This prevents MissingReferenceException in GetPlayerFocusPoint.
        if (TurnManager.Instance != null)
            TurnManager.Instance.UnregisterUnit(this);

        Destroy(gameObject);
    }
}