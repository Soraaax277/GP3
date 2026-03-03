using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NomadicSaboteur : MonoBehaviour
{
    public HexTile currentTile;
    public float destructionChance = 0.2f;

    public void Initialize(HexTile startTile)
    {
        currentTile = startTile;
        // Don't block tiles for units, just coexist
        startTile.placedUnit = null; 
    }

    public void PerformTurnAction()
    {
        // 1. Check if adjacent to player/enemy infrastructure
        List<HexTile> neighbors = GridManager.Instance.GetNeighbors(currentTile);
        foreach (HexTile neighbor in neighbors)
        {
            if (neighbor.placedTower != null && !neighbor.placedTower.IsDestroyed())
            {
                if (Random.value < destructionChance)
                {
                    neighbor.placedTower.TakeDamage(999f); // Instant destruction
                    Debug.Log($"[Saboteur] Tower at {neighbor.cubeCoords} DESTROYED by Saboteur!");
                    Die();
                    return;
                }
            }
            if (neighbor.placedWire != null && !neighbor.placedWire.isDestroyed)
            {
                if (Random.value < destructionChance)
                {
                    neighbor.placedWire.TakeDamage(999f); // Instant destruction
                    Debug.Log($"[Saboteur] Wire at {neighbor.cubeCoords} DESTROYED by Saboteur!");
                    Die();
                    return;
                }
            }
        }

        // 2. If no infrastructure nearby or chance failed, move toward the nearest one
        MoveToNearestTarget();
    }

    public void Die()
    {
        if (HazardManager.Instance != null)
        {
            HazardManager.Instance.activeSaboteurs.Remove(this);
        }
        Destroy(gameObject);
    }

    private void MoveToNearestTarget()
    {
        // Simple AI: find nearest tower or wire
        TowerNode nearestTower = null;
        float minDist = float.MaxValue;

        foreach (var tower in TurnManager.Instance.GetAllTowers())
        {
            if (tower == null) continue;
            float d = Vector3.Distance(transform.position, tower.transform.position);
            if (d < minDist) { minDist = d; nearestTower = tower; }
        }

        if (nearestTower != null)
        {
            // Pathfind or move towards
            List<HexTile> path = GridManager.Instance.FindPath(currentTile, nearestTower.tile);
            if (path != null && path.Count > 1)
            {
                // Move 1 tile towards target
                StartCoroutine(MoveRoutine(path[1]));
            }
        }
        else
        {
            // No target, just wander
            List<HexTile> neighbors = GridManager.Instance.GetNeighbors(currentTile);
            if (neighbors.Count > 0)
            {
                HexTile randomNeighbor = neighbors[Random.Range(0, neighbors.Count)];
                if (randomNeighbor.type == HexTile.TileType.Land)
                    StartCoroutine(MoveRoutine(randomNeighbor));
            }
        }
    }

    private IEnumerator MoveRoutine(HexTile target)
    {
        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos = target.transform.position + Vector3.up;

        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        currentTile = target;
    }
}
