using UnityEngine;

public class BuildNodeCommand
{
    public static void Execute(HexTile tile, GameObject nodePrefab)
    {
        if (tile.IsOccupied())
        {
            Debug.Log("Tile already occupied");
            return;
        }

        // Clear decorative buildings if they exist
        if (tile.hasStructure)
            tile.ClearEnvironmentalStructures();

        PlayerData player = TurnManager.Instance.currentPlayer;

        GameObject nodeObj = GameObject.Instantiate(
            nodePrefab,
            tile.transform.position,
            Quaternion.identity
        );

        SignalNode node = nodeObj.GetComponent<SignalNode>();
        node.Initialize(tile, player);
    }
}
