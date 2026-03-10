using UnityEngine;
using System.Collections.Generic;

public class TechManager : MonoBehaviour
{
    public static TechManager Instance;

    // Per-player active (persistent) effects — previously a single shared list,
    // which caused AI tech buffs to be applied to the human player's units.
    private Dictionary<PlayerData, List<TechEffect>> _playerActiveEffects
        = new Dictionary<PlayerData, List<TechEffect>>();

    private List<TechEffect> GetActiveEffectsFor(PlayerData player)
    {
        if (player == null) return new List<TechEffect>();
        if (!_playerActiveEffects.ContainsKey(player))
            _playerActiveEffects[player] = new List<TechEffect>();
        return _playerActiveEffects[player];
    }

    // Legacy property kept so any code referencing activeEffects still compiles.
    private List<TechEffect> activeEffects => GetActiveEffectsFor(TurnManager.Instance?.currentPlayer);

    // -----------------------------------------------------------------------
    //  PER-PLAYER UNLOCK STATE
    //  TechNode is a ScriptableObject — one shared asset instance for all
    //  players.  We store which nodes each player has unlocked here so that
    //  the AI researching a tech does not also unlock it for the human player.
    //  Key: PlayerData reference  |  Value: set of unlocked TechNode assets.
    // -----------------------------------------------------------------------
    private Dictionary<PlayerData, HashSet<TechNode>> _playerUnlocks
        = new Dictionary<PlayerData, HashSet<TechNode>>();

    /// Returns true if the given player has already unlocked this node.
    public bool IsNodeUnlocked(PlayerData player, TechNode node)
    {
        if (player == null || node == null) return false;
        return _playerUnlocks.TryGetValue(player, out var set) && set.Contains(node);
    }

    /// Records that the given player has unlocked this node.
    public void MarkNodeUnlocked(PlayerData player, TechNode node)
    {
        if (player == null || node == null) return;
        if (!_playerUnlocks.ContainsKey(player))
            _playerUnlocks[player] = new HashSet<TechNode>();
        _playerUnlocks[player].Add(node);
    }

    // Per-player unit unlock names (previously a single shared HashSet)
    private Dictionary<PlayerData, HashSet<string>> _playerUnlockedUnitNames
        = new Dictionary<PlayerData, HashSet<string>>();

    // Per-player feature unlocks (previously a single shared HashSet)
    private Dictionary<PlayerData, HashSet<string>> _playerUnlockedFeatures
        = new Dictionary<PlayerData, HashSet<string>>();

    // Per-player RP bonus (previously a single global int)
    private Dictionary<PlayerData, int> _playerRPBonusPerTurn
        = new Dictionary<PlayerData, int>();

    // Per-player sabotage tab unlock flag (previously a single global bool)
    private Dictionary<PlayerData, bool> _playerSabotageTabUnlocked
        = new Dictionary<PlayerData, bool>();

    // Legacy accessors — resolve to the current player so existing callers compile unchanged.
    // Where possible, prefer the explicit-player overloads below.
    public HashSet<string> unlockedUnitNames =>
        GetOrCreateSet(_playerUnlockedUnitNames, TurnManager.Instance?.currentPlayer);
    public HashSet<string> unlockedFeatures =>
        GetOrCreateSet(_playerUnlockedFeatures, TurnManager.Instance?.currentPlayer);

    public HashSet<string> GetUnlockedUnitNamesFor(PlayerData player) =>
        GetOrCreateSet(_playerUnlockedUnitNames, player);

    // Exposes the feature set for a specific player — used by DebugCheatManager
    // to force-unlock features by their exact string keys without going through TechNodes.
    public HashSet<string> GetOrCreateFeatureSetFor(PlayerData player) =>
        GetOrCreateSet(_playerUnlockedFeatures, player);

    // Explicit-player version of UnlockFeature — used by DebugCheatManager so
    // features are always written to the correct player regardless of whose turn it is.
    public void UnlockFeatureFor(PlayerData player, string featureName)
    {
        if (player == null || string.IsNullOrEmpty(featureName)) return;
        var set = GetOrCreateSet(_playerUnlockedFeatures, player);
        if (!set.Contains(featureName))
        {
            set.Add(featureName);
            Debug.Log($"Feature Unlocked: {featureName} for {player.playerName}");
        }
    }

