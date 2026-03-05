using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Tech", menuName = "Tech Tree/Tech Node")]
public class TechNode : ScriptableObject
{
    [Header("Info")] 
    public string techName; 
    [TextArea] public string description;
    
    [Header("Costs")]
    public int researchCost; // Deducted from Research Points
    public int goldCost;     // Deducted from Gold (Resources)

    [Header("Tab Requirements")]
    [Tooltip("When TRUE, researching this node unlocks the Sabotage tab. Multiple nodes can have this set — the tab unlocks as soon as ANY one of them is researched.")]
    public bool unlocksSabotageTab = false;

    [Header("Passive Research Bonus")]
    [Tooltip("Flat Research Points added to the player's per-turn RP income when this tech is unlocked. Stacks additively. Leave at 0 for no bonus.")]
    public int rpBonusPerTurn = 0;
    
    [Header("Prerequisite Tech")]
    public List<TechNode> preReqs;

    [Header("Tech Effects")] 
    public List<TechEffect> unlockEffects;

    // -----------------------------------------------------------------------
    //  UNLOCK STATE — stored per-player in TechManager, NOT on this asset.
    //
    //  TechNode is a ScriptableObject: there is exactly ONE instance of each
    //  asset shared by every player at runtime.  Storing _isUnlocked here
    //  (even as [System.NonSerialized]) means Player 1 and the AI share the
    //  same boolean — so any tech the AI researches immediately appears
    //  unlocked in the human player's tree.
    //
    //  IsUnlocked / CanUnlock / UnlockTech now all require a PlayerData so
    //  they delegate to TechManager's per-player unlock set.
    // -----------------------------------------------------------------------
    // Added since a bug was found where the AI would research a tech and it would appear unlocked for the human player, since TechNode is a ScriptableObject and shared across all players.
    // Now the unlock state is tracked in TechManager per player, and these methods check that instead.
    // Feel free to explore other approaches if you think of a cleaner way to handle this, but this was the minimal change to fix the issue while keeping the same API for checking/unlocking techs.

    // Returns true if this node has been unlocked by the given player.
    public bool IsUnlockedBy(PlayerData player)
    {
        if (TechManager.Instance == null || player == null) return false;
        return TechManager.Instance.IsNodeUnlocked(player, this);
    }

    // Returns true if the given player meets all prerequisites and has not
    // yet unlocked this node.
    public bool CanUnlockFor(PlayerData player)
    {
        if (IsUnlockedBy(player)) return false;

        foreach (var preReq in preReqs)
        {
            if (preReq != null && !preReq.IsUnlockedBy(player)) return false;
        }
        return true;
    }

    // Marks this node as unlocked for the given player via TechManager.
    public void UnlockFor(PlayerData player)
    {
        if (TechManager.Instance == null || player == null) return;
        TechManager.Instance.MarkNodeUnlocked(player, this);
        Debug.Log($"Tech '{techName}' unlocked for {player.playerName}!");
    }

    // -----------------------------------------------------------------------
    //  Legacy shims — kept so that any code still calling the old parameterless
    //  API continues to compile.  They resolve the player from TurnManager so
    //  behaviour is identical to before, but now operates per-player.
    // -----------------------------------------------------------------------

    // Legacy: prefer IsUnlockedBy(player).
    public bool IsUnlocked
    {
        get
        {
            PlayerData p = TurnManager.Instance?.currentPlayer;
            return p != null && IsUnlockedBy(p);
        }
    }

    // Legacy: prefer CanUnlockFor(player).
    public bool CanUnlock()
    {
        PlayerData p = TurnManager.Instance?.currentPlayer;
        return p != null && CanUnlockFor(p);
    }

    // Legacy: prefer UnlockFor(player).
    public void UnlockTech()
    {
        PlayerData p = TurnManager.Instance?.currentPlayer;
        if (p != null) UnlockFor(p);
    }
}