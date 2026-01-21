using System.Collections.Generic;
using System.Linq;

public class PlayerData
{
    public int playerId;
    public string playerName;
    public bool isAI;
    public int resources;
    public List<SignalNode> ownedNodes;

    public int GetTotalInfluence()
    {
        return GridManager.Instance.tiles.Values
            .Sum(tile => tile.influenceByPlayer.ContainsKey(this) ? tile.influenceByPlayer[this] : 0);
    }

    public PlayerData(int id, string name, bool ai = false)
    {
        playerId = id;
        playerName = name;
        isAI = ai;
        resources = 100;
        ownedNodes = new List<SignalNode>();
    }
}
