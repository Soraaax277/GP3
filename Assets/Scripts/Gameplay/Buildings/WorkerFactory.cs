using UnityEngine;
using System.Collections.Generic;
public class WorkerFactory : StructureNode
{
    private void Awake()
    {
        tilesOccupied = 2; // Large industrial building
        expansionRadius = 3; 
        visionRange = 4; // Explicitly ensure it grants generous vision like the other structures
        autoScaleToFit = true; // FORCE this on, in case it was disabled in the inspector
    }

    private void Start()
    {
        // Unity's Renderer bounds can be completely invalid on the exact microsecond an object is instantiated,
        // causing the Auto-Scaler to calculate an infinitely tiny mesh and massively over-inflate the building.
        // By deferring the AutoScale check to Start(), we guarantee the mesh boundaries are physically 
        // instantiated and valid.
        
        AutoScaleToFitTiles();
        
        // The default AutoScale math still makes the WorkerFactory feel far too bulky and huge.
        // We forcefully chop its final scaled size here by an extreme 80% (making it 0.20x its mathematically calculated bulk).
        // This guarantees it is much, much smaller in all 3 states exactly as requested!
        transform.localScale *= 0.20f;
    }

    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        baseGoldCost = 300;
        base.Initialize(tiles, player);
    }

    public override string GetRequiredTechFeature() => "WorkerFactories";
}