    private HashSet<string> GetOrCreateSet(Dictionary<PlayerData, HashSet<string>> dict, PlayerData player)
    {
        if (player == null) return new HashSet<string>();
        if (!dict.ContainsKey(player)) dict[player] = new HashSet<string>();
        return dict[player];
    }

    public int GetTotalRPBonus() => GetTotalRPBonusFor(TurnManager.Instance?.currentPlayer);
    public int GetTotalRPBonusFor(PlayerData player)
    {
        if (player == null) return 0;
        return _playerRPBonusPerTurn.TryGetValue(player, out int v) ? v : 0;
    }

    public bool IsSabotageTabUnlocked() => IsSabotageTabUnlockedFor(TurnManager.Instance?.currentPlayer);
    public bool IsSabotageTabUnlockedFor(PlayerData player)
    {
        if (player == null) return false;
        return _playerSabotageTabUnlocked.TryGetValue(player, out bool v) && v;
    }

    public List<TechNode> GetUnlockedNodes(PlayerData player)
    {
        if (player == null) return new List<TechNode>();
        if (_playerUnlocks.TryGetValue(player, out var set))
            return new List<TechNode>(set);
        return new List<TechNode>();
    }

    public void LoadTechState(PlayerData player, List<string> techNames)
    {
        if (player == null || techNames == null) return;
        
        HashSet<TechNode> unlockedSet = new HashSet<TechNode>();
        
        // Find all tech nodes in the project to match names
        TechNode[] allNodes = Resources.FindObjectsOfTypeAll<TechNode>();
        
        foreach (string name in techNames)
        {
            foreach (TechNode node in allNodes)
            {
                if (node.techName == name)
                {
                    unlockedSet.Add(node);
                    // Also trigger their effects if they aren't already active?
                    // Actually, for infra/feature unlocks, we should re-apply them.
                    foreach (var effect in node.unlockEffects)
                    {
                        // We need a subtle way to re-apply without double-charging or side effects
                        // but most effects are idempotent (setting a bool, adding to a HashSet)
                        // infra multipliers might STACK though, so we must be careful.
                    }
                    break;
                }
            }
        }
        
        _playerUnlocks[player] = unlockedSet;
        
        // Refresh persistent state based on unlocked nodes
        RefreshPlayerTechStats(player);
    }

    private void RefreshPlayerTechStats(PlayerData player)
    {
        // Clear and rebuild player-specific tech flags (don't clear global infra stats here)
        GetOrCreateSet(_playerUnlockedUnitNames, player).Clear();
        GetOrCreateSet(_playerUnlockedFeatures, player).Clear();
        _playerRPBonusPerTurn[player] = 0;
        _playerSabotageTabUnlocked[player] = false;

        if (_playerUnlocks.TryGetValue(player, out var nodes))
        {
            foreach (var node in nodes)
            {
                // RP Bonus
                _playerRPBonusPerTurn[player] += node.rpBonusPerTurn;
                
                // Sabotage
                if (node.unlocksSabotageTab) _playerSabotageTabUnlocked[player] = true;

                // Effects
                foreach (var effect in node.unlockEffects)
                {
                    switch (effect.type)
                    {
                        case EffectType.UnlockFeature:
                            GetOrCreateSet(_playerUnlockedFeatures, player).Add(effect.featureName);
                            break;
                        case EffectType.UnlockUnit:
                            if (effect.targetUnits != null)
                            {
                                foreach (var u in effect.targetUnits) 
                                    GetOrCreateSet(_playerUnlockedUnitNames, player).Add(u.name);
                            }
                            break;
                    }
                }
            }
        }
    }

    public Dictionary<string, float> GetInfraMultipliers() => infraMultipliers;
    public Dictionary<string, float> GetInfraFlatStats() => infraFlatBonuses;

    public void LoadInfraStats(List<string> mKeys, List<float> mValues, List<string> fKeys, List<float> fValues)
    {
        infraMultipliers.Clear();
        for (int i = 0; i < mKeys.Count; i++) infraMultipliers[mKeys[i]] = mValues[i];

        infraFlatBonuses.Clear();
        for (int i = 0; i < fKeys.Count; i++) infraFlatBonuses[fKeys[i]] = fValues[i];
    }

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
        if (tech.IsUnlockedBy(player)) return;

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

