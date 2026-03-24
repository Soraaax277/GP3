using UnityEngine;
using System.Collections.Generic;

public class WorkerFactory : StructureNode
{
    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        tilesOccupied = 2; // Large industrial building
        expansionRadius = 3; 
        baseGoldCost = 300;
        base.Initialize(tiles, player);
    }

    public override string GetRequiredTechFeature() => "WorkerFactories";
}