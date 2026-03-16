using UnityEngine;
using System.Collections.Generic;

public class TechManager : MonoBehaviour
{
    public static TechManager Instance;

    // Per-player active (persistent) effects
    private Dictionary<PlayerData, List<TechEffect>> _playerActiveEffects
        = new Dictionary<PlayerData, List<TechEffect>>();

    private List<TechEffect> GetActiveEffectsFor(PlayerData player)
    {
        if (player == null) return new List<TechEffect>();
        if (!_playerActiveEffects.ContainsKey(player))
            _playerActiveEffects[player] = new List<TechEffect>();
        return _playerActiveEffects[player];
    }

    private List<TechEffect> activeEffects => GetActiveEffectsFor(TurnManager.Instance?.currentPlayer);

    // -----------------------------------------------------------------------
    //  PER-PLAYER UNLOCK STATE
    // -----------------------------------------------------------------------
    private Dictionary<PlayerData, HashSet<TechNode>> _playerUnlocks
        = new Dictionary<PlayerData, HashSet<TechNode>>();

    public bool IsNodeUnlocked(PlayerData player, TechNode node)
    {
        if (player == null || node == null) return false;
        return _playerUnlocks.TryGetValue(player, out var set) && set.Contains(node);
    }

    public void MarkNodeUnlocked(PlayerData player, TechNode node)
    {
        if (player == null || node == null) return;
        if (!_playerUnlocks.ContainsKey(player))
            _playerUnlocks[player] = new HashSet<TechNode>();
        _playerUnlocks[player].Add(node);
    }

    // -----------------------------------------------------------------------
    //  IN-PROGRESS RESEARCH QUEUE
    //  Key: PlayerData  |  Value: (TechNode → turns remaining)
    //  Cost is paid immediately on purchase. Effects activate when turns hit 0.
    // -----------------------------------------------------------------------
    private Dictionary<PlayerData, Dictionary<TechNode, int>> _activeResearch
        = new Dictionary<PlayerData, Dictionary<TechNode, int>>();

    /// Returns true if the player has paid for this node and it is ticking down.
    public bool IsResearching(PlayerData player, TechNode node)
    {
        if (player == null || node == null) return false;
        return _activeResearch.TryGetValue(player, out var dict) && dict.ContainsKey(node);
    }

    /// Returns the number of turns still remaining before the node completes.
    /// Returns 0 if the node is not currently being researched.
    public int GetResearchTurnsRemaining(PlayerData player, TechNode node)
    {
        if (player == null || node == null) return 0;
        if (_activeResearch.TryGetValue(player, out var dict) && dict.TryGetValue(node, out int turns))
            return turns;
        return 0;
    }

    /// Returns a snapshot of all nodes currently being researched by the player,
    /// mapped to their remaining turn count. Safe to iterate; it is a copy.
    public Dictionary<TechNode, int> GetActiveResearchFor(PlayerData player)
    {
        if (player == null) return new Dictionary<TechNode, int>();
        if (_activeResearch.TryGetValue(player, out var dict))
            return new Dictionary<TechNode, int>(dict);
        return new Dictionary<TechNode, int>();
    }

    /// Called once per player turn (from TurnManager.StartTurn via TickResearch hook).
    /// Decrements all in-progress research counters for that player and completes
    /// any that have reached zero.
    public void TickResearch(PlayerData player)
    {
        if (player == null) return;
        if (!_activeResearch.TryGetValue(player, out var dict) || dict.Count == 0) return;

        // Collect keys first to avoid modifying the dictionary while iterating.
        List<TechNode> nodes = new List<TechNode>(dict.Keys);
        List<TechNode> toComplete = new List<TechNode>();

        foreach (TechNode node in nodes)
        {
            dict[node]--;
            Debug.Log($"[TechManager] '{node.techName}' for {player.playerName}: {dict[node]} turns remaining.");
            if (dict[node] <= 0)
                toComplete.Add(node);
        }

        foreach (TechNode node in toComplete)
        {
            dict.Remove(node);
            CompleteResearch(player, node);
        }

        // Refresh the tech tree UI if it is currently open (human player only).
        if (!player.isAI && TechTreeWindowManager.Instance != null && TechTreeWindowManager.IsTechTreeOpen)
        {
            TechTreeWindowManager.Instance.RefreshAllTechButtons();
            TechTreeWindowManager.Instance.UpdateAllLines();
        }
    }

    // -----------------------------------------------------------------------
    //  PER-PLAYER MISC STATE
    // -----------------------------------------------------------------------
    private Dictionary<PlayerData, HashSet<string>> _playerUnlockedUnitNames
        = new Dictionary<PlayerData, HashSet<string>>();

