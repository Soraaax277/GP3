using UnityEngine;

public class Canteen : StructureNode
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

    public override string GetRequiredTechFeature() => "Canteens";
}
