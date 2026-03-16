using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Tech", menuName = "Tech Tree/Tech Node")]
public class TechNode : ScriptableObject
{
    [Header("Info")] 
    public string techName; 
    [TextArea] public string description;
    public TurnManager.GameEra eraRequirement = TurnManager.GameEra.Industrial;
    
    [Header("Costs")]
    public int researchCost; // Deducted from Research Points
    public int goldCost;     // Deducted from Gold (Resources)

    [Header("Research Duration")]
    [Tooltip("How many turns after purchase before this tech is fully integrated. " +
             "0 = instant, unlocks the same turn it is purchased (default). " +
             "1 = unlocks on the player's next turn. " +
             "2 = unlocks after 2 turns, and so on. " +
             "Costs are always deducted immediately on purchase.")]
    public int researchTurns;

    [Header("Tab Requirements")]
    [Tooltip("When TRUE, researching this node unlocks the Sabotage tab. Multiple nodes can have this set — the tab unlocks as soon as ANY one of them is researched.")]
    public bool unlocksSabotageTab = false;

    [Header("Passive Research Bonus")]
    [Tooltip("Flat Research Points added to the player's per-turn RP income when this tech is unlocked. Stacks additively. Leave at 0 for no bonus.")]
    public int rpBonusPerTurn = 0;

    [Header("Era Fog Control")]
    [Tooltip("TRUE if this is the column 1 node of an era. " +
             "Researching it fades out the partial fog covering columns 2-5 of its era.")]
    public bool isEraGateNode = false;

    [Tooltip("TRUE if this is one of the final nodes of an era. " +
             "When ALL required transition nodes are unlocked, the NEXT era's full fog " +
             "fades out and its partial fog fades in, revealing column 1.")]
    public bool isEraTransitionNode = false;

    [Header("Prerequisite Tech")]
    public List<TechNode> preReqs;

    [Header("Tech Effects")] 
    public List<TechEffect> unlockEffects;

    // -----------------------------------------------------------------------
    //  UNLOCK STATE — stored per-player in TechManager, NOT on this asset.
    // -----------------------------------------------------------------------

    // Returns true if this node has been unlocked by the given player.
    public bool IsUnlockedBy(PlayerData player)
    {
        if (TechManager.Instance == null || player == null) return false;
        return TechManager.Instance.IsNodeUnlocked(player, this);
    }

    // Returns true if the given player is currently in the process of
    // researching this node (cost paid, turns ticking down, not yet complete).
    public bool IsResearchingBy(PlayerData player)
    {
        if (TechManager.Instance == null || player == null) return false;
        return TechManager.Instance.IsResearching(player, this);
    }

    // Returns true if the given player meets all prerequisites, is not
    // already researching this node, and has not yet unlocked it.
    public bool CanUnlockFor(PlayerData player)
    {
        if (IsUnlockedBy(player))   return false;
        if (IsResearchingBy(player)) return false; // already queued — prevent double-purchase

        foreach (var preReq in preReqs)
        {
            if (preReq != null && !preReq.IsUnlockedBy(player)) return false;
        }
        return true;
    }

    // Marks this node as unlocked for the given player via TechManager.
    // Called by TechManager.CompleteResearch — do not call directly for
    // queued techs, as effects are handled there too.
    public void UnlockFor(PlayerData player)
    {
        if (TechManager.Instance == null || player == null) return;
        TechManager.Instance.MarkNodeUnlocked(player, this);
        Debug.Log($"Tech '{techName}' unlocked for {player.playerName}!");
    }

    // -----------------------------------------------------------------------
    //  Legacy shims — kept so that any code still calling the old parameterless
    //  API continues to compile.
    // -----------------------------------------------------------------------

    public bool IsUnlocked
    {
        get
        {
            PlayerData p = TurnManager.Instance?.currentPlayer;
            return p != null && IsUnlockedBy(p);
        }
    }

    public bool CanUnlock()
    {
        PlayerData p = TurnManager.Instance?.currentPlayer;
        return p != null && CanUnlockFor(p);
    }

    public void UnlockTech()
    {
        PlayerData p = TurnManager.Instance?.currentPlayer;
        if (p != null) UnlockFor(p);
    }
}