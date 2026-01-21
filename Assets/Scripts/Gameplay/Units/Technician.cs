using UnityEngine;
using System.Linq;

public class Technician : Unit
{
    public int repairCharges = 2;

    public void RepairAdjacentStructure()
    {
        if (!canAct && !testingMode)
        {
            Debug.Log("[Technician] Cannot act (turn/action used)");
            return;
        }

        TowerNode targetTower = null;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(currentTile))
        {
            if (neighbor.placedTower != null && neighbor.placedTower.IsDestroyed())
            {
                targetTower = neighbor.placedTower;
                break;
            }
        }

        if (targetTower == null)
        {
            Debug.Log("[Technician] No destroyed tower adjacent!");
            return;
        }

        targetTower.Repair();

        repairCharges--;
        ConsumeAction();
        Debug.Log($"[Technician] Repair complete. Charges left: {repairCharges}");

        if (repairCharges <= 0)
        {
            Die();
            return;
        }

        if (!owner.isAI)
        {
            SetSelected(false);
            PlayerInput.Instance.ClearHighlights();
        }
    }

    void Die()
    {
        if (currentTile != null) currentTile.placedUnit = null;
        Destroy(gameObject);
    }
}
