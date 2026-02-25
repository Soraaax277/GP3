using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class SalesMarketer : Unit
{
    public int denyRange = 2;
    public float denyChance = 0.35f;
    public int denyAmount = 5;

    public bool canRecruit = false;
    
    

    private GameObject rangeIndicator;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        CreateRangeIndicator();
        ShowRange(true);
    }

    public override void UnlockSkill(string skillName)
    {
        if (skillName == "UnlockRecruiting")
        {
            UnlockRecruiting();
        }
        else if (skillName == "GuaranteeInfluence")
        {
            GuaranteeInfluence();
        }
    }

    public void UnlockRecruiting()
    {
        canRecruit = true;
        Debug.Log("SalesMarketer can now recruit Enemy Workers");
    }

    public void GuaranteeInfluence()
    {
        denyChance = 1;
        Debug.Log("SalesMarketer now guarantees influence");
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "DenyRange")
        {
            denyRange += (int)amount;
            if (rangeIndicator != null)
            {
                float visualRadius = denyRange * GridManager.Instance.hexSize;
                rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
            }
            Debug.Log($"SalesMarketer: Deny range increased to {denyRange}");
        }
        else if (statName == "DenyChance")
        {
            denyChance += amount;
            Debug.Log($"SalesMarketer: Deny chance increased by {amount * 100}% (now {denyChance * 100}%)");
        }
        else if (statName == "DenyAmount")
        {
            denyAmount += (int)amount;
            Debug.Log($"SalesMarketer: Deny amount increased by {(int)amount} (now {denyAmount})");
        }
        else if (statName == "Actions")
        {
            // SalesMarketer doesn't have action charges like other units
            // Actions for SalesMarketer might enable multiple denies per turn
            Debug.Log($"SalesMarketer received +{(int)amount} Actions (passive unit)");
        }
    }

    public void RecruitNearestWorker()
    {
        if (!canAct && !testingMode)
        {
            Debug.Log("[Saboteur] Cannot act (turn/action used)");
            return;
        }

        if (!canRecruit && !testingMode)
        {
            Debug.Log("Recruit ability not Unlocked");
            return;
        }
        Unit targetUnit = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            //checks if target is owned by enemyAI
            if (neighbor.placedUnit != null && neighbor.placedUnit.owner != TurnManager.Instance.currentPlayer)
            {
                targetUnit = neighbor.placedUnit;
                break;
            }
        }

        if (targetUnit == null)
        {
            Debug.Log("No unit adjacent!");
            return;
        }

        int procInt = Random.Range(0, 1);
        if (procInt >= 1) //procInt has a 20% chance of being 4
        { 
            targetUnit.Recruit(owner);
        }
    }

    public override void OnTurnStart(PlayerData activePlayer)
    {
        base.OnTurnStart(activePlayer);
    }

    void CreateRangeIndicator()
    {
        rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeIndicator.transform.SetParent(transform);
        rangeIndicator.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        rangeIndicator.transform.localRotation = Quaternion.identity;

        float visualRadius = denyRange * GridManager.Instance.hexSize;
        rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);

        Renderer rend = rangeIndicator.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = new Color(0.5f, 0f, 1f, 0.25f);

        Destroy(rangeIndicator.GetComponent<Collider>());
    }

    public void ShowRange(bool show)
    {
        if (rangeIndicator != null)
            rangeIndicator.SetActive(show);
    }

    public void PerformDeny()
    {
        if (!canAct) return;

        Debug.Log($"[SalesMarketer] {owner.playerName}'s Marketer performing Deny action.");

        List<HexTile> tilesInRange = GridManager.Instance.GetTilesInRange(currentTile, denyRange);
        int tilesAffected = 0;

        bool sabotageUnlocked = TechManager.Instance != null && TechManager.Instance.IsSabotageTabUnlocked();

        foreach (HexTile tile in tilesInRange)
        {
            // SABOTAGE: Apply persistent suppression if unlocked
            if (sabotageUnlocked)
            {
                tile.influenceSuppression += denyAmount;
                Debug.Log($"[SalesMarketer] Sabotage! Added {denyAmount} suppression to {tile.name}");
            }

            // Create a list of players to remove influence from (excluding the owner)
            List<PlayerData> enemyPlayers = tile.influenceByPlayer.Keys
                .Where(p => p != owner)
                .ToList();

            foreach (PlayerData enemy in enemyPlayers)
            {
                if (tile.GetInfluence(enemy) > 0)
                {
                    if (Random.value <= denyChance)
                    {
                        tile.RemoveInfluence(enemy, denyAmount);
                        tilesAffected++;
                        Debug.Log($"[SalesMarketer] Successfully denied influence for {enemy.playerName} at {tile.name}");
                    }
                }
            }
        }

        Debug.Log($"[SalesMarketer] Deny action complete. Tiles affected: {tilesAffected}");
        ConsumeAction();
    }

    private void OnMouseEnter() { ShowRange(true); }
    private void OnMouseExit() { ShowRange(false); }
}