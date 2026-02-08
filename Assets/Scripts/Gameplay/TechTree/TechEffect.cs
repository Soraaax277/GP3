using UnityEngine;
using System;
using System.Collections.Generic;

public enum EffectType
{
    UpgradeUnitStat,
    UnlockUnit,
    UnlockSkill
}

[Serializable]
public class TechEffect
{
    [Header("General Settings")]
    public EffectType type;
    public List<GameObject> targetUnits;
    
    [Header("IF Stat Upgrades")]
    public string statToUpgrade;
    public float amount; 

    [Header("IF SkillUnlock")]
    public string skillName;
    
    public void ActivateEffect()
    {
        switch (type)
        {
            case EffectType.UpgradeUnitStat:
            {
                break;   
            }

            case EffectType.UnlockUnit:
            {
                break;   
            }
            
            case EffectType.UnlockSkill:
            {
                break;   
            }
            default:
            {
                Debug.LogWarning("Unknown effect type: " + type);
                break;
            }
        }
    }
}