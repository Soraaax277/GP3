using UnityEngine;

public class SignalJammer : StructureNode
{
    private const int SUPPRESSION_AMOUNT = 50;

    public override void Initialize(HexTile tile, PlayerData player)
    {
        expansionRadius = 2; // Specialized defense
        baseGoldCost = 300;
        base.Initialize(tile, player);
    }

    public override void Build()
    {
        base.Build();
        if (QuestManager.Instance != null && owner != null)
        {
            QuestManager.Instance.SetQuestFlag(owner, "BuiltSignalJammer");

            // Check if built near enemy influence (within 3 hexes)
            var nearby = GridManager.Instance.GetTilesInRange(ParentTile, 3);
            foreach (var t in nearby)
            {
                foreach (var kvp in t.influenceByPlayer)
                {
                    if (kvp.Key != owner && kvp.Value > 0)
                    {
                        QuestManager.Instance.SetQuestFlag(owner, "JammerNearEnemy");
                        return;
                    }
                }
            }
        }
    }

    public override string GetRequiredTechFeature() => "SignalJammers";

    public override void ApplyInfluence()
    {
        if (ParentTile == null || owner == null) return;

        var tiles = GridManager.Instance.GetTilesInRange(ParentTile, expansionRadius);
        int hexesStripped = 0;

        foreach (HexTile t in tiles)
        {
            // 1. ADD SUPPRESSION (Blocks all players' effective influence)
            t.influenceSuppression += SUPPRESSION_AMOUNT;

            // 2. STRIP ENEMIES (Deny active influence)
            bool strippedSomething = false;
            foreach (PlayerData p in TurnManager.Instance.players)
            {
                // ONLY remove influence from players who are NOT the owner of the jammer
                if (p != owner)
                {
                    if (t.GetInfluence(p) > 0) strippedSomething = true;
                    t.RemoveInfluence(p, baseInfluenceAmount * 2);
                }
            }
            if (strippedSomething) hexesStripped++;

            // 3. FORCE OWN (Bypass the dominance rule to establish a footprint)
            t.AddInfluence(owner, baseInfluenceAmount, true);
        }

        if (QuestManager.Instance != null && owner != null && hexesStripped >= 3)
        {
            QuestManager.Instance.SetQuestFlag(owner, "StrippedThreeOverlappingHexes");
        }

        if (TurnManager.Instance != null) TurnManager.Instance.NotifyStatusChanged();
    }

    public override void RemoveInfluence()
    {
        if (ParentTile == null || owner == null) return;

        var tiles = GridManager.Instance.GetTilesInRange(ParentTile, expansionRadius);
        foreach (HexTile t in tiles)
        {
            // Remove the suppression when jammer is gone
            t.influenceSuppression = Mathf.Max(0, t.influenceSuppression - SUPPRESSION_AMOUNT);
            // We don't restore enemy influence — it was deleted.
            t.RemoveInfluence(owner, baseInfluenceAmount);
        }

        if (TurnManager.Instance != null) TurnManager.Instance.NotifyStatusChanged();
    }
}
