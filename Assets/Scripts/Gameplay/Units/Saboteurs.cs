using UnityEngine;

public class Saboteurs: Unit
{
    public int sabotageCharges;
    
    public bool canUseBombs;
    public float baseDamage = 10;
    public float damageMultiplier = 1.0f;
    
    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        SetMoveRange(2);
        CheckTechStatus();
    }
    
    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        // 1. ERA SPECIFIC UPGRADES (Futuristic)
        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            sabotageCharges = Mathf.Max(sabotageCharges, 5); 
            damageMultiplier = 2.0f;
        }

        // 2. TECH TREE FEATURES
        if (TechManager.Instance.IsFeatureUnlocked("NeutronBombs"))
        {
            UnlockBombs();
        }
    }
    
    public void UnlockBombs()
    {
        canUseBombs = true;
        Debug.Log("Saboteur now uses Neutron Bombs (20% to fully destroy on sabotage)!");
    }
    
    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "SabotageCharges" || statName == "Actions")
        {
            sabotageCharges += (int)amount;
            Debug.Log($"Saboteur received +{(int)amount} Sabotage Charges");
        }

        if (statName == "DamagePercent" || statName == "Damage")
        {
            damageMultiplier += amount;
            Debug.Log($"Saboteur received +{amount}% Damage");
        }
    }
    
    public void DamageAdjacentStructure()
    {
        if (!canAct && !testingMode)
        {
            Debug.Log("[Saboteur] Cannot act (turn/action used)");
            return;
        }

        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            //checks if target is owned by enemyAI
            if (neighbor.placedTower != null && neighbor.placedTower.owner != TurnManager.Instance.currentPlayer)
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

        sabotageCharges = Mathf.Max(0, sabotageCharges - 1); // Uses build charges
        ConsumeAction();
        Debug.Log($"[Builder] Sabotage complete, dealing {sabotageDamage}. Sabotages left: {sabotageCharges}");

        if (sabotageCharges <= 0)
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