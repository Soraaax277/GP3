using System.Collections.Generic;

public class PlayerData
{
    public int playerId;
    public string playerName;
    public int resources;
    public List<SignalNode> ownedNodes;

    public PlayerData(int id, string name)
    {
        playerId = id;
        playerName = name;
        resources = 100;
        ownedNodes = new List<SignalNode>();
    }
}
