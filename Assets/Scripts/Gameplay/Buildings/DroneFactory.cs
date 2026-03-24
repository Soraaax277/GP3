using UnityEngine;
using System.Collections.Generic;

public class DroneFactory : StructureNode
{
    private void Awake() { tilesOccupied = 4; }

    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        expansionRadius = 3; 
        baseGoldCost = 350;
        base.Initialize(tiles, player);
    }

    public override string GetRequiredTechFeature() => "DroneFactories";
}
