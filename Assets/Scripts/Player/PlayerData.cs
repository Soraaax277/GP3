using System.Collections.Generic;
using System.Linq;

public class PlayerData
{
    public int playerId;
    public string playerName;
    public bool isAI;
    
    // "Resources" = Gold/Revenue
    public int resources; 
    
    // Research Points
    public int researchPoints; 

    public List<SignalNode> ownedNodes;

    //  ERA TRACKING  (System 1)
    //  hardwareEra  — the player's current technology generation for towers/wires.
    //  workforceEra — the player's current workforce skill level.
    //  If hardwareEra is ahead of the World Era → obsolete-tech influence debuff.
    //  If hardwareEra is ahead of workforceEra   → unskilled-labor upkeep penalty.
    public TurnManager.PlayerEra hardwareEra  = TurnManager.PlayerEra.Industrial;
    public TurnManager.PlayerEra workforceEra = TurnManager.PlayerEra.Industrial;

    public int GetTotalInfluence()
    {
        // Sums up the influence value this player has on every tile in the grid
        return GridManager.Instance.tiles.Values
            .Sum(tile => tile.influenceByPlayer.ContainsKey(this) ? tile.influenceByPlayer[this] : 0);
    }
    
    // Helper to count active towers
    public int GetActiveTowerCount()
    {
        // Finds all towers owned by this player that are fully built
        return UnityEngine.Object.FindObjectsByType<TowerNode>(UnityEngine.FindObjectsSortMode.None)
            .Count(t => t.owner == this && t.IsBuilt());
    }

    public PlayerData(int id, string name, bool ai = false)
    {
        playerId = id;
        playerName = name;
        isAI = ai;
        resources = 100; // Starting Gold
        researchPoints = 0; // Starting RP
        ownedNodes = new List<SignalNode>();
    }
}