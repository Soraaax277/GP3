using UnityEngine;
using System.Collections.Generic;

public class TechManager : MonoBehaviour
{
    public static TechManager Instance;

    private List<TechEffect> activeEffects = new List<TechEffect>();
    public HashSet<string> unlockedUnitNames  = new HashSet<string>();
    public HashSet<string> unlockedFeatures   = new HashSet<string>();

    // Accumulated flat RP bonus from all unlocked TechNodes that have rpBonusPerTurn > 0.
    // Read by EconomyManager.ProcessTurnIncome() each turn.
    private int _totalRPBonusPerTurn = 0;
    public int GetTotalRPBonus() => _totalRPBonusPerTurn;

    // Set to true permanently once any TechNode with unlocksSabotageTab=true is researched.
    private bool _sabotageTabUnlocked = false;

    // Returns true if the Sabotage tech tree tab has been unlocked by researching
    // at least one TechNode that has unlocksSabotageTab = true.
    // Read by TechTreeWindowManager to enable/disable btnSabotage.
    public bool IsSabotageTabUnlocked() => _sabotageTabUnlocked;

    private Dictionary<string, float> infraMultipliers  = new Dictionary<string, float>();
    private Dictionary<string, float> infraFlatBonuses  = new Dictionary<string, float>();

    // -----------------------------------------------------------------------
    //  Valid Infrastructure Stat Names (for ApplyInfrastructureUpgrade)
    // -----------------------------------------------------------------------
    // ECONOMY / TOWERS
    //   "TowerRevenue"         – multiplier  – gold income per active tower
    //   "TowerRange"           – flat + mult – hex broadcast radius
    //   "TowerDurability"      – multiplier  – reduces incoming decay damage
    //   "TowerCapacity"        – flat        – extra towers per HQ
    //
    // WIRES
    //   "WireDurability"       – multiplier  – increases wire max HP
    //   "WireDegradation"      – flat        – reduces per-turn decay rate
    //   "WireLength"           – flat        – max hex distance for wire placement
    //   "WireCost"             – multiplier  – gold cost per wire tile (negative = cheaper)
    //
    // SIGNAL NETWORK (System 2)
    //   "BaseSignalBoost"      – flat        – adds to HQ base signal output
    //   "SignalDecayReduction" – flat        – reduces per-hop signal loss
    //                                         (default 50%; 0.10 bonus → 40%)
    //
    // HQ PLACEMENT
    //   "InfluenceRadius"      – flat        – expands tower placement radius
    //
    // WORKFORCE UPGRADES (System 4)
    //   "MaintenanceCost"      – multiplier  – upkeep cost for all infrastructure/units (negative = cheaper)
    //   "RecruitmentCost"      – multiplier  – cost to spawn units (negative = cheaper)
    //   "BuildingCost"         – multiplier  – cost to build towers (negative = cheaper)
    //   "RepairCost"           – multiplier  – cost to repair structures (negative = cheaper)
    //   "StructureDurability"  – multiplier  – durability for towers and wires
    //   "RepairEfficiency"     – multiplier  – HP restored when repairing (applied to Technician units)
    //   "StructureDegradation" – multiplier  – decay rate for towers (negative = slower decay)
    //

    // ERA UPGRADES (System 1)
    //   Not stored in dictionaries. Use EffectType.UpgradePlayerEra on a TechEffect,
    //   or call UpgradeHardwareEra() / UpgradeWorkforceEra() directly.
    // -----------------------------------------------------------------------

