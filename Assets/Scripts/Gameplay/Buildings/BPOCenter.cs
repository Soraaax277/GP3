using UnityEngine;

public class BPOCenter : StructureNode
{
    [Header("BPO Settings")]
    public int incomePerBusinessperson = 50;
    public int incomePerITPersonnel    = 30;

    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 3; // Corporate hubs claim more area
        baseGoldCost = 300;
        base.Initialize(tile, player);
    }

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
        {
            owner.resources += extraIncome;
            if (!owner.isAI)
            {
                string workerName = (ParentTile.placedUnit is Businessman) ? "Businessman" : "IT Personnel";
                ActionLogUI.PostFiltered(owner, $"BPO Center generated bonus income from {workerName}!", ActionLogUI.Colors.Neutral);
            }
        }
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