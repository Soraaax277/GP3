using UnityEngine;
using System;
using System.Collections.Generic;

public enum EffectType
{
    UpgradeUnitStat,
    UnlockUnit,
    UnlockSkill,
    UpgradeInfrastructure, // Handles stats like Revenue, Durability, Range
    UnlockFeature,         // Handles mechanics like PowerBoxes, Tower Unlocks
    UpgradePlayerEra       // Advances the player's Hardware or Workforce Era by one step
}

[Serializable]
public class TechEffect
{
    public EffectType type;
    
    // Only used if type == UnlockUnit or UpgradeUnitStat or UnlockSkill
    public List<GameObject> targetUnits; 
    
    // Only used if type == UpgradeUnitStat
    public string statToUpgrade; 
    public float amount; 

    // Only used if type == UnlockSkill
    public string skillName;

    // Only used if type == UpgradeInfrastructure
    // -----------------------------------------------------------------------
    //  Valid infraStatName values:
    //
    //  ECONOMY / TOWERS
    //    "TowerRevenue"         – multiplier  – gold income per active tower
    //    "TowerRange"           – flat + mult – hex broadcast radius
    //    "TowerDurability"      – multiplier  – reduces incoming decay damage
    //    "MaxTowers"            – flat        – extra towers per HQ
    //
    //  WIRES
    //    "WireDurability"       – multiplier  – increases wire max HP
    //    "WireDegradation"      – flat        – reduces per-turn decay rate
    //
    //  SIGNAL NETWORK (System 2)
    //    "BaseSignalBoost"      – flat        – adds to HQ base signal output
    //    "SignalDecayReduction" – flat        – reduces per-hop signal loss
    //                                          (default 50%; 0.10 bonus → 40%)
    //
    //  HQ PLACEMENT
    //    "InfluenceRadius"      – flat        – expands tower placement radius
    // -----------------------------------------------------------------------
    public string infraStatName; 
    public float infraValueMod; 
    public bool isMultiplier;

    // Only used if type == UnlockFeature
    public string featureName;

    // -----------------------------------------------------------------------
    //  ERA UPGRADE  (only used if type == UpgradePlayerEra)
    // -----------------------------------------------------------------------
    [Tooltip(
        "TRUE  → Advance the player's HARDWARE Era (Industrial → EarlyEighties → Retro → Futuristic).\n" +
        "         Reduces the obsolete-tech influence debuff when the World Era is ahead.\n\n" +
        "FALSE → Advance the player's WORKFORCE Era (Industrial → EarlyEighties → Retro → Futuristic).\n" +
        "         Reduces the labor-mismatch upkeep penalty when Hardware is ahead of Workforce.\n\n" +
        "Each research of a TechNode with this effect advances the era by exactly one step.\n" +
        "To advance two steps, add two UpgradePlayerEra effects to the same TechNode.")]
    public bool isHardwareEra;

    // -----------------------------------------------------------------------
    //  ACTIVATE
    // -----------------------------------------------------------------------
    public void ActivateEffect()
    {
        switch (type)
        {
            case EffectType.UpgradeUnitStat:
                // Applied to existing units by TechManager.ResearchTech after this returns.
                break;
            
            case EffectType.UnlockUnit:
                // Unit name registration is handled by TechManager.ResearchTech.
                break;

            case EffectType.UnlockSkill:
                // Skill application to existing units is handled by TechManager.ResearchTech.
                break;

            case EffectType.UpgradeInfrastructure:
                if (TechManager.Instance != null)
                {
                    TechManager.Instance.ApplyInfrastructureUpgrade(infraStatName, infraValueMod, isMultiplier);
                    Debug.Log($"Applied Infrastructure Upgrade: {infraStatName} by {infraValueMod}");
                }
                else
                {
                    Debug.LogError("TechManager Instance not found!");
                }
                break;

            case EffectType.UnlockFeature:
                if (TechManager.Instance != null)
                {
                    TechManager.Instance.UnlockFeature(featureName);
                    Debug.Log($"Unlocked Feature: {featureName}");
                }
                else
                {
                    Debug.LogError("TechManager Instance not found!");
                }
                break;

            // ----------------------------------------------------------------
            //  ERA UPGRADE  (System 1)
            //  Gets the current player directly from TurnManager —
            //  works for both human and AI turns without extra parameters.
            // ----------------------------------------------------------------
            case EffectType.UpgradePlayerEra:
                if (TechManager.Instance == null)
                {
                    Debug.LogError("[TechEffect] UpgradePlayerEra: TechManager Instance not found!");
                    break;
                }

                PlayerData eraPlayer = null;
                if (TurnManager.Instance != null)
                    eraPlayer = TurnManager.Instance.currentPlayer;

                if (eraPlayer == null)
                {
                    Debug.LogError("[TechEffect] UpgradePlayerEra: Could not resolve current player from TurnManager.");
                    break;
                }

                if (isHardwareEra)
                    TechManager.Instance.UpgradeHardwareEra(eraPlayer);
                else
                    TechManager.Instance.UpgradeWorkforceEra(eraPlayer);
                break;

            default:
                Debug.LogWarning("Unknown effect type: " + type);
                break;
        }
    }
}