    //  DEBUG / TESTING
    [Header("Debug")]
    [Tooltip("DEBUG: When enabled, all tech nodes can be researched for free. Does NOT modify ScriptableObject data. Disable before shipping!")]
    public bool freeResearchMode = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    //  RESEARCH
    public void ResearchTech(TechNode tech)
    {
        if (tech == null) return;
        
        // Get the actual current player (works for AI too)
        PlayerData player = null;
        if (TurnManager.Instance != null)
        {
            player = TurnManager.Instance.currentPlayer;
        }
        else
        {
            Debug.LogWarning("TechManager: No TurnManager found, using Player 0 default.");
            if (GameManager.Instance != null) player = GameManager.Instance.players[0];
        }

        if (player == null) return;
        if (tech.IsUnlocked) return;

        // COST CHECKS
        // Skipped entirely when freeResearchMode is on. ScriptableObject data is untouched.
        if (!freeResearchMode)
        {
            if (player.researchPoints < tech.researchCost) 
            {
                Debug.Log($"Cannot Research {tech.techName}: Not enough RP! " +
                          $"(Have: {player.researchPoints}, Need: {tech.researchCost})");
                return;
            }

            if (player.resources < tech.goldCost)
            {
                Debug.Log($"Cannot Research {tech.techName}: Not enough Gold! " +
                          $"(Have: {player.resources}, Need: {tech.goldCost})");
                return;
            }
        }

        if (!tech.CanUnlock()) 
        {
            Debug.Log("Prerequisites not met!");
            return;
        }

        // PAY THE COST
        // Not deducted in freeResearchMode — player resources are left unchanged.
        if (!freeResearchMode)
        {
            player.researchPoints -= tech.researchCost;
            player.resources      -= tech.goldCost;
        }
        else
        {
            Debug.LogWarning($"[TechManager] freeResearchMode ON — '{tech.techName}' researched at no cost.");
        }

        // UNLOCK
        tech.UnlockTech(); 
        
        if (GameStatusUI.Instance != null)
        {
            // GameStatusUI.Instance.UpdateUI();
        }

        // ACTIVATE EFFECTS
        if (tech.unlockEffects != null)
        {
            foreach (var effect in tech.unlockEffects)
            {
                // ActivateEffect() dispatches internally to TechManager / TurnManager
                effect.ActivateEffect();

                // Determine persistence — only unit-targeting effects need to be
                // remembered so they can be re-applied to units spawned later.
                switch (effect.type)
                {
                    // One-shot effects: fire and forget, do NOT track in activeEffects.
                    case EffectType.UpgradeInfrastructure:
                    case EffectType.UnlockFeature:
                    case EffectType.UpgradePlayerEra:
                        // Already handled inside ActivateEffect(). Nothing else to do.
                        break;

                    // Persistent unit-stat effects: apply to existing units now,
                    // and remember for units spawned later.
                    case EffectType.UpgradeUnitStat:
                    case EffectType.UnlockSkill:
                        activeEffects.Add(effect);
                        ApplyEffectToExistingUnits(effect, player);
                        break;

                    // UnlockUnit: register the unit name AND apply to existing units.
                    case EffectType.UnlockUnit:
                        activeEffects.Add(effect);
                        ApplyEffectToExistingUnits(effect, player);
                        if (effect.targetUnits != null)
                        {
                            foreach (var unit in effect.targetUnits)
                            {
                                if (unit != null) unlockedUnitNames.Add(unit.name);
                            }
                        }
                        break;
                }
            }
        }
        
        // Accumulate passive RP bonus from this node
        if (tech.rpBonusPerTurn > 0)
        {
            _totalRPBonusPerTurn += tech.rpBonusPerTurn;
            Debug.Log($"[TechManager] {player.playerName} passive RP bonus is now +{_totalRPBonusPerTurn}/turn");
        }

        // Unlock Sabotage tab if this node is flagged for it
        if (tech.unlocksSabotageTab && !_sabotageTabUnlocked)
        {
            _sabotageTabUnlocked = true;
            Debug.Log($"[TechManager] Sabotage tab unlocked by '{tech.techName}'!");

            // Notify TechTreeWindowManager to refresh button states immediately
            if (TechTreeWindowManager.Instance != null)
                TechTreeWindowManager.Instance.RefreshSabotageButton();
        }

        // Refresh Build UI
        if (BuildUIManager.Instance != null && BuildUIManager.Instance.buildPanel.activeSelf)
        {
             BuildUIManager.Instance.UpdateBuildButtons();
        }
        
        Debug.Log($"Successfully researched {tech.techName}. " +
                  $"Remaining RP: {player.researchPoints}, Gold: {player.resources}");
    }

    // -----------------------------------------------------------------------
    //  ERA UPGRADE METHODS  (System 1)
    //  Called by TechEffect.ActivateEffect() when type == UpgradePlayerEra,
    //  or can be called directly from other systems.
    // -----------------------------------------------------------------------

