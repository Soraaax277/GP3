using UnityEngine;
using System.Collections.Generic;

public class AdvancedServiceCenter : ServiceCenter
{
    private void Awake() { tilesOccupied = 2; }

    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        expansionRadius = 3; 
        baseGoldCost = 350;
        base.Initialize(tiles, player);
    }

    // Potentially add more unit types or improved stats
    public override string GetRequiredTechFeature() => "AdvancedServiceCenter";
}
