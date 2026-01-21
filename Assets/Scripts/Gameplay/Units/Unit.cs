using UnityEngine;
using System.Collections.Generic;

public abstract class Unit : MonoBehaviour
{
    public HexTile currentTile;
    public PlayerData owner;

    public bool canAct;
    public bool isFresh;
    public bool testingMode = false;

    private Renderer[] renderers;
    private Material[] originalMaterials;
    private Material outlineMaterial;

    public int moveRange = 3;
    public int movementRemaining;
    public bool isMoving;

    public bool forceCanAct = false;

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

        TurnManager.Instance.RegisterUnit(this);

        renderers = GetComponentsInChildren<Renderer>();

        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].material;
        }

        outlineMaterial = new Material(Shader.Find("Sprites/Default"));
        outlineMaterial.color = new Color(0f, 1f, 0f, 0.7f);

        SetMoveRange(moveRange);
        
        if (testingMode)
        {
            canAct = true;
            isFresh = false;
        }
    }

    public virtual void SetMoveRange(int range)
    {
        moveRange = range;
        movementRemaining = moveRange;
    }

    public void SetSelected(bool selected)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material = selected ? outlineMaterial : originalMaterials[i];
        }
    }

    public virtual void OnTurnStart(PlayerData activePlayer)
    {
        if (owner != activePlayer) return;

        isFresh = false;
        canAct = true;
        movementRemaining = moveRange;
    }

    public void ConsumeAction()
    {
        canAct = false;
        movementRemaining = 0;
    }

    public void MoveTo(HexTile tile, int allowedRange)
    {
        if (isMoving) return;
        
        if (TurnManager.Instance.currentPlayer != owner && !testingMode)
        {
            Debug.Log("Wait for your turn again!");
            return;
        }

        if (!canAct && !testingMode)
        {
            Debug.Log("Unit already acted or out of movement!");
            return;
        }

        List<HexTile> path = GridManager.Instance.FindPath(currentTile, tile);
        if (path == null || path.Count - 1 > movementRemaining)
        {
            Debug.Log("No valid path or too far!");
            return;
        }

        StartCoroutine(MoveRoutine(path));
    }

    private System.Collections.IEnumerator MoveRoutine(List<HexTile> path)
    {
        isMoving = true;
        
        if (currentTile != null)
            currentTile.placedUnit = null;

        for (int i = 1; i < path.Count; i++)
        {
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
        }

        currentTile.placedUnit = this;
        isMoving = false;

        if (!owner.isAI)
        {
            SetSelected(false);
            PlayerInput.Instance.ClearHighlights();
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
        if (dist > movementRemaining) return false;
        if (tile == currentTile) return true;
        if (tile.IsOccupied()) return false;

        return true;
    }
}
