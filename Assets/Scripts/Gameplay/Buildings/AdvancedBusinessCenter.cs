using UnityEngine;

public class AdvancedBusinessCenter : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 3; // Large corporate headquarters
        baseGoldCost = 400;
        base.Initialize(tile, player);
    }

    public override string GetRequiredTechFeature() => "AdvancedBusinessCenters";
}
