using UnityEngine;

public class AdvancedServiceCenter : ServiceCenter
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 3; // Improved service hub
        baseGoldCost = 350;
        base.Initialize(tile, player);
    }

    // Potentially add more unit types or improved stats
    public override string GetRequiredTechFeature() => "AdvancedServiceCenter";
}
