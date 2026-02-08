using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Tech", menuName = "Tech Tree/Tech Node")]
public class TechNode : ScriptableObject
{
    [Header("Info")] 
    public string techName;
    public string description;
    
    [Header("Cost and Bonus")]
    public int researchCost;
    public int researchBonus; //Research Gain per Turn
    
    [Header("Prerequisite Tech")]
    public List<TechNode> preReqs;

    [Header("Tech Effects")] 
    public List<TechEffect> unlockEffects;
    
    private bool isUnlocked;
    
    public bool Unlockable()
    {
        foreach (var preReq in preReqs)
        {
            if (!preReq.isUnlocked) return false;
        }
        return true;
    }

    public void UnlockTech()
    {
        if (!isUnlocked)
        {
            foreach (var effect in unlockEffects)
            {
                effect.ActivateEffect();
            }
            isUnlocked = true;
            Debug.Log("Tech \"" + techName + "\" unlocked");
        }
    }
}
