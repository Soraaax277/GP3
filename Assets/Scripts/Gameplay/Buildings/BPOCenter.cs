using UnityEngine;
using System.Collections.Generic;

public class BPOCenter : StructureNode
{
    [Header("BPO Settings")]
    public int incomePerBusinessperson = 50;
    public int incomePerITPersonnel = 30;

    public override void OnTurnStart()
    {
        if (!IsPowered) return;

        int extraIncome = 0;
        
        // Check for units on this tile
        if (ParentTile.placedUnit != null && ParentTile.placedUnit.owner == owner)
        {
            Unit u = ParentTile.placedUnit;
            if (u is Businessman)
            {
                extraIncome += incomePerBusinessperson;
                Debug.Log($"[BPO Center] Businessman working! Generating {incomePerBusinessperson} gold.");
            }
            else if (u is ITPersonnel)
            {
                extraIncome += incomePerITPersonnel;
                Debug.Log($"[BPO Center] IT Personnel working! Generating {incomePerITPersonnel} gold.");
            }
        }

        if (extraIncome > 0)
        {
            owner.resources += extraIncome;
        }
    }

    public override string GetRequiredTechFeature() => "BPOCenters";
}