        if (!tech.CanUnlockFor(player)) 
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
        tech.UnlockFor(player); 
        
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
                        GetActiveEffectsFor(player).Add(effect);
                        ApplyEffectToExistingUnits(effect, player);
                        break;

                    // UnlockUnit: register the unit name AND apply to existing units.
                    case EffectType.UnlockUnit:
                        GetActiveEffectsFor(player).Add(effect);
                        ApplyEffectToExistingUnits(effect, player);
                        if (effect.targetUnits != null)
                        {
                            var unitNames = GetOrCreateSet(_playerUnlockedUnitNames, player);
                            foreach (var unit in effect.targetUnits)
                            {
                                if (unit != null) unitNames.Add(unit.name);
                            }
                        }
                        break;
                }
            }
        }
        
        // Accumulate passive RP bonus from this node
        if (tech.rpBonusPerTurn > 0)
        {
            if (!_playerRPBonusPerTurn.ContainsKey(player)) _playerRPBonusPerTurn[player] = 0;
            _playerRPBonusPerTurn[player] += tech.rpBonusPerTurn;
            Debug.Log($"[TechManager] {player.playerName} passive RP bonus is now +{_playerRPBonusPerTurn[player]}/turn");
        }

        // Unlock Sabotage tab if this node is flagged for it
        if (tech.unlocksSabotageTab && !IsSabotageTabUnlockedFor(player))
        {
            _playerSabotageTabUnlocked[player] = true;
            Debug.Log($"[TechManager] Sabotage tab unlocked by '{tech.techName}' for {player.playerName}!");

            // Only refresh the UI button if this is the human player's unlock
            if (!player.isAI && TechTreeWindowManager.Instance != null)
                TechTreeWindowManager.Instance.RefreshSabotageButton();
        }

        // Refresh Build UI — reopen the current building panel so costs/availability update immediately
        if (BuildingUIManager.Instance != null && BuildingUIManager.Instance.panel.activeSelf)
        {
            SignalNode current = BuildingUIManager.Instance.GetCurrentBusiness();
            if (current != null)
                BuildingUIManager.Instance.Open(current);
        }
        
        Debug.Log($"Successfully researched {tech.techName}. " +
                  $"Remaining RP: {player.researchPoints}, Gold: {player.resources}");
    }

    public void UnlockTechExplicitly(string techName)
    {
         // Find node by name (Search in Resources or use an asset map)
         TechNode[] allNodes = Resources.FindObjectsOfTypeAll<TechNode>();
         foreach (var node in allNodes)
         {
             if (node.techName == techName || node.name == techName)
             {
                 ResearchTechForce(node);
                 return;
             }
         }
    }

    private void ResearchTechForce(TechNode tech)
    {
        PlayerData player = TurnManager.Instance != null ? TurnManager.Instance.currentPlayer : null;
        if (player == null || tech == null) return;
        
        // No cost deduction here
        tech.UnlockFor(player);
        
        if (tech.unlockEffects != null)
        {
            foreach (var effect in tech.unlockEffects)
            {
                effect.ActivateEffect();
                // Persist if needed (copied logic from ResearchTech)
                if (effect.type == EffectType.UpgradeUnitStat || effect.type == EffectType.UnlockSkill || effect.type == EffectType.UnlockUnit)
                {
                     GetActiveEffectsFor(player).Add(effect);
                     ApplyEffectToExistingUnits(effect, player);
                }
            }
        }
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
        PlayerData player = TurnManager.Instance?.currentPlayer;
        if (player == null) return;
        var set = GetOrCreateSet(_playerUnlockedFeatures, player);
        if (!set.Contains(featureName))
        {
            set.Add(featureName);
            Debug.Log($"Feature Unlocked: {featureName} for {player.playerName}");
        }
    }

    public bool IsFeatureUnlocked(string featureName)
    {
        PlayerData player = TurnManager.Instance?.currentPlayer;
        return player != null && GetOrCreateSet(_playerUnlockedFeatures, player).Contains(featureName);
    }

    public bool IsFeatureUnlockedFor(PlayerData player, string featureName)
    {
        return player != null && GetOrCreateSet(_playerUnlockedFeatures, player).Contains(featureName);
    }

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

    //  UNIT EFFECT APPLICATION  
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
        if (unit == null || unit.owner == null) return;
        foreach (var effect in GetActiveEffectsFor(unit.owner))
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