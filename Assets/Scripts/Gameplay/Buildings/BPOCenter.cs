using UnityEngine;

public class BPOCenter : StructureNode
{
    [Header("BPO Settings")]
    public int incomePerBusinessperson = 50;
    public int incomePerITPersonnel    = 30;

    public override void OnTurnStart()
    {
        if (!IsPowered) return;

        int extraIncome = 0;

        if (ParentTile.placedUnit != null && ParentTile.placedUnit.owner == owner)
        {
            Unit u = ParentTile.placedUnit;
            if (u is Businessman)
            {
                extraIncome += incomePerBusinessperson;
                Debug.Log($"[BPO Center] Businessman working! +{incomePerBusinessperson}G.");
            }
            else if (u is ITPersonnel)
            {
                extraIncome += incomePerITPersonnel;
                Debug.Log($"[BPO Center] IT Personnel working! +{incomePerITPersonnel}G.");
            }
        }

        if (extraIncome > 0)
            owner.resources += extraIncome;
    }

    private void OnMouseDown()
    {
        if (owner != TurnManager.Instance.currentPlayer || owner.isAI) return;
        BuildingUIManager.Instance?.Open(this);
    }

    public string GetCurrentWorkerName()
    {
        if (ParentTile.placedUnit == null || ParentTile.placedUnit.owner != owner) return "None";
        if (ParentTile.placedUnit is Businessman) return "Businessman";
        if (ParentTile.placedUnit is ITPersonnel) return "IT Personnel";
        return "None";
    }

    public int GetCurrentWorkerIncome()
    {
        if (ParentTile.placedUnit == null || ParentTile.placedUnit.owner != owner) return 0;
        if (ParentTile.placedUnit is Businessman) return incomePerBusinessperson;
        if (ParentTile.placedUnit is ITPersonnel) return incomePerITPersonnel;
        return 0;
    }

    public override string GetRequiredTechFeature() => "BPOCenters";
}