using UnityEngine;

public class DroneFactory : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 3; // Large factory structure
        baseGoldCost = 350;
        base.Initialize(tile, player);
    }

    public override string GetRequiredTechFeature() => "DroneFactories";
}
