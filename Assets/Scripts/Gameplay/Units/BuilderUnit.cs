using UnityEngine;

public class BuilderUnit : Unit
{
    public int moveRange = 2;
    public int buildRange = 1;

    public void BuildTower(TowerNode tower)
    {
        Debug.Log($"[Builder] Build attempt on tower: {tower?.name}");

        if (!CanAct && !testingMode)
        {
            Debug.Log("[Builder] Cannot act (turn/action used)");
            return;
        }

        if (tower == null)
        {
            Debug.Log("[Builder] Tower is null");
            return;
        }

        Debug.Log($"[Builder] Tower state BEFORE build: {tower.state}");

        if (tower.IsBuilt())
        {
            Debug.Log("[Builder] Tower already built");
            return;
        }

        int dist = GridManager.Instance.CubeDistance(
            currentTile.cubeCoords,
            tower.tile.cubeCoords
        );

        Debug.Log($"[Builder] Distance to tower: {dist}");

        if (dist > buildRange)
        {
            Debug.Log("[Builder] Tower too far to build");
            return;
        }

        tower.Build();

        Debug.Log($"[Builder] Tower state AFTER build: {tower.state}");

        ConsumeAction();
        Debug.Log("[Builder] Action consumed");

        SetSelected(false);
        PlayerInput.Instance.ClearHighlights();

        Debug.Log("[Builder] Build completed successfully");
    }

}