    private Dictionary<PlayerData, HashSet<string>> _playerUnlockedFeatures
        = new Dictionary<PlayerData, HashSet<string>>();

    private Dictionary<PlayerData, int> _playerRPBonusPerTurn
        = new Dictionary<PlayerData, int>();

    private Dictionary<PlayerData, bool> _playerSabotageTabUnlocked
        = new Dictionary<PlayerData, bool>();

    public HashSet<string> unlockedUnitNames =>
        GetOrCreateSet(_playerUnlockedUnitNames, TurnManager.Instance?.currentPlayer);
    public HashSet<string> unlockedFeatures =>
        GetOrCreateSet(_playerUnlockedFeatures, TurnManager.Instance?.currentPlayer);

    public HashSet<string> GetUnlockedUnitNamesFor(PlayerData player) =>
        GetOrCreateSet(_playerUnlockedUnitNames, player);

    public HashSet<string> GetOrCreateFeatureSetFor(PlayerData player) =>
        GetOrCreateSet(_playerUnlockedFeatures, player);

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
        TechNode[] allNodes = Resources.FindObjectsOfTypeAll<TechNode>();
        
        foreach (string name in techNames)
        {
            foreach (TechNode node in allNodes)
            {
                if (node.techName == name)
                {
                    unlockedSet.Add(node);
                    break;
                }
            }
        }
        
