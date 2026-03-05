    // The Service Center acts as a recruitment hub for specific workforce units.
    // UI logic will need to check for ServiceCenter presence to enable these units
    // or we can allow the UI to open when clicking this building.
using UnityEngine;

public class ServiceCenter : StructureNode
{
    public override void Initialize(HexTile tile, PlayerData player)
    {
        base.Initialize(tile, player);
    }

    private void OnMouseDown()
    {
        if (owner != TurnManager.Instance.currentPlayer || owner.isAI) return;
        BuildingUIManager.Instance?.Open(this);
    }

    public override string GetRequiredTechFeature() => "ServiceCenter";
}