using UnityEngine;

public class SignalBooster : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        // Randomized expansion radius between 2 and 4 hexes
        expansionRadius = Random.Range(2, 5); 
        baseGoldCost = 250;
        base.Initialize(tile, player);
    }

    public override string GetRequiredTechFeature() => "SignalBooster";
}
