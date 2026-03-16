using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public List<QuestData> allQuests = new List<QuestData>();

    // state per player
    private Dictionary<int, List<QuestData>> activeQuests = new Dictionary<int, List<QuestData>>();
    private Dictionary<int, Dictionary<string, bool>> questCompletionStatus = new Dictionary<int, Dictionary<string, bool>>();
    public Dictionary<int, HashSet<string>> allCompletedQuests = new Dictionary<int, HashSet<string>>();
    public Dictionary<int, HashSet<string>> questFlags = new Dictionary<int, HashSet<string>>();
    private Dictionary<int, int> turnStartTileCount = new Dictionary<int, int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitQuests();
    }

    private void Start()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStarted += OnTurnStarted;

            // Catch-up for Turn 1 if we missed the event during loading
            if (TurnManager.Instance.currentTurn == 1 && TurnManager.Instance.players.Count > 0)
            {
                OnTurnStarted(TurnManager.Instance.players[0]);
            }
        }

        // Always refresh the UI after Start so the panel shows its initial state
        // (e.g. if StartGame fired OnTurnStarted before QuestManager subscribed)
        UpdateLocalPlayerUI();
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStarted -= OnTurnStarted;
        }
    }

    private void OnTurnStarted(PlayerData player)
    {
        int pid = player.playerId;
        EnsurePlayerRecords(pid);
        
        // Track tile count at start of turn for "Expand influence grid by +3"
        int currentTiles = 0;
        if (GridManager.Instance != null)
            currentTiles = GridManager.Instance.tiles.Values.Count(t => t.GetOwner() == player);
        turnStartTileCount[pid] = currentTiles;

        // Check for new quests or replacements this turn based on turn count or era
        int t = TurnManager.Instance.currentTurn;
        TurnManager.GameEra currentEra = TurnManager.Instance.currentEra;

        // 1. Evaluate completed quests from last turn/currently
        EvaluateQuests(player);

        // 2. Assign any new quests for this turn
        bool UIchanged = false;

        foreach (var q in allQuests)
        {
            if (q.startTurn == t)
            {
                // For Mini and Main, remove the previous one of the same tier if it exists
                if (q.tier != QuestTier.Major)
                {
                    activeQuests[pid].RemoveAll(existing => existing.tier == q.tier);
                }

                // Add new quest
                activeQuests[pid].Add(q);
                questCompletionStatus[pid][q.id] = false;

                if (!player.isAI && pid == TurnManager.Instance.players[0].playerId)
                    UIchanged = true;
            }
        }

        // Major quest assignment relies on Era (1 per era)
        // If era changes and major quest changed, replace.
        // I set up their startTurn = { 1, 26, 51, 76 } to align with era change turns.

        if (UIchanged)
        {
            UpdateLocalPlayerUI();
        }
    }

    private void EnsurePlayerRecords(int pid)
    {
        if (!activeQuests.ContainsKey(pid))
        {
            activeQuests[pid] = new List<QuestData>();
            questCompletionStatus[pid] = new Dictionary<string, bool>();
            allCompletedQuests[pid] = new HashSet<string>();
            questFlags[pid] = new HashSet<string>();
        }
    }

    public void SetQuestFlag(PlayerData player, string flag)
    {
        EnsurePlayerRecords(player.playerId);
        questFlags[player.playerId].Add(flag);
        EvaluateQuests(player);
    }

    // Call this if an action might have completed a quest during the turn
    public void NotifyAction(PlayerData player, string actionDesc)
    {
        SetQuestFlag(player, actionDesc);
    }

    private void EvaluateQuests(PlayerData player)
    {
        int pid = player.playerId;
        EnsurePlayerRecords(pid);

        bool updatedUI = false;

        foreach (QuestData q in new List<QuestData>(activeQuests[pid]))
        {
            if (!questCompletionStatus[pid][q.id])
            {
                if (CheckCondition(player, q))
                {
                    // COMPLETE
                    questCompletionStatus[pid][q.id] = true;
                    allCompletedQuests[pid].Add(q.id);

                    // Give Rewards
                    player.resources += q.goldReward;
                    player.researchPoints += q.rpReward;

                    if (!player.isAI)
                        updatedUI = true;
                }
            }
        }

        if (updatedUI && pid == TurnManager.Instance.players[0].playerId)
        {
            UpdateLocalPlayerUI();
        }
    }

    private bool CheckCondition(PlayerData player, QuestData quest)
    {
        int pid = player.playerId;
        HashSet<string> flags = questFlags.ContainsKey(pid) ? questFlags[pid] : new HashSet<string>();

        // We combine state checks with flag checks (from SetQuestFlag notifications)
        switch (quest.id)
        {
            // --- ERA 1: Industrial ---
            case "Era1_Mini2": return flags.Contains("LaidWire");
            case "Era1_Mini4": return flags.Contains("WorkerClearedTerrain");
            case "Era1_Mini6": return flags.Contains("ScoutEdgeVision");
            case "Era1_Mini8": return flags.Contains("ConnectedNeutralResource");
            case "Era1_Mini10": 
                int workerCount = TurnManager.Instance.GetAllUnits().Count(u => u != null && u.owner == player && u.gameObject.name.Contains("Worker"));
                return workerCount >= 2;
            case "Era1_Mini12": return flags.Contains("BuiltCanteen") || flags.Contains("BuiltWorkerFactory");
            case "Era1_Mini14": return flags.Contains("RevealedTwoHexes");
            case "Era1_Mini16": return true; // Handled per-turn implicitly if no negative income flags
            case "Era1_Mini18": return TurnManager.Instance.GetAllStructures().Count(s => s != null && s.owner == player) > 0 || TurnManager.Instance.GetAllTowers().Count(t => t != null && t.owner == player) > 0;
            case "Era1_Mini20": return flags.Contains("WireDifficultTerrain");
            case "Era1_Mini22": 
                 // No enemies within 2 hexes of ANY HQ
                 foreach (SignalNode hq in player.ownedNodes) {
                    if (hq == null || hq.ParentTile == null) continue;
                    var nearby = GridManager.Instance.GetTilesInRange(hq.ParentTile, 2);
                    foreach (var n in nearby) {
                        if (n.placedUnit != null && n.placedUnit.owner != player) return false;
                    }
                 }
                 return true;
            case "Era1_Mini24": return player.resources >= 50;

            case "Era1_Main5": 
                if (GridManager.Instance != null) {
                    return GridManager.Instance.GetAllTiles().Count(t => t.influenceByPlayer.ContainsKey(player) && t.influenceByPlayer[player] > 0) >= 5;
                }
                return false;
            case "Era1_Main10": return flags.Contains("UnlockedTransport") || flags.Contains("UnlockedService");
            case "Era1_Main15": return TurnManager.Instance.GetAllTowers().Count(t => t != null && t.owner == player && t.IsBuilt()) >= 1;
            case "Era1_Main20": 
                // Market Expansion: Border touches enemyAI
                foreach (var tilePair in GridManager.Instance.tiles) {
                    HexTile t = tilePair.Value;
                    if (t.GetOwner() == player) {
                        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(t)) {
                            PlayerData nOwner = neighbor.GetOwner();
                            if (nOwner != null && nOwner != player) return true;
                        }
                    }
                }
                return false;
            case "Era1_Main25": return player.resources >= 100;

            // MAJOR
            case "Era1_Major": 
                // Connect HQ to 3 Geysers
                int connectedGeysers = 0;
                if (HazardManager.Instance != null) {
                    foreach (var geyser in HazardManager.Instance.activeGeysers) {
                        if (geyser != null && geyser.currentTile != null) {
                            if (geyser.currentTile.placedWire != null && geyser.currentTile.placedWire.owner == player && geyser.currentTile.placedWire.IsPowered)
                                connectedGeysers++;
                        }
                    }
                }
                return connectedGeysers >= 3;

            // --- ERA 2: Early 80s ---
            case "Era2_Mini27": return flags.Contains("BuiltSignalJammer");
            case "Era2_Mini29": return flags.Contains("MarketerInNeutral");
            case "Era2_Mini31": return flags.Contains("ResearchEra2Tech");
            case "Era2_Mini33": return flags.Contains("BoostedSignalRange");
            case "Era2_Mini35": return flags.Contains("RecruitedSaboteur");
            case "Era2_Mini37": return flags.Contains("UnitNextToEnemyStructure");
            case "Era2_Mini39": return flags.Contains("FinishedWorkforceTech");
            case "Era2_Mini41": return flags.Contains("ScoutedEnemyHQ");
            case "Era2_Mini43": return flags.Contains("ClaimedChokepoint");
            case "Era2_Mini45": return flags.Contains("WorkerInExpansionPath");
            case "Era2_Mini47": return flags.Contains("UpgradedTower");
            case "Era2_Mini49": return flags.Contains("IntelOnThreeEnemies");

            case "Era2_Main30": return flags.Contains("JammerNearEnemy");
            case "Era2_Main35": return flags.Contains("FlippedTilesWithMarketing");
            case "Era2_Main40": return flags.Contains("SaboteurSurvivedTurn");
            case "Era2_Main45": return flags.Contains("ResearchedGridEfficiency");
            case "Era2_Main50": return flags.Contains("StrippedThreeOverlappingHexes");
            case "Era2_Major": return player.GetTotalInfluence() >= 75 && flags.Contains("SiphonedResource");

            // --- ERA 3: Retro ---
            case "Era3_Mini52": return flags.Contains("ConnectedDigitalNode");
            case "Era3_Mini54": return flags.Contains("DeployedSyntheticSurveillance");
            case "Era3_Mini56": return flags.Contains("RepairedStructure");
            case "Era3_Mini58": return flags.Contains("VisionOfHiddenEnemyGeyser");
            case "Era3_Mini60": return flags.Contains("IntimidationTactics");
            case "Era3_Mini62": 
                if (EconomyManager.Instance != null) return EconomyManager.Instance.CalculateNetGoldIncome(player) >= 25;
                return player.resources >= 25; // fallback
            case "Era3_Mini64": return flags.Contains("PlacedHighTierBuilding");
            case "Era3_Mini66": return flags.Contains("Grouped3CombatUnits");
            case "Era3_Mini68": return flags.Contains("InterceptedSaboteur");
            case "Era3_Mini70": 
                int startCount = turnStartTileCount.ContainsKey(pid) ? turnStartTileCount[pid] : 0;
                int currentCount = GridManager.Instance.tiles.Values.Count(t => t.GetOwner() == player);
                return (currentCount - startCount) >= 3;
            case "Era3_Mini72": return flags.Contains("UnlockedIllicitPractices");
            case "Era3_Mini74": 
                // Surrounded neighbor hex (exists an enemy hex where all neighbors are ours)
                foreach(var t in GridManager.Instance.tiles.Values) {
                    if (t.GetOwner() != null && t.GetOwner() != player) {
                        bool allNeighborsOurs = true;
                        foreach(var n in GridManager.Instance.GetNeighbors(t)) {
                            if (n.GetOwner() != player) { allNeighborsOurs = false; break; }
                        }
                        if (allNeighborsOurs) return true;
                    }
                }
                return false;

            case "Era3_Main55": return flags.Contains("Upgraded3WiresDigital");
            case "Era3_Main60": return flags.Contains("SneakedSaboteur");
            case "Era3_Main65": return flags.Contains("MaximizedTowerRadius");
            case "Era3_Main70": return flags.Contains("ResearchedSiliconBoom");
            case "Era3_Main75": return flags.Contains("DestroyedBuildingWithSaboteur");
            case "Era3_Major": return player.resources >= 500 && player.researchPoints >= 100;

            // --- ERA 4: Futuristic ---
            case "Era4_Mini77": return flags.Contains("DeployedCyberUnit");
            case "Era4_Mini79": return flags.Contains("FlippedEnemyStructureHex");
            case "Era4_Mini81": return flags.Contains("UnitsHealed");
            case "Era4_Mini83": return flags.Contains("MaxUnitLimitReached");
            case "Era4_Mini85": return flags.Contains("ResearchLategameSabotage");
            case "Era4_Mini87": return flags.Contains("Used3DPrinterTech");
            case "Era4_Mini89": return flags.Contains("CutOffEnemyUnit");
            case "Era4_Mini91": return player.researchPoints >= 50;
            case "Era4_Mini93": return flags.Contains("LockDownEnemyHQ");
            case "Era4_Mini95": return flags.Contains("FiveUpgradedTowers");
            case "Era4_Mini97": return flags.Contains("UntestedStimulants");
            case "Era4_Mini99": return flags.Contains("SurvivedTo100");

            case "Era4_Main80": return flags.Contains("NeuralPropaganda");
            case "Era4_Main85": return flags.Contains("ThreeMechanicalWorkers");
            case "Era4_Main90": return flags.Contains("MaxRangeSignalLink");
            case "Era4_Main95": return flags.Contains("DismantleAdvancedStructure");
            case "Era4_Main100": return flags.Contains("UltimateTechNode");
            case "Era4_Major": return player.resources >= 2000 && player.researchPoints >= 1000;
        }

        // AI simulated progression (~30% a turn for generic quest actions it doesn't fire events for)
        if (player.isAI && Random.value < 0.3f) 
            return true;
            
        return false;
    }

    public void UpdateLocalPlayerUI()
    {
        QuestPanelUI ui = FindFirstObjectByType<QuestPanelUI>();
        if (ui != null)
        {
            PlayerData p = TurnManager.Instance.players[0];
            int pid = p.playerId;
            EnsurePlayerRecords(pid);

            ui.RefreshQuestData(activeQuests[pid], questCompletionStatus[pid]);
        }
    }

    private void InitQuests()
    {
        // ERA 1: Industrial (Turns 2-25)
        allQuests.Add(new QuestData("Era1_Mini2", "Lay your first wire on an adjacent hex.", QuestTier.Mini, TurnManager.GameEra.Industrial, 2, 4, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini4", "Spend a Worker action to clear terrain.", QuestTier.Mini, TurnManager.GameEra.Industrial, 4, 6, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini6", "Move a Scout to the edge of your vision radius.", QuestTier.Mini, TurnManager.GameEra.Industrial, 6, 8, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini8", "Establish a connection to 1 neutral resource tile.", QuestTier.Mini, TurnManager.GameEra.Industrial, 8, 10, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini10", "Have 2 Workers active simultaneously.", QuestTier.Mini, TurnManager.GameEra.Industrial, 10, 12, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini12", "Construct a Canteen or Worker Factory.", QuestTier.Mini, TurnManager.GameEra.Industrial, 12, 14, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini14", "Reveal 2 new hexes of FoW.", QuestTier.Mini, TurnManager.GameEra.Industrial, 14, 16, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini16", "Maintain positive Gold income.", QuestTier.Mini, TurnManager.GameEra.Industrial, 16, 18, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini18", "Build a basic structural building.", QuestTier.Mini, TurnManager.GameEra.Industrial, 18, 20, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini20", "Route a wire through difficult terrain.", QuestTier.Mini, TurnManager.GameEra.Industrial, 20, 22, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini22", "No enemies within 2 hexes of HQ.", QuestTier.Mini, TurnManager.GameEra.Industrial, 22, 24, 10, 5));
        allQuests.Add(new QuestData("Era1_Mini24", "Stockpile at least 50 Gold.", QuestTier.Mini, TurnManager.GameEra.Industrial, 24, 26, 10, 5));

        allQuests.Add(new QuestData("Era1_Main5", "Copper Foundation: 5 connected hexes of influence.", QuestTier.Main, TurnManager.GameEra.Industrial, 5, 10, 50, 25));
        allQuests.Add(new QuestData("Era1_Main10", "Workforce Logistics: Unlock Company Transportation.", QuestTier.Main, TurnManager.GameEra.Industrial, 10, 15, 50, 25));
        allQuests.Add(new QuestData("Era1_Main15", "The First Broadcast: Construct first major Signal Tower.", QuestTier.Main, TurnManager.GameEra.Industrial, 15, 20, 50, 25));
        allQuests.Add(new QuestData("Era1_Main20", "Market Expansion: Touch the border of an opposing faction.", QuestTier.Main, TurnManager.GameEra.Industrial, 20, 25, 50, 25));
        allQuests.Add(new QuestData("Era1_Main25", "Industrial Dominance: Generate +10 RP per turn.", QuestTier.Main, TurnManager.GameEra.Industrial, 25, 30, 50, 25));

        allQuests.Add(new QuestData("Era1_Major", "The Transcontinental Line: Connect HQ to 3 specific points.", QuestTier.Major, TurnManager.GameEra.Industrial, 1, 26, 0, 100));

        // ERA 2: Early 80s (Turns 26-50)
        allQuests.Add(new QuestData("Era2_Mini27", "Build a Signal Jammer or defensive structure.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 27, 29, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini29", "Send a Sales Marketer into neutral territory.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 29, 31, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini31", "Begin researching Era 2 tech node.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 31, 33, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini33", "Successfully boost a signal range.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 33, 35, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini35", "Recruit a new Saboteur.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 35, 37, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini37", "Unit end turn next to enemy structure.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 37, 39, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini39", "Spend RP to complete Increased Workforce Size.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 39, 41, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini41", "Scout an enemy HQ outer defenses.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 41, 43, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini43", "Block enemy expansion by claiming choke-point.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 43, 45, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini45", "Station Worker in enemy path.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 45, 47, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini47", "Upgrade a previously built tower.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 47, 49, 10, 10));
        allQuests.Add(new QuestData("Era2_Mini49", "Gather intel on 3 enemy units simultaneously.", QuestTier.Mini, TurnManager.GameEra.EarlyEighties, 49, 51, 10, 10));

        allQuests.Add(new QuestData("Era2_Main30", "Radio Waves: Deploy Signal Jammer near enemy.", QuestTier.Main, TurnManager.GameEra.EarlyEighties, 30, 35, 60, 35));
        allQuests.Add(new QuestData("Era2_Main35", "Aggressive Advertising: Unlock Ad Campaign & flip 2 tiles.", QuestTier.Main, TurnManager.GameEra.EarlyEighties, 35, 40, 60, 35));
        allQuests.Add(new QuestData("Era2_Main40", "Corporate Espionage: Saboteur survives 1 turn in enemy area.", QuestTier.Main, TurnManager.GameEra.EarlyEighties, 40, 45, 60, 35));
        allQuests.Add(new QuestData("Era2_Main45", "Grid Efficiency: Reduce upkeep via Efficient Worker Deployment.", QuestTier.Main, TurnManager.GameEra.EarlyEighties, 45, 50, 60, 35));
        allQuests.Add(new QuestData("Era2_Main50", "Hostile Takeover: Strip influence from 3 overlapping enemy hexes.", QuestTier.Main, TurnManager.GameEra.EarlyEighties, 50, 55, 60, 35));

        allQuests.Add(new QuestData("Era2_Major", "Monopoly of the Airwaves: Hold 75 hexes & siphon 1 resource.", QuestTier.Major, TurnManager.GameEra.EarlyEighties, 26, 51, 0, 150));

        // ERA 3: Retro (Turns 51-75)
        allQuests.Add(new QuestData("Era3_Mini52", "Connect a new digital node to main network.", QuestTier.Mini, TurnManager.GameEra.Retro, 52, 54, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini54", "Deploy Synthetic Airborne Surveillance.", QuestTier.Mini, TurnManager.GameEra.Retro, 54, 56, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini56", "Repair a damaged structure.", QuestTier.Mini, TurnManager.GameEra.Retro, 56, 58, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini58", "Gain vision of hidden enemy Resource Geyser.", QuestTier.Mini, TurnManager.GameEra.Retro, 58, 60, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini60", "Saboteur applies Intimidation Tactics.", QuestTier.Mini, TurnManager.GameEra.Retro, 60, 62, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini62", "Generate at least +25 Gold this turn.", QuestTier.Mini, TurnManager.GameEra.Retro, 62, 64, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini64", "Place a high-tier building (HQ upgrade).", QuestTier.Mini, TurnManager.GameEra.Retro, 64, 66, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini66", "Group 3 combat/sabotage units together.", QuestTier.Mini, TurnManager.GameEra.Retro, 66, 68, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini68", "Intercept or reveal enemy Saboteur.", QuestTier.Mini, TurnManager.GameEra.Retro, 68, 70, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini70", "Expand influence grid by +3 tiles this turn.", QuestTier.Mini, TurnManager.GameEra.Retro, 70, 72, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini72", "Unlock Illicit Business Practices tech.", QuestTier.Mini, TurnManager.GameEra.Retro, 72, 74, 20, 15));
        allQuests.Add(new QuestData("Era3_Mini74", "Surround enemy hex with your influence.", QuestTier.Mini, TurnManager.GameEra.Retro, 74, 76, 20, 15));

        allQuests.Add(new QuestData("Era3_Main55", "Dial-up Infrastructure: Upgrade 3 Wires to digital.", QuestTier.Main, TurnManager.GameEra.Retro, 55, 60, 80, 50));
        allQuests.Add(new QuestData("Era3_Main60", "Equipment Smuggling: Sneak Saboteur past enemy borders.", QuestTier.Main, TurnManager.GameEra.Retro, 60, 65, 80, 50));
        allQuests.Add(new QuestData("Era3_Main65", "Market Saturation: Maximize tower influence radius.", QuestTier.Main, TurnManager.GameEra.Retro, 65, 70, 80, 50));
        allQuests.Add(new QuestData("Era3_Main70", "The Silicon Boom: Research Higher Worker Wages.", QuestTier.Main, TurnManager.GameEra.Retro, 70, 75, 80, 50));
        allQuests.Add(new QuestData("Era3_Main75", "Deadlier Methods: Destroy enemy building with Saboteur.", QuestTier.Main, TurnManager.GameEra.Retro, 75, 80, 80, 50));

        allQuests.Add(new QuestData("Era3_Major", "The Dot-Com Bubble: 500 Gold & 100 RP w/ 40% map control.", QuestTier.Major, TurnManager.GameEra.Retro, 51, 76, 0, 200));

        // ERA 4: Futuristic (Turns 76-100)
        allQuests.Add(new QuestData("Era4_Mini77", "Deploy Cybernetically Augmented unit.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 77, 79, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini79", "Flip enemy structure hex.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 79, 81, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini81", "Keep units fully healed for 1 turn.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 81, 83, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini83", "Reach maximum allowed unit limit.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 83, 85, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini85", "Research late-game Sabotage tech.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 85, 87, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini87", "Use 3D-Printer Tech to instant-spawn.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 87, 89, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini89", "Cut off enemy unit from supply grid.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 89, 91, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini91", "Generate +50 RP in a single turn.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 91, 93, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini93", "Lock down enemy HQ with Signal Jammer.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 93, 95, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini95", "Have 5 fully upgraded towers active.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 95, 97, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini97", "Apply Untested Stimulants buff to unit.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 97, 99, 30, 25));
        allQuests.Add(new QuestData("Era4_Mini99", "Survive to Turn 100 with HQ intact.", QuestTier.Mini, TurnManager.GameEra.Futuristic, 99, 101, 30, 25));

        allQuests.Add(new QuestData("Era4_Main80", "Neural Propaganda: Propagate massive influence.", QuestTier.Main, TurnManager.GameEra.Futuristic, 80, 85, 100, 75));
        allQuests.Add(new QuestData("Era4_Main85", "Mechanical Workforce: Output mechanical units.", QuestTier.Main, TurnManager.GameEra.Futuristic, 85, 90, 100, 75));
        allQuests.Add(new QuestData("Era4_Main90", "Space-Age Telecomm: Establish max-range signal link.", QuestTier.Main, TurnManager.GameEra.Futuristic, 90, 95, 100, 75));
        allQuests.Add(new QuestData("Era4_Main95", "Lethal Subterfuge: Dismantle advanced enemy structure.", QuestTier.Main, TurnManager.GameEra.Futuristic, 95, 100, 100, 75));
        allQuests.Add(new QuestData("Era4_Main100", "The Final Node: Research ultimate Tech tree node.", QuestTier.Main, TurnManager.GameEra.Futuristic, 100, 105, 100, 75));

        allQuests.Add(new QuestData("Era4_Major", "The Singularity Protocol: Level 3 Network, 2000 Gold, 1000 RP.", QuestTier.Major, TurnManager.GameEra.Futuristic, 76, 120, 0, 500));
    }
}
