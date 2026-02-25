using UnityEngine;
using UnityEngine.Serialization;

public class ScoutUnit: Unit
{
    public int visionRange;
    public int baseVision;
    public bool permanentVision;
    public bool canTelescope = false;
    public float movementPenalty = 0.25f;
    public bool isDrone;
    
    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        SetMoveRange(2);
        CheckTechStatus();
    }
    
    private void CheckTechStatus()
    {
        if (TechManager.Instance != null)
        {
            if (TechManager.Instance.IsFeatureUnlocked("PermanentVision"))
            {
                UnlockPermaVision();
            }

            if (TechManager.Instance.IsFeatureUnlocked("Drone"))
            {
                BeDrone();
            }

            if (TechManager.Instance.IsFeatureUnlocked("Telescope"))
            {
                UnlockTelescope();
            }
        }
        
        
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "Vision" || statName == "Actions")
        {
            visionRange += (int)amount;
            baseVision = visionRange;
            Debug.Log($"Builder received +{(int)amount} Build Charges");
        }
        else if (statName == "Movement")
        {
            moveRange =  (int)amount;
        }
        else if (statName == "MovementPenalty")
        {
            movementPenalty += (int)amount;
        }
    }

    public void UnlockPermaVision()
    {
        permanentVision = true;
        Debug.Log("Scout unlocked Permanent Vision");
    }

    public void BeDrone()
    {
        isDrone = true;
        //insert code to convert scout to drone
        Debug.Log("Scout is now drone");
    }
    
    public void UnlockTelescope()
    {
        canTelescope = true;
        Debug.Log("Scout can now use Telescope");
    }

    public void CheckForTelescope()
    {
        if (!canTelescope)
        {
            Debug.Log("No Telescope Upgrade");
            return;
        }
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            //checks if target is owned by enemyAI
            if (neighbor.placedTower != null && neighbor.placedTower.owner == TurnManager.Instance.currentPlayer)
            {
                visionRange += 4;
            }
            else
            {
                visionRange = baseVision;
            }
        }
    }
}
