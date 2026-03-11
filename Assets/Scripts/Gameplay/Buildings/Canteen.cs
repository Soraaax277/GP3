using UnityEngine;

public class Canteen : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 3; // Large structure
        baseGoldCost = 150;
        base.Initialize(tile, player);
    }



    public override string GetRequiredTechFeature() => "Canteens";
}
