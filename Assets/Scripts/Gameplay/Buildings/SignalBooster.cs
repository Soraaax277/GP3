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

    public override void Build()
    {
        base.Build();
        if (QuestManager.Instance != null && owner != null)
        {
            QuestManager.Instance.SetQuestFlag(owner, "BoostedSignalRange");
        }
    }

    public override string GetRequiredTechFeature() => "SignalBooster";
}
