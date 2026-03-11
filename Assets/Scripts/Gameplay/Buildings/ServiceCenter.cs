    // The Service Center acts as a recruitment hub for specific workforce units.
    // UI logic will need to check for ServiceCenter presence to enable these units
    // or we can allow the UI to open when clicking this building.
using UnityEngine;

public class ServiceCenter : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 2;
        baseGoldCost = 200;
        base.Initialize(tile, player);
    }



    public override string GetRequiredTechFeature() => "ServiceCenter";
}