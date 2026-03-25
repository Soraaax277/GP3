using UnityEngine;
using System.Collections.Generic;

public class MicrowaveRelay : StructureNode
{
    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        // Massive signal range for pushing influence across the map
        expansionRadius = 5; 
        baseGoldCost = 300;
        baseInfluenceAmount = 15;
        visionRange = 5; // Has massive line of sight

        base.Initialize(tiles, player);
    }

    public override void Build()
    {
        base.Build();
        ActionLogUI.PostFiltered(owner, $"Microwave Relay established! Network expanded by {expansionRadius} hexes.", ActionLogUI.Colors.Construction);
    }

    public override void OnTurnStart()
    {
        // Add passive mechanics here later if desired.
        // E.g. Manned by IT Personnel or Technician to boost signal further.
    }

    // Replace string with exact feature name in TechTree if it exists. 
    public override string GetRequiredTechFeature() => "MicrowaveRelay";
}
