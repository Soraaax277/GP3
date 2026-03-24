using UnityEngine;
using System.Collections.Generic;

public class AdvancedBusinessCenter : StructureNode
{
    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        tilesOccupied = 2; // High-tier regional corporate hub
        expansionRadius = 3; 
        baseGoldCost = 400;
        base.Initialize(tiles, player);
    }

    public override string GetRequiredTechFeature() => "AdvancedBusinessCenters";
}