    // Advances the player's Hardware Era by one step (up to Futuristic).
    // Reduces the obsolete-tech influence penalty when the World Era is ahead.
    // Triggered in the Inspector via: EffectType = UpgradePlayerEra, isHardwareEra = TRUE.
    public void UpgradeHardwareEra(PlayerData player)
    {
        if (player == null) return;

        int nextIndex = (int)player.hardwareEra + 1;
        int maxIndex  = (int)TurnManager.PlayerEra.Futuristic;

        if (nextIndex > maxIndex)
        {
            Debug.Log($"[Tech] {player.playerName} Hardware Era is already at maximum (Futuristic).");
            return;
        }

        player.hardwareEra = (TurnManager.PlayerEra)nextIndex;
        Debug.Log($"[Tech] {player.playerName} Hardware Era upgraded to: {player.hardwareEra}");

        if (TurnManager.Instance != null)
            TurnManager.Instance.NotifyStatusChanged();
    }

    // Advances the player's Workforce Era by one step (up to Futuristic).
    // Reduces the labor-mismatch upkeep penalty when Hardware is ahead of Workforce.
    // Triggered in the Inspector via: EffectType = UpgradePlayerEra, isHardwareEra = FALSE.
    public void UpgradeWorkforceEra(PlayerData player)
    {
        if (player == null) return;

        int nextIndex = (int)player.workforceEra + 1;
        int maxIndex  = (int)TurnManager.PlayerEra.Futuristic;

        if (nextIndex > maxIndex)
        {
            Debug.Log($"[Tech] {player.playerName} Workforce Era is already at maximum (Futuristic).");
            return;
        }

        player.workforceEra = (TurnManager.PlayerEra)nextIndex;
        Debug.Log($"[Tech] {player.playerName} Workforce Era upgraded to: {player.workforceEra}");

        if (TurnManager.Instance != null)
            TurnManager.Instance.NotifyStatusChanged();
    }

    //  FEATURE / INFRA LOGIC 
    public void UnlockFeature(string featureName)
    {
        if (!unlockedFeatures.Contains(featureName))
        {
            unlockedFeatures.Add(featureName);
            Debug.Log($"Feature Unlocked: {featureName}");
        }
    }

    public bool IsFeatureUnlocked(string featureName) => unlockedFeatures.Contains(featureName);

    public void ApplyInfrastructureUpgrade(string statName, float value, bool isMultiplier)
    {
        if (isMultiplier)
        {
            if (!infraMultipliers.ContainsKey(statName)) infraMultipliers[statName] = 1.0f;
            infraMultipliers[statName] += value;
        }
        else
        {
            if (!infraFlatBonuses.ContainsKey(statName)) infraFlatBonuses[statName] = 0f;
            infraFlatBonuses[statName] += value;
        }
        Debug.Log($"Infrastructure Upgrade Applied: {statName}");
    }

    public float GetInfraMultiplier(string statName) =>
        infraMultipliers.ContainsKey(statName) ? infraMultipliers[statName] : 1.0f;

    public float GetInfraFlatBonus(string statName) =>
        infraFlatBonuses.ContainsKey(statName) ? infraFlatBonuses[statName] : 0f;

    //  UNIT EFFECT APPLICATION  (unchanged)
    private void ApplyEffectToExistingUnits(TechEffect effect, PlayerData player)
    {
        if (TurnManager.Instance == null) return;

        foreach (Unit unit in TurnManager.Instance.GetAllUnits())
        {
            if (unit.owner != player) continue;
            
            if (IsUnitTarget(unit, effect.targetUnits))
            {
                ApplyStatToUnit(unit, effect);
            }
        }
    }

    public void ApplyEffectsToNewUnit(Unit unit)
    {
        foreach (var effect in activeEffects)
        {
            if (IsUnitTarget(unit, effect.targetUnits))
            {
                ApplyStatToUnit(unit, effect);
            }
        }
    }

    private void ApplyStatToUnit(Unit unit, TechEffect effect)
    {
        if (effect.type == EffectType.UpgradeUnitStat)
        {
            unit.ReceiveStatUpgrade(effect.statToUpgrade, effect.amount);
        }
        else if (effect.type == EffectType.UnlockSkill)
        {
            if (unit is BuilderUnit builder && effect.skillName == "ConstructTower")
            {
                builder.UnlockConstruction();
            }
        }
    }

    private bool IsUnitTarget(Unit unit, List<GameObject> targets)
    {
        if (targets == null) return false;
        foreach (var target in targets)
        {
            if (target != null && unit.GetType() == target.GetComponent<Unit>().GetType())
                return true;
        }
        return false;
    }
}