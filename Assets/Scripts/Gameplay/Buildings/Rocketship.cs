using UnityEngine;
using System.Collections.Generic;

public class Rocketship : StructureNode
{
    private void Awake()
    {
        tilesOccupied = 1;
        expansionRadius = 3; 
        visionRange = 8;         // Big vision for the end-game structure
        autoScaleToFit = true;
        verticalOffset = 10f;    // Lifts the rocket above ground in all 3 hologram/build states
    }

    private void Start()
    {
        AutoScaleToFitTiles();
        transform.localScale *= 8.0f;

        // Also apply the vertical lift directly on the built structure (Start fires after Initialize)
        Vector3 pos = transform.position;
        pos.y += verticalOffset;
        transform.position = pos;
    }

    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        baseGoldCost = 500;
        base.Initialize(tiles, player);
    }

    public bool CanLaunch() => IsMannedBy<Technician>() && IsMannedBy<Businessman>();

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
