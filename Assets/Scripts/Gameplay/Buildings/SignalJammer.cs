using UnityEngine;
using System.Collections.Generic;

public class SignalJammer : StructureNode
{
    private const int SUPPRESSION_AMOUNT = 50;

    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        expansionRadius = 2; // Specialized defense
        baseGoldCost = 300;
        base.Initialize(tiles, player);
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
            // 1. STRIP ENEMIES (Deny active influence from anyone except the Jammer's owner)
            bool strippedSomething = false;
            foreach (PlayerData p in TurnManager.Instance.players)
            {
                if (p != owner)
                {
                    if (t.GetInfluence(p) > 0) strippedSomething = true;
                    // Significantly reduce enemy influence per turn (or flat clear if you prefer, 
                    // but here we use a multiple of the base amount)
                    t.RemoveInfluence(p, baseInfluenceAmount * 10);
                }
            }
            if (strippedSomething) hexesStripped++;

            // 2. ASSERT OWN (Maintain the owner's border)
            t.AddInfluence(owner, baseInfluenceAmount, true);
        }

        ActionLogUI.PostFiltered(owner, "Signal Jammer stripping enemy influence!", owner.isAI ? ActionLogUI.Colors.Enemy : ActionLogUI.Colors.Player);

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
            // Simply remove the owner's jammer footprint
            t.RemoveInfluence(owner, baseInfluenceAmount);
        }

        if (TurnManager.Instance != null) TurnManager.Instance.NotifyStatusChanged();
    }
}
