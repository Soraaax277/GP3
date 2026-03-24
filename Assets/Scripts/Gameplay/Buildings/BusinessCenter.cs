using UnityEngine;
using System.Collections.Generic;

public class BusinessCenter : StructureNode
{
    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        expansionRadius = 2; // Balanced commercial expansion
        baseGoldCost = 250;
        base.Initialize(tiles, player);
    }

    public override string GetRequiredTechFeature() => "BusinessCenters";

    public static bool IsCorporateManagementActive(PlayerData p)
    {
        // Search all business centers globally for this player
        foreach (var sn in FindObjectsOfType<BusinessCenter>())
            if (sn.owner == p && sn.IsMannedBy<Businessman>()) return true;
        foreach (var sn in FindObjectsOfType<AdvancedBusinessCenter>())
             if (sn.owner == p && sn.IsMannedBy<Businessman>()) return true;
        return false;
    }
}
