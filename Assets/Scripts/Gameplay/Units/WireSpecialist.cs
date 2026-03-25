using UnityEngine;

public class WireSpecialist : Unit
{
    public int wiresRemaining = 8;
    public int maxWires = 8;

    public override int CurrentCharges { get => wiresRemaining; set => wiresRemaining = value; }
    public override int MaxCharges => maxWires;

    public bool canRepairTowers = false;     // Unlocked by Versatile Repairmen tech
    public bool canSabotage = false;     // Unlocked by Brainwashed Workforce tech
    public bool canUseBombs = false;     // Unlocked by Neutron Bombs tech
    public float repairEfficiency = 1.0f;
    public float baseDamage = 10f;
    public float damageMultiplier = 1.0f;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
    }

    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        // 1. ERA SPECIFIC UPGRADES (Futuristic)
        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            damageMultiplier = 2.0f;
            maxWires = 12;
            wiresRemaining = Mathf.Max(wiresRemaining, 12);
        }

        // 2. TECH TREE FEATURES
        if (TechManager.Instance.IsFeatureUnlocked("CanRepairTowers") || 
            TechManager.Instance.IsFeatureUnlocked("VersatileRepairmen"))
        {
            canRepairTowers = true;
        }

        if (TechManager.Instance.IsFeatureUnlocked("CanSabotage") || 
            TechManager.Instance.IsFeatureUnlocked("BrainwashedWorkforce"))
        {
            canSabotage = true;
        }

        if (TechManager.Instance.IsFeatureUnlocked("Neutron Bombs"))
        {
            canUseBombs = true;
        }
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "WireCharges" || statName == "Actions")
        {
            wiresRemaining += (int)amount;
            Debug.Log($"WireSpecialist received +{(int)amount} Wire Charges");
        }
        else if (statName == "WireLength")
        {
            Debug.Log($"WireSpecialist: Wire Length upgraded by +{(int)amount}");
        }
        else if (statName == "WireDurability")
        {
            Debug.Log($"WireSpecialist: Wire Durability upgraded by {amount * 100}%");
        }
        else if (statName == "RepairEfficiency")
        {
            repairEfficiency += amount;
            Debug.Log($"WireSpecialist: Repair efficiency increased by {amount * 100}% (now {repairEfficiency * 100}%)");
        }
        else if (statName == "DamagePercent")
        {
            damageMultiplier += amount;
        }
    }

    public override void UnlockSkill(string skillName)
    {
        base.UnlockSkill(skillName);
        
        if (skillName == "CanRepairTowers" || skillName == "VersatileRepairmen")
        {
            canRepairTowers = true;
            Debug.Log("WireSpecialist learned to repair towers!");
        }

        if (skillName == "CanSabotage" || skillName == "BrainwashedWorkforce")
        {
            UnlockSabotage();
        }

        if (skillName == "Neutron Bombs")
        {
            UnlockBombs();
        }
    }
    
    public void UnlockBombs()
    {
        canUseBombs = true;
        Debug.Log("WireSpecialist now uses Neutron Bombs (20% to fully destroy on sabotage)!");
    }
    
    public void UnlockSabotage()
    {
        canSabotage = true;
        Debug.Log("WireSpecialist learned to sabotage");
    }

    public void BuildWire(HexTile tile, float yRotation = 0f)
    {
        if (!canAct && !testingMode) return;
        if (tile == null || tile.IsOccupied() || tile.HasWire()) return;

        if (tile.hasStructure)
            tile.ClearEnvironmentalStructures();

        int dist = GridManager.Instance.CubeDistance(currentTile.cubeCoords, tile.cubeCoords);
        if (dist > 1)
        {
            Debug.Log("[WireSpecialist] Too far to build wire");
            return;
        }

        bool carriesPower = false;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
        {
            if (neighbor.placedNode != null || neighbor.placedTower != null || neighbor.placedWire != null)
            {
                carriesPower = true;
                break;
            }
        }

        if (!carriesPower)
        {
            Debug.Log("[WireSpecialist] Wire must be adjacent to existing infrastructure");
            return;
        }

        GameObject wireObj;
        if (WirePlacementManager.Instance != null && WirePlacementManager.Instance.wirePrefab != null)
            wireObj = Instantiate(WirePlacementManager.Instance.wirePrefab);
        else
            wireObj = new GameObject("Wire_" + tile.name);

        wireObj.transform.position = tile.transform.position + Vector3.up * 0.84f;
        wireObj.transform.rotation = Quaternion.Euler(0, yRotation, 90);
        
        WireNode wireNode = wireObj.GetComponent<WireNode>();
        if (wireNode == null) wireNode = wireObj.AddComponent<WireNode>();
        
        wireNode.Initialize(tile, owner);
        
        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterWire(wireNode);

        if (AudioManager.Instance != null && AudioManager.Instance.layWireSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.layWireSFX);

        if (FeedbackController.Instance != null)
            FeedbackController.Instance.PlayWirePlacement(tile.transform.position);

        ActionLogUI.PostFiltered(owner, "Laid down a new wire segment.", owner.isAI ? ActionLogUI.Colors.Enemy : ActionLogUI.Colors.Player);

        if (ShouldConsumeCharge())
            wiresRemaining--;

        ConsumeAction();

        if (wiresRemaining <= 0)
            Die();
    }

    public void DamageAdjacentStructure()
    {
        if (!canSabotage && !testingMode)
        {
            Debug.Log("Sabotage Ability not unlocked");
            return;
        }

        if (!canAct && !testingMode)
        {
            Debug.Log("[WireSpecialist] Cannot act (turn/action used)");
            return;
        }

        WireNode targetWire = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedWire != null && neighbor.placedWire.owner != TurnManager.Instance.currentPlayer)
            {
                targetWire = neighbor.placedWire;
                break;
            }
        }

        if (targetWire == null)
        {
            Debug.Log("No enemy wire adjacent!");
            return;
        }

        float sabotageDamage;
        if (canUseBombs)
        {
            int procInt = Random.Range(0, 5);
            sabotageDamage = procInt >= 4 ? targetWire.currentDurability : baseDamage * damageMultiplier;
        }
        else
        {
            sabotageDamage = baseDamage * damageMultiplier;
        }

        if (AudioManager.Instance != null && AudioManager.Instance.sabotageSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.sabotageSFX);

        targetWire.TakeDamage(sabotageDamage);

        if (ShouldConsumeCharge())
            wiresRemaining = Mathf.Max(0, wiresRemaining - 1);
            
        ConsumeAction();
        Debug.Log($"[WireSpecialist] Sabotage complete, dealing {sabotageDamage}. Wires left: {wiresRemaining}");

        if (wiresRemaining <= 0)
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

    public void RepairAdjacentTower()
    {
        if (!canRepairTowers && !testingMode)
        {
            Debug.Log("[WireSpecialist] Tower repair capability not unlocked yet!");
            return;
        }

        if (!canAct && !testingMode)
        {
            Debug.Log("[WireSpecialist] Cannot act (turn/action used)");
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
            Debug.Log("[WireSpecialist] No destroyed tower adjacent!");
            return;
        }

        int repairCost = GetRepairCost();
        if (owner.resources < repairCost)
        {
            Debug.Log($"[WireSpecialist] Not enough gold to repair! Need {repairCost}, have {owner.resources}");
            return;
        }

        owner.resources -= repairCost;
        
        if (AudioManager.Instance != null && AudioManager.Instance.repairSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.repairSFX);

        targetTower.Repair(repairEfficiency);

        if (ShouldConsumeCharge())
            wiresRemaining = Mathf.Max(0, wiresRemaining - 1);
            
        ConsumeAction();
        Debug.Log($"[WireSpecialist] Tower repair complete (efficiency: {repairEfficiency * 100}%, cost: {repairCost}). Wires left: {wiresRemaining}");

        if (wiresRemaining <= 0)
            Die();
    }

    // Changed from private to public so UnitActionPanel can read the cost for display
    public int GetRepairCost()
    {
        int baseCost = 45;
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
        Destroy(gameObject);
    }
}