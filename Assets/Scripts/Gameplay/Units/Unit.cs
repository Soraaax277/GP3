using UnityEngine;
using System.Collections; 
using System.Collections.Generic;

public abstract class Unit : MonoBehaviour
{
    public HexTile currentTile;
    public PlayerData owner;

    public bool canAct;
    public bool isFresh;
    public bool isRecruited = false;
    public bool testingMode = false;

    private Renderer[] renderers;
    private Material[] originalMaterials;

    public int moveRange = 3;
    public int visionRange = 2;
    public int movementRemaining;
    public bool isMoving;
    
    public bool forceCanAct = false;
    
    [Header("Veterancy & Charges")]
    public int level = 1;
    public int refillsHappened = 0;
    
    public abstract int CurrentCharges { get; set; }
    public abstract int MaxCharges { get; }
    
    public virtual void RefillCharges()
    {
        CurrentCharges = MaxCharges;
        refillsHappened++;
        
        if (refillsHappened >= 3)
        {
            LevelUp();
        }
    }

    public virtual void LevelUp()
    {
        if (level >= 3) return;
        
        level++;
        refillsHappened = 0;
        
        Debug.Log($"{name} leveled up to Level {level}!");
        
        // Apply Perks
        if (level == 2)
        {
            moveRange += 1;
            movementRemaining = moveRange;
            if (FeedbackController.Instance != null)
                FeedbackController.Instance.PlayLevelUpEffect(transform.position);
        }
        else if (level == 3)
        {
            // "Expert" Perk: 20% chance to not consume a charge
            if (FeedbackController.Instance != null)
                FeedbackController.Instance.PlayLevelUpEffect(transform.position);
        }
    }

    public bool ShouldConsumeCharge()
    {
        if (level >= 3)
        {
            // Expert Perk: 20% chance to NOT consume charge
            return Random.value > 0.20f;
        }
        return true;
    }

    //  UPKEEP  (System 3)
    //  Gold subtracted from the owning player at the start of each turn.
    //  Override this value in subclasses (e.g. BuilderUnit, TechnicianUnit)
    //  to give each unit type its own upkeep cost.
    public int goldUpkeep = 10;

    public bool CanAct => canAct;
    public bool CanSelect => owner == TurnManager.Instance.currentPlayer;
    public bool IsFresh => isFresh;

    public virtual void Initialize(HexTile spawnTile, PlayerData player)
    {
        currentTile = spawnTile;
        owner = player;

        transform.position = spawnTile.transform.position + Vector3.up * 1f;

        isFresh = true;
        canAct = false;
        
        spawnTile.placedUnit = this;

        // Register with TurnManager
        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterUnit(this);

        renderers = GetComponentsInChildren<Renderer>();

        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originalMaterials[i] = renderers[i].sharedMaterial; // Save sharedMaterial to avoid cloning
        }

