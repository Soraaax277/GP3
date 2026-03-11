using UnityEngine;

public class Rocketship : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 3; // Large specialized structure
        baseGoldCost = 500;
        base.Initialize(tile, player);
    }

    public override string GetRequiredTechFeature() => "Rocketship";
}
