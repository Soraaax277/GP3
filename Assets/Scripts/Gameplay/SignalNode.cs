using UnityEngine;

public class SignalNode : MonoBehaviour
{
    public PlayerData owner;
    public HexTile tile;
    public int range = 2;

    public void Initialize(PlayerData player, HexTile hexTile)
    {
        owner = player;
        tile = hexTile;

        tile.placedNode = this;
        player.ownedNodes.Add(this);
    }
}