        CheckTechStatus();
    }

    public virtual void CheckTechStatus() { }

    public virtual void OnTurnStart(PlayerData activePlayer)
    {
        if (owner == activePlayer)
        {
            CheckTechStatus();
            isFresh = false;
            canAct = true;
            movementRemaining = moveRange;
        }
    }

    public void ConsumeAction()
    {
        canAct = false;
        // movementRemaining remains as is, allowing units to move after actions
    }

    public void SetSelected(bool selected)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                if (selected)
                {
                    // Accessing .material creates an instance clone.
                    // We only do this when selecting to apply the tint.
                    renderers[i].material.color = Color.green; 
                }
                else
                {
                    // Reassign the original shared material to clear the instance clone
                    renderers[i].sharedMaterial = originalMaterials[i];
                }
            }
        }
    }

    // Accepts 2 arguments as required by PlayerInput and EnemyAI
    public void MoveTo(HexTile tile, int range)
    {
        if (isMoving) return;
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[Unit] MoveTo called on inactive GameObject: {name}. Skipping.");
            return;
        }

        // Keep selection active during movement so the UI doesn't flicker away
        // if (!owner.isAI) SetSelected(false); 
        
        StartCoroutine(MoveRoutine(tile, range));
    }

    public virtual int GetRefillCost()
    {
        // 50 Gold base, minus something for veterancy or plus something?
        return 75;
    }
    
    public bool IsNearServiceCenter()
    {
        if (currentTile == null) return false;
        
        // Check current tile
        if (currentTile.placedStructure is ServiceCenter sc && sc.owner == owner) return true;
        
        // Check neighbors
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedStructure is ServiceCenter neighborSc && neighborSc.owner == owner)
                return true;
        }
        
        return false;
    }

    private IEnumerator MoveRoutine(HexTile target, int range)
    {
        isMoving = true;
        
        List<HexTile> path = GridManager.Instance.FindPath(currentTile, target);
        if (path == null || path.Count == 0)
        {
            isMoving = false;
            yield break;
        }

        // Remove start tile from path
        path.RemoveAt(0);

        currentTile.placedUnit = null;

        // Use the passed 'range' or movementRemaining, whichever is smaller
        int limit = Mathf.Min(range, movementRemaining);

        for (int i = 0; i < path.Count; i++)
        {
            if (limit <= 0 && !testingMode) break;

            HexTile nextTile = path[i];
            Vector3 startPos = transform.position;
            Vector3 endPos = nextTile.transform.position + Vector3.up * 1f;
            
            float duration = 0.3f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            transform.position = endPos;
            currentTile = nextTile;
            
            // Trigger dynamic vision update during movement
            if (FieldOfViewManager.Instance != null && owner != null && !owner.isAI)
            {
                FieldOfViewManager.Instance.UpdateFogOfWar(owner);
            }

            movementRemaining--;
            limit--;
        }

        currentTile.placedUnit = this;
        isMoving = false;

        if (!owner.isAI)
        {
            if (PlayerInput.Instance != null && PlayerInput.Instance.selectedUnit == this)
            {
                // Refresh the action panel and detail panel after moving, in case new actions (like construction) are now possible.
                UnitActionPanel.Instance?.Open(this);
                DetailPanel.Instance?.ShowUnit(this);
            }
        }
    }

    public bool CanMoveTo(HexTile tile, int allowedRange)
    {
        if (isMoving) return false;
        if (!canAct && !testingMode) return false;
        if (movementRemaining <= 0 && !testingMode) return false;

        // Block if target tile is already occupied by ANOTHER unit
        if (tile.placedUnit != null && tile.placedUnit != this) return false;

        List<HexTile> path = GridManager.Instance.FindPath(currentTile, tile);
        if (path == null) return false;
        
        int dist = path.Count - 1;
        return dist <= movementRemaining && dist <= allowedRange; 
    }
    
    public void SetMoveRange(int range)
    {
        moveRange = range;
    }

    public virtual void ReceiveStatUpgrade(string statName, float amount)
    {
        if (statName == "MoveRange" || statName == "Movement")
        {
            moveRange += (int)amount;
            Debug.Log($"{name} received +{(int)amount} Move Range");
        }
        else if (statName == "Actions")
        {
            // "Actions" is a universal stat that applies to all worker types.
            // Subclasses override this to apply to their specific charge stat.
            Debug.Log($"{name} received +{(int)amount} Actions (base implementation)");
        }
        else if (statName == "UpkeepReduction")
        {
            goldUpkeep = Mathf.Max(0, goldUpkeep - (int)amount);
            Debug.Log($"{name} upkeep reduced by {(int)amount}, now {goldUpkeep}");
        }
    }

    public void Recruit(PlayerData newOwner)
    {
        if (!isRecruited)
        {
            owner = newOwner;
        }
        isRecruited = true;
    }

    public virtual void UnlockSkill(string skillName)
    {
        Debug.Log($"{name} unlocked skill: {skillName}");
    }

    public virtual void Die()
    {
        currentTile.placedUnit = null;
        if (TurnManager.Instance != null)
            TurnManager.Instance.UnregisterUnit(this);
        
        Debug.Log($"[Unit] {name} has been killed!");
        Destroy(gameObject);
    }
}