using UnityEngine;

public class BuilderUnit : Unit
{
    public int buildRange = 1;
    public int buildsRemaining = 3;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        SetMoveRange(2);
    }

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
            if (neighbor.placedTower != null && neighbor.placedTower.state == TowerNode.TowerState.Unbuilt)
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

        targetTower.Build();

        buildsRemaining = Mathf.Max(0, buildsRemaining - 1);
        ConsumeAction();
        Debug.Log($"[Builder] Construction complete. Builds left: {buildsRemaining}");

        if (buildsRemaining <= 0)
        {
            Die();
            return;
        }

        if (!owner.isAI)
        {
            SetSelected(false);
            PlayerInput.Instance.ClearHighlights();
            BuildUIManager.Instance.CloseBuildMenu();
        }
    }

    void Die()
    {
        if (currentTile != null) currentTile.placedUnit = null;
        Destroy(gameObject);
    }

}
