using UnityEngine;

public class Tesseract : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        base.Initialize(tile, player);
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
}
