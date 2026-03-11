using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class SalesMarketer : Unit
{
    public int denyRange = 2;
    public float denyChance = 0.35f;
    public int denyAmount = 5;

    [Header("Phase 3: Charges")]
    public int marketingCharges = 5;
    public int maxMarketingCharges = 5;

    public override int CurrentCharges { get => marketingCharges; set => marketingCharges = value; }
    public override int MaxCharges => maxMarketingCharges;

    public bool canRecruit = false;
    
    public override void CheckTechStatus()
    {
        if (TechManager.Instance == null || owner == null) return;

        // 1. ERA SPECIFIC UPGRADES (Futuristic)
        if (owner.hardwareEra == TurnManager.PlayerEra.Futuristic)
        {
            denyChance = 0.75f; // Neural Marketing
            denyAmount = 10; // High-impact campaigns
        }

        // 2. TECH TREE FEATURES
        if (TechManager.Instance.IsFeatureUnlocked("UnlockRecruiting"))
        {
            canRecruit = true;
        }

        if (TechManager.Instance.IsFeatureUnlocked("GuaranteeInfluence"))
        {
            denyChance = 1.0f;
        }
    }

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
            Debug.Log("[SalesMarketer] Cannot act (turn/action used)");
            return;
        }

        if (!canRecruit && !testingMode)
        {
            Debug.Log("Recruit ability not Unlocked");
            return;
        }

        // Charge consumed on attempt, regardless of success
        if (ShouldConsumeCharge())
        {
            marketingCharges--;
            if (marketingCharges <= 0)
            {
                ConsumeAction();
                Die();
                return;
            }
        }

        Unit targetUnit = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedUnit != null && neighbor.placedUnit.owner != TurnManager.Instance.currentPlayer)
            {
                targetUnit = neighbor.placedUnit;
                break;
            }
        }

        if (targetUnit == null)
        {
            Debug.Log("[SalesMarketer] No enemy unit adjacent — charge still spent.");
            ConsumeAction();
            return;
        }

        // 50% recruitment chance
        if (Random.value >= 0.5f)
        {
            targetUnit.Recruit(owner);
            Debug.Log($"[SalesMarketer] Successfully recruited {targetUnit.name}!");
        }
        else
        {
            Debug.Log("[SalesMarketer] Recruitment failed.");
        }

        ConsumeAction();
    }

    public override void OnTurnStart(PlayerData activePlayer)
    {
        base.OnTurnStart(activePlayer);
    }

    void CreateRangeIndicator()
    {
        rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeIndicator.transform.SetParent(transform);
        rangeIndicator.transform.localPosition = new Vector3(0f, 0f, 0.01f); // Reverted to original height
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

    // ─────────────────────────────────────────────────────────────────────────
    //  DEATH: when all charges are consumed the marketer retires
    // ─────────────────────────────────────────────────────────────────────────
    private void Die()
    {
        Debug.Log($"[SalesMarketer] {owner.playerName}'s Marketer has used all charges and retired.");

        // Clear the tile reference so nothing references a dead unit
        if (currentTile != null && currentTile.placedUnit == this)
            currentTile.placedUnit = null;

        if (TurnManager.Instance != null)
            TurnManager.Instance.UnregisterUnit(this);

        Destroy(gameObject);
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

        if (ShouldConsumeCharge())
        {
            marketingCharges--;
            if (marketingCharges <= 0)
            {
                ConsumeAction();
                Die();
                return;
            }
        }
            
        ConsumeAction();
    }

    public void ClaimInfluence()
    {
        if (!canAct) return;

        Debug.Log($"[SalesMarketer] {owner.playerName}'s Marketer claiming/improving influence on {currentTile.name}");

        // 1. Chance to add own influence (Improve tile)
        if (Random.value < 0.5f)
        {
            currentTile.AddInfluence(owner, denyAmount, true); // Specialist bypasses "First Influence" rule
            Debug.Log($"[SalesMarketer] Improved influence on {currentTile.name} by {denyAmount}");
        }

        // 2. Chance to remove other's influence (Deny) — also deducts from their score
        List<PlayerData> enemyPlayers = currentTile.influenceByPlayer.Keys
            .Where(p => p != owner)
            .ToList();

        foreach (PlayerData enemy in enemyPlayers)
        {
            int enemyInf = currentTile.GetInfluence(enemy);
            if (enemyInf > 0 && Random.value < denyChance)
            {
                int deducted = Mathf.Min(denyAmount, enemyInf);
                currentTile.RemoveInfluence(enemy, deducted);
                Debug.Log($"[SalesMarketer] Reduced {enemy.playerName} influence on {currentTile.name} by {deducted}");
            }
        }

        if (ShouldConsumeCharge())
        {
            marketingCharges--;
            if (marketingCharges <= 0)
            {
                ConsumeAction();
                Die();
                return;
            }
        }
            
        ConsumeAction();
    }

    private void OnMouseEnter() { ShowRange(true); }
    private void OnMouseExit() { ShowRange(false); }
}