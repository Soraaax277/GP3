using UnityEngine;
using System.Collections.Generic;

public class Rocketship : StructureNode
{
    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        tilesOccupied = 2; // Large specialized structure
        expansionRadius = 3; 
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
    }

    public override string GetRequiredTechFeature() => "Rocketship";
}
