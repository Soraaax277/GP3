using UnityEngine;
using System.Collections.Generic;

public class ServiceCenter : StructureNode
{
    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        expansionRadius = 2;
        baseGoldCost = 200;
        base.Initialize(tiles, player);
    }

    public override void OnTurnStart()
    {
        if (!IsPowered || !IsMannedBy<MaintenanceCrew>()) return;

        // AUTO-MAINTENANCE: Coordinate repairs for nearby structures
        List<HexTile> inRange = GetTilesInRange();
        int repairCount = 0;

        foreach (var t in inRange)
        {
            if (t == null) continue;
            if (t.placedStructure != null && t.placedStructure.owner == owner && t.placedStructure.IsBroken)
            {
                t.placedStructure.Repair(10f); // Auto-heal 10 HP
                repairCount++;
            }
            if (t.placedTower != null && t.placedTower.owner == owner && t.placedTower.IsDestroyed())
            {
                t.placedTower.Repair(0.10f); // Restore 10%
                repairCount++;
            }
        }

        if (repairCount > 0)
        {
            ActionLogUI.PostFiltered(owner, $"Service Center Team repaired {repairCount} local structures.", ActionLogUI.Colors.Neutral);
        }
    }

    public override string GetRequiredTechFeature() => "ServiceCenter";
}