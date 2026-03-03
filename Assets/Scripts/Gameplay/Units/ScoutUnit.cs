using UnityEngine;
using UnityEngine.Serialization;

public class ScoutUnit: Unit
{
    private bool permanentVision;
    private bool canTelescope = false;
    private bool isDrone;
    private int baseVisionDefault = 4;
    
    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        visionRange = baseVisionDefault;
        SetMoveRange(3); // Scouts start more mobile
        base.Initialize(spawnTile, player);
    }
    
    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        // 1. ERA SPECIFIC UPGRADES (Futuristic)
        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            visionRange = baseVisionDefault + 2; // Advanced Sensor Array
            moveRange = 4; // High-tech propulsion
        }
        else
        {
            visionRange = baseVisionDefault;
            moveRange = 3;
        }

        // 2. TECH TREE FEATURES
        if (TechManager.Instance.IsFeatureUnlocked("PermanentVision"))
        {
            permanentVision = true;
        }

        if (TechManager.Instance.IsFeatureUnlocked("Drone") && !isDrone)
        {
            BeDrone();
        }

        if (TechManager.Instance.IsFeatureUnlocked("Telescope"))
        {
            canTelescope = true;
        }

        CheckForTelescope();
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "Vision")
        {
            visionRange += (int)amount;
            Debug.Log($"Scout vision upgraded by {(int)amount}");
        }
    }

    public void BeDrone()
    {
        if (isDrone) return;
        isDrone = true;
        
        // Visual indicator of drone mode
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        foreach (var r in rends) r.material.color = new Color(0.3f, 0.8f, 1f, 1f); // Cyan tech look
        
        Debug.Log("Scout is now a Drone - vision across the map!");
    }

    public void CheckForTelescope()
    {
        if (!canTelescope || currentTile == null) return;

        bool nearTower = false;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedTower != null && neighbor.placedTower.owner == owner)
            {
                nearTower = true;
                break;
            }
        }

        if (nearTower)
        {
            visionRange += 2; // Telescope bonus
        }
    }

    public override void OnTurnStart(PlayerData activePlayer)
    {
        base.OnTurnStart(activePlayer);
        // If permanent vision is active, some games keep vision revealed. 
        // In our FOV system, UpdateFogOfWar handles the current visibility.
    }
}
