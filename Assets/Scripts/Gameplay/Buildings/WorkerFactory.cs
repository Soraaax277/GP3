using UnityEngine;

public class WorkerFactory : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 3; // Large industrial building
        baseGoldCost = 300;
        base.Initialize(tile, player);
    }

    public override string GetRequiredTechFeature() => "WorkerFactories";
}