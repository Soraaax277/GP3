using UnityEngine;

public class BuilderUnit : Unit
{
    public int moveRange = 2;
    public int buildRange = 1;
    public int buildsRemaining = 3;

    public void ConstructAdjacentTower()
    {
        if (!canAct && !testingMode)
        {
            Debug.Log("[Builder] Cannot act (turn/action used)");
            return;
        }

        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedTower != null && !neighbor.placedTower.IsBuilt())
            {
                targetTower = neighbor.placedTower;
                break;
            }
        }

        if (targetTower == null)
        {
            Debug.Log("[Builder] No unbuilt tower adjacent!");
            return;
        }

        // Adjacency check for power connection is already handled during placement,
        // but we can re-verify here if needed.
        
        targetTower.Build();

        buildsRemaining--;
        ConsumeAction();
        Debug.Log($"[Builder] Construction complete. Builds left: {buildsRemaining}");

        if (buildsRemaining <= 0)
        {
            Die();
        }

        SetSelected(false);
        PlayerInput.Instance.ClearHighlights();
        BuildUIManager.Instance.CloseBuildMenu();
    }

    void Die()
    {
        if (currentTile != null) currentTile.placedUnit = null;
        Destroy(gameObject);
    }

}