        _playerUnlocks[player] = unlockedSet;
        RefreshPlayerTechStats(player);
    }

    /// Restores active (in-progress) research from a save file.
    /// techNames and turnsRemaining are parallel lists produced by SaveSystem.
    public void LoadActiveResearch(PlayerData player, List<string> techNames, List<int> turnsRemaining)
    {
        if (player == null || techNames == null || turnsRemaining == null) return;
        if (techNames.Count != turnsRemaining.Count)
        {
            Debug.LogWarning("[TechManager] LoadActiveResearch: list length mismatch, skipping.");
            return;
        }

        if (!_activeResearch.ContainsKey(player))
            _activeResearch[player] = new Dictionary<TechNode, int>();

        TechNode[] allNodes = Resources.FindObjectsOfTypeAll<TechNode>();

        for (int i = 0; i < techNames.Count; i++)
        {
            foreach (TechNode node in allNodes)
            {
                if (node.techName == techNames[i])
                {
                    _activeResearch[player][node] = turnsRemaining[i];
                    Debug.Log($"[TechManager] Restored in-progress research: '{node.techName}' " +
                              $"({turnsRemaining[i]} turns left) for {player.playerName}");
                    break;
                }
            }
        }
    }

    private void RefreshPlayerTechStats(PlayerData player)
    {
        GetOrCreateSet(_playerUnlockedUnitNames, player).Clear();
        GetOrCreateSet(_playerUnlockedFeatures, player).Clear();
        _playerRPBonusPerTurn[player] = 0;
        _playerSabotageTabUnlocked[player] = false;

        if (_playerUnlocks.TryGetValue(player, out var nodes))
        {
            foreach (var node in nodes)
            {
                _playerRPBonusPerTurn[player] += node.rpBonusPerTurn;
                
                if (node.unlocksSabotageTab) _playerSabotageTabUnlocked[player] = true;

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
    //  Valid Infrastructure Stat Names (unchanged — see original comments)
    // -----------------------------------------------------------------------

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

    // -----------------------------------------------------------------------
    //  RESEARCH — public entry point (called by UI and AI)
    //
    //  Flow:
    //    1. Validate prerequisites and affordability.
    //    2. Deduct costs immediately.
    //    3a. researchTurns <= 1  → CompleteResearch() right away (old behaviour).
    //    3b. researchTurns  > 1  → Queue in _activeResearch; effects fire later
    //                              when TickResearch() counts down to zero.
    // -----------------------------------------------------------------------
    public void ResearchTech(TechNode tech)
    {
        if (tech == null) return;
        
        PlayerData player = null;
        if (TurnManager.Instance != null)
            player = TurnManager.Instance.currentPlayer;
        else
        {
            Debug.LogWarning("TechManager: No TurnManager found, using Player 0 default.");
            if (GameManager.Instance != null) player = GameManager.Instance.players[0];
        }

        if (player == null) return;
        if (tech.IsUnlockedBy(player))
        {
            Debug.Log($"[TechManager] '{tech.techName}' is already unlocked for {player.playerName}.");
            return;
        }
        if (IsResearching(player, tech))
        {
            Debug.Log($"[TechManager] '{tech.techName}' is already in the research queue for {player.playerName}.");
            return;
        }

        // COST CHECKS
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
            Debug.Log($"[TechManager] Prerequisites not met for '{tech.techName}'!");
            return;
        }

        // PAY THE COST (always upfront, even for queued research)
        if (!freeResearchMode)
        {
            player.researchPoints -= tech.researchCost;
            player.resources      -= tech.goldCost;
        }
        else
        {
            Debug.LogWarning($"[TechManager] freeResearchMode ON — '{tech.techName}' researched at no cost.");
        }

        // INSTANT vs QUEUED
        // researchTurns == 0 → complete immediately (same turn as purchase).
        // researchTurns  > 0 → queue; TickResearch counts down each of the
        //                       player's turns and completes when it hits 0.
        if (tech.researchTurns <= 0)
        {
            // Instant — complete on the same turn as purchase.
            CompleteResearch(player, tech);
        }
        else
        {
            // Queue the research. Effects will fire in TickResearch().
            if (!_activeResearch.ContainsKey(player))
                _activeResearch[player] = new Dictionary<TechNode, int>();
            _activeResearch[player][tech] = tech.researchTurns;

            // QUEST HOOK: Begin Era 2 tech
            if (QuestManager.Instance != null && tech.eraRequirement == TurnManager.GameEra.EarlyEighties)
                QuestManager.Instance.SetQuestFlag(player, "ResearchEra2Tech");

            Debug.Log($"[TechManager] '{tech.techName}' queued: completes in {tech.researchTurns} " +
                      $"turn{(tech.researchTurns == 1 ? "" : "s")} for {player.playerName}. Cost paid.");

            // Refresh the UI so the node shows "IN RESEARCH" immediately.
            if (!player.isAI && TechTreeWindowManager.Instance != null)
            {
                TechTreeWindowManager.Instance.RefreshAllTechButtons();
                TechTreeWindowManager.Instance.UpdateAllLines();
            }
        }
    }

    // -----------------------------------------------------------------------
    //  COMPLETE RESEARCH — activates all effects for a finished tech.
    //  Called either instantly (researchTurns == 0) or by TickResearch().
    // -----------------------------------------------------------------------
    private void CompleteResearch(PlayerData player, TechNode tech)
    {
        if (player == null || tech == null) return;

        // Mark as unlocked in the per-player set.
        tech.UnlockFor(player);

        // QUEST HOOKS
        if (QuestManager.Instance != null)
        {
            // Main Quest 10: Company Transportation or Professional Services
            if (tech.techName.Contains("Transportation") || tech.techName.Contains("Logistics")) 
                QuestManager.Instance.SetQuestFlag(player, "UnlockedTransport");
            if (tech.techName.Contains("Service")) 
                QuestManager.Instance.SetQuestFlag(player, "UnlockedService");
            
            if (tech.techName.Contains("Workforce"))
                QuestManager.Instance.SetQuestFlag(player, "FinishedWorkforceTech");

            if (tech.techName.Contains("Artificial") || tech.techName.Contains(" AI"))
                QuestManager.Instance.SetQuestFlag(player, "ResearchAITech");

            if (tech.techName.Contains("Illicit") || tech.techName.Contains("Shadow"))
                QuestManager.Instance.SetQuestFlag(player, "UnlockedIllicitPractices");

            if (tech.techName.Contains("Grid Efficiency"))
                QuestManager.Instance.SetQuestFlag(player, "ResearchedGridEfficiency");

            if (tech.techName.Contains("Worker Wages") || tech.techName.Contains("Silicon Boom"))
                QuestManager.Instance.SetQuestFlag(player, "ResearchedSiliconBoom");

            if (tech.techName.Contains("Tower") && tech.techName.Contains("Range"))
            {
                // Check if any tower is now at max range
                foreach (var tower in TurnManager.Instance.GetAllTowers())
                {
                    if (tower.owner == player && tower.CurrentRange >= 3) // 3 is capped max
                    {
                        QuestManager.Instance.SetQuestFlag(player, "MaximizedTowerRadius");
                        break;
                    }
                }
                QuestManager.Instance.SetQuestFlag(player, "UpgradedTower");
            }
        }

        // ACTIVATE EFFECTS
        if (tech.unlockEffects != null)
        {
            foreach (var effect in tech.unlockEffects)
            {
                effect.ActivateEffect();

                switch (effect.type)
                {
                    case EffectType.UpgradeInfrastructure:
                    case EffectType.UnlockFeature:
                    case EffectType.UpgradePlayerEra:
                        // One-shot — already handled inside ActivateEffect().
                        break;

                    case EffectType.UpgradeUnitStat:
                    case EffectType.UnlockSkill:
                        GetActiveEffectsFor(player).Add(effect);
                        ApplyEffectToExistingUnits(effect, player);
                        break;

                    case EffectType.UnlockUnit:
                        GetActiveEffectsFor(player).Add(effect);
                        ApplyEffectToExistingUnits(effect, player);
                        if (effect.targetUnits != null)
                        {
                            var unitNames = GetOrCreateSet(_playerUnlockedUnitNames, player);
                            foreach (var unit in effect.targetUnits)
                                if (unit != null) unitNames.Add(unit.name);
                        }
                        break;
                }
            }
        }

        // Accumulate passive RP bonus.
        if (tech.rpBonusPerTurn > 0)
        {
            if (!_playerRPBonusPerTurn.ContainsKey(player)) _playerRPBonusPerTurn[player] = 0;
            _playerRPBonusPerTurn[player] += tech.rpBonusPerTurn;
            Debug.Log($"[TechManager] {player.playerName} passive RP bonus: +{_playerRPBonusPerTurn[player]}/turn");
        }

        // Sabotage tab unlock.
        if (tech.unlocksSabotageTab && !IsSabotageTabUnlockedFor(player))
        {
            _playerSabotageTabUnlocked[player] = true;
            Debug.Log($"[TechManager] Sabotage tab unlocked by '{tech.techName}' for {player.playerName}!");

            if (!player.isAI && TechTreeWindowManager.Instance != null)
                TechTreeWindowManager.Instance.RefreshSabotageButton();
        }

        // Refresh Build UI.
        if (BuildingUIManager.Instance != null && BuildingUIManager.Instance.panel.activeSelf)
        {
            SignalNode current = BuildingUIManager.Instance.GetCurrentBusiness();
            if (current != null)
                BuildingUIManager.Instance.Open(current);
        }

        // Refresh Tech Tree UI (handles both instant and delayed completions).
        if (!player.isAI && TechTreeWindowManager.Instance != null)
        {
            TechTreeWindowManager.Instance.RefreshAllTechButtons();
            TechTreeWindowManager.Instance.RefreshSabotageButton();
            TechTreeWindowManager.Instance.UpdateAllLines();
            TechTreeWindowManager.Instance.RefreshAllEraFog(instant: false);
        }

        Debug.Log($"[TechManager] '{tech.techName}' completed for {player.playerName}. " +
                  $"Remaining RP: {player.researchPoints}, Gold: {player.resources}");
    }

    // -----------------------------------------------------------------------
    //  DEBUG / FORCE UNLOCK (bypasses queue — instant, no cost)
    // -----------------------------------------------------------------------
    public void UnlockTechExplicitly(string techName)
    {
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

        // Remove from queue if it was in there, then complete immediately.
        if (_activeResearch.TryGetValue(player, out var dict))
            dict.Remove(tech);

        CompleteResearch(player, tech);
    }

    // -----------------------------------------------------------------------
    //  ERA UPGRADE METHODS  (System 1)
    // -----------------------------------------------------------------------
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

    // -----------------------------------------------------------------------
    //  FEATURE / INFRA LOGIC
    // -----------------------------------------------------------------------
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

    public float GetInfraMultiplier(PlayerData player, string statName) => GetInfraMultiplier(statName);

    public float GetInfraFlatBonus(string statName) =>
        infraFlatBonuses.ContainsKey(statName) ? infraFlatBonuses[statName] : 0f;

    public float GetInfraFlatBonus(PlayerData player, string statName) => GetInfraFlatBonus(statName);

    // -----------------------------------------------------------------------
    //  UNIT EFFECT APPLICATION
    // -----------------------------------------------------------------------
    private void ApplyEffectToExistingUnits(TechEffect effect, PlayerData player)
    {
        if (TurnManager.Instance == null) return;

        foreach (Unit unit in TurnManager.Instance.GetAllUnits())
        {
            if (unit.owner != player) continue;
            if (IsUnitTarget(unit, effect.targetUnits))
                ApplyStatToUnit(unit, effect);
        }
    }

    public void ApplyEffectsToNewUnit(Unit unit)
    {
        if (unit == null || unit.owner == null) return;
        foreach (var effect in GetActiveEffectsFor(unit.owner))
        {
            if (IsUnitTarget(unit, effect.targetUnits))
                ApplyStatToUnit(unit, effect);
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
                builder.UnlockConstruction();
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