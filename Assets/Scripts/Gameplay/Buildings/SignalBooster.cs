using UnityEngine;
using System.Collections.Generic;

public class SignalBooster : StructureNode
{
    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        // Randomized expansion radius between 2 and 4 hexes
        expansionRadius = Random.Range(2, 5); 
        baseGoldCost = 250;
        base.Initialize(tiles, player);
    }

    public override void Build()
    {
        base.Build();
        ActionLogUI.PostFiltered(owner, $"Signal Booster calibrated at radius {expansionRadius}!", ActionLogUI.Colors.Construction);
        if (QuestManager.Instance != null && owner != null)
        {
            QuestManager.Instance.SetQuestFlag(owner, "BoostedSignalRange");
        }
    }

    public override void OnTurnStart()
    {
        if (!IsPowered || !IsMannedBy<ITPersonnel>()) return;

        // Count hexes influenced by this booster's radius
        List<HexTile> inRange = GetTilesInRange();
        int income = inRange.Count * 2;
        owner.resources += income;

        ActionLogUI.PostFiltered(owner, $"Signal Monetization: Earned {income}G from {inRange.Count} hexes.", ActionLogUI.Colors.Neutral);
    }

    public override string GetRequiredTechFeature() => "SignalBooster";
}
