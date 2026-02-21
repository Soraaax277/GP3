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
    
    [System.NonSerialized] 
    private bool _isUnlocked = false;

    public bool IsUnlocked => _isUnlocked;

    private void OnEnable()
    {
        _isUnlocked = false;
    }
    
    public bool CanUnlock()
    {
        if (_isUnlocked) return false;

        foreach (var preReq in preReqs)
        {
            if (preReq != null && !preReq.IsUnlocked) return false;
        }
        return true;
    }

    public void UnlockTech()
    {
        if (!_isUnlocked)
        {
            _isUnlocked = true; 
            Debug.Log($"Tech '{techName}' unlocked!");
        }
    }
}