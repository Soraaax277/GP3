using UnityEngine;
using System.Collections.Generic;

public class Rocketship : StructureNode
{
    private void Awake()
    {
        // 1 tile footprint to keep the map clickable.
        tilesOccupied = 1; 
        expansionRadius = 3; 
        visionRange = 10;         
        autoScaleToFit = true;
        
        // Locked to 8.93f to reach the target Y=10.24 height exactly.
        verticalOffset = 8.93f;    
    }

    private void Start()
    {
        AutoScaleToFitTiles();
        transform.localScale *= 8.0f;
    }

    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        baseGoldCost = 500;
        base.Initialize(tiles, player);

        // Force ground alignment immediately after instantiation 
        // using the 8.93f offset to reach Y=10.24.
        Vector3 pos = transform.position;
        pos.y = tiles[0].GetSurfaceY() + 8.93f;
        transform.position = pos;
    }

    public bool CanLaunch()
    {
        if (IsMannedBy<Technician>() && IsMannedBy<Businessman>()) return true;

        // Radius 2 cluster (19 tiles) Manning Fallback
        // This ensures your specialists can be standing anywhere within a 2-hex 
        // distance from the rocket's base to activate the launch.
        bool techFound = false;
        bool bizFound = false;

        List<HexTile> manningArea = GridManager.Instance.GetTilesInRange(ParentTile, 2);
        foreach (var tile in manningArea)
        {
            if (tile.placedUnit is Technician t && t.owner == owner) techFound = true;
            if (tile.placedUnit is Businessman b && b.owner == owner) bizFound = true;
        }

        return techFound && bizFound;
    }

    protected override void OnMouseDown()
    {
        // Call base to open the menu
        base.OnMouseDown();

        // Extra: If it can launch, tell the player they can use the button
        if (CanLaunch())
        {
             Debug.Log("[Rocketship] Manned and ready for launch! Button enabled in UI.");
        }
    }

    public void Launch()
    {
        if (!CanLaunch() || owner == null) return;

        // Consume units (remove from play)
        Technician tech = null;
        Businessman biz = null;

        foreach (var t in occupiedTiles)
        {
            if (t.placedUnit is Technician techu && techu.owner == owner) tech = techu;
            if (t.placedUnit is Businessman bizu && bizu.owner == owner) biz = bizu;
        }

        if (tech != null) tech.Die();
        if (biz != null) biz.Die();

        int revenue = 1500;
        owner.resources += revenue;

        ActionLogUI.PostFiltered(owner, $"SATELLITE LAUNCH SUCCESS! Earned {revenue}G Revenue.", ActionLogUI.Colors.Neutral);
        if (FeedbackController.Instance != null) FeedbackController.Instance.PlayLevelUpEffect(transform.position);

        // ── EXODUS VICTORY ─────────────────────────────────────────────────
        // This is the Exodus Victory trigger. The player successfully launched
        // the rocket — fire the endgame sequence.
        if (VictoryManager.Instance != null)
            VictoryManager.Instance.TriggerExodusVictory(owner);
    }

    public override string GetRequiredTechFeature() => "Rocketship";
}
