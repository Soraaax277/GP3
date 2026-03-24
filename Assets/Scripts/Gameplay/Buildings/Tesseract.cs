using UnityEngine;
using System.Collections.Generic;

public class Tesseract : StructureNode
{
    private void Awake() { tilesOccupied = 7; }

    public override void Initialize(List<HexTile> tiles, PlayerData player)
    {
        baseGoldCost = 1000;
        base.Initialize(tiles, player);
        ApplyTesseractEffect(true);

        if (QuestManager.Instance != null && player != null)
        {
            QuestManager.Instance.SetQuestFlag(player, "ConnectedDigitalNode");
        }
    }

    private void ApplyTesseractEffect(bool active)
    {
        // Tesseract uniquely powers ALL wires.
        // We'll set a global flag or iterate all wires. 
        // For simplicity, let's assume we can set a flag in PowerGridManager or similar.
        // If PowerGridManager doesn't support this yet, we'll need to update it.
        Debug.Log($"[Tesseract] Global power effect {(active ? "Activated" : "Deactivated")}");
        if (active)
            ActionLogUI.PostFiltered(owner, "Global Tesseract Power Grid ACTIVATED!", ActionLogUI.Colors.Construction);
        else
            ActionLogUI.PostFiltered(owner, "Global Tesseract Power Offline!", ActionLogUI.Colors.Construction);
        
        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
        }
    }

    protected override void DestroyStructure()
    {
        ApplyTesseractEffect(false);
        base.DestroyStructure();
    }

    public override string GetRequiredTechFeature() => "Tesseract";

    public override void OnTurnStart()
    {
        if (!IsPowered || !IsMannedBy<ITPersonnel>()) return;

        // DATA HARVESTING: Extraction from Neutral/Enemy tiles in massive range
        List<HexTile> inRange = GetTilesInRange();
        int harvestCount = 0;
        foreach (var t in inRange)
        {
            if (t != null && t.GetOwner() != owner) harvestCount++;
        }

        int income = harvestCount * 15;
        owner.resources += income;

        if (income > 0)
        {
            ActionLogUI.PostFiltered(owner, $"Data Harvesting: Extracted {income}G from {harvestCount} hexes.", ActionLogUI.Colors.Neutral);
        }
    }
}
