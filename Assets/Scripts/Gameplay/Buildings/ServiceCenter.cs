using UnityEngine;

public class ServiceCenter : StructureNode
{
    // The Service Center acts as a recruitment hub for specific workforce units.
    // UI logic will need to check for ServiceCenter presence to enable these units
    // or we can allow the UI to open when clicking this building.

    public override void Initialize(HexTile tile, PlayerData player)
    {
        base.Initialize(tile, player);
        // Visuals or setup
    }

    private void OnMouseDown()
    {
        // Open recruitment UI for this center if it's the player's turn
        if (owner == TurnManager.Instance.currentPlayer && !owner.isAI)
        {
            if (UnitPurchaseUI.Instance != null)
            {
                // We'll need to update UnitPurchaseUI to support Service Centers
                UnitPurchaseUI.Instance.OpenForServiceCenter(this);
            }
        }
    }

    public override string GetRequiredTechFeature() => "ServiceCenter";
}
