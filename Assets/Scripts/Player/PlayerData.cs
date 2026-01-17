using System.Collections.Generic;

public class PlayerData
{
    public int playerId;
    public string playerName;
    public bool isAI;
    public int resources;
    public List<SignalNode> ownedNodes;

    public PlayerData(int id, string name, bool ai = false)
    {
        playerId = id;
        playerName = name;
        isAI = ai;
        resources = 100;
        ownedNodes = new List<SignalNode>();
    }
}
