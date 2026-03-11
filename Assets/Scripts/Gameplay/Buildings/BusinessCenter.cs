using UnityEngine;

public class BusinessCenter : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 2; // Balanced commercial expansion
        baseGoldCost = 250;
        base.Initialize(tile, player);
    }

    public override string GetRequiredTechFeature() => "BusinessCenters";
}
