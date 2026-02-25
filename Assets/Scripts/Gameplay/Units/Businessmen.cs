using UnityEngine;

public class Businessman: Unit
{
    public int recruitCharges;
    
    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        SetMoveRange(2);
    }
    
    public void RecruitNearestWorker()
    {
        if (!canAct && !testingMode)
        {
            Debug.Log("Businessman act (turn/action used)");
            return;
        }
        
        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            //checks if target is owned by enemyAI
            if (neighbor.placedTower != null && neighbor.placedTower.owner != TurnManager.Instance.currentPlayer)
            {
                targetTower = neighbor.placedTower;
                break;
            }
        }

        if ( targetTower == null)
        {
            Debug.Log("No tower adjacent!");
            return;
        }

        int procInt = Random.Range(0, 1);
        if (procInt >= 1) // 50/50 on recruitment chance
        { 
            targetTower.Recruit(TurnManager.Instance.currentPlayer);
        }
    }
}
