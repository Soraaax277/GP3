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
    public int movementRemaining;
    public bool isMoving;
    
    public bool forceCanAct = false;

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
                originalMaterials[i] = renderers[i].material;
        }
    }

    public virtual void OnTurnStart(PlayerData activePlayer)
    {
        if (owner == activePlayer)
        {
            isFresh = false;
            canAct = true;
            movementRemaining = moveRange;
        }
    }

    public void ConsumeAction()
    {
        canAct = false;
        movementRemaining = 0; 
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
                    renderers[i].material.color = Color.green; 
                }
                else
                {
                    renderers[i].material = originalMaterials[i];
                }
            }
        }
    }

    // Accepts 2 arguments as required by PlayerInput and EnemyAI
    public void MoveTo(HexTile tile, int range)
    {
        if (isMoving) return;
        StartCoroutine(MoveRoutine(tile, range));
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
            movementRemaining--;
            limit--;
        }

        currentTile.placedUnit = this;
        isMoving = false;

        if (!owner.isAI)
        {
            SetSelected(false);
            if (PlayerInput.Instance != null) PlayerInput.Instance.ClearHighlights();
        }
    }

    public bool CanMoveTo(HexTile tile, int allowedRange)
    {
        if (isMoving) return false;
        if (!canAct && !testingMode) return false;
        if (movementRemaining <= 0 && !testingMode) return false;

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
}