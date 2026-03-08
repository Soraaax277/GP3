using UnityEngine;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    //  WORLD ERA  (advances every 25 turns, shared by all players)
    public enum GameEra { Industrial, EarlyEighties, Retro, Futuristic }
    public GameEra currentEra { get; private set; }

    //  PLAYER ERA  (per-player tech levels — stored on PlayerData)
    public enum PlayerEra { Industrial, EarlyEighties, Retro, Futuristic }

    public int currentTurn { get; set; } = 1;
    public const int MAX_TURNS = 100;

    public PlayerData currentPlayer { get; private set; }

    public List<PlayerData> players = new List<PlayerData>();
    public int currentPlayerIndex { get; private set; }

    private List<Unit>       allUnits  = new List<Unit>();
    private List<TowerNode>     allTowers     = new List<TowerNode>();
    private List<WireNode>      allWires      = new List<WireNode>();
    private List<StructureNode> allStructures = new List<StructureNode>();

    public event System.Action OnGameStatusChanged;

    public void NotifyStatusChanged()
    {
        OnGameStatusChanged?.Invoke();
    }

    //  LIFECYCLE
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // GameManager handled LoadGame logic in its SetupGame coroutine
    }

    public void StartGame(List<PlayerData> playerList)
    {
        players = playerList;
        currentTurn = 1;
        currentEra = GameEra.Industrial;
        currentPlayerIndex = 0;

        StartTurn();
    }

    //  TURN FLOW
    void StartTurn()
    {
        currentPlayer = players[currentPlayerIndex];

        // CLEANUP UI
        // Ensure no old menus are stuck open from the previous player
        if (BuildingUIManager.Instance != null) 
            BuildingUIManager.Instance.Close();

        // SIGNAL PROPAGATION
        // Propagate signal from every player's HQs before influence is recalculated,
        // so that towers have up-to-date receivedSignalStrength values.
        foreach (PlayerData p in players)
        {
            foreach (SignalNode node in p.ownedNodes)
            {
                if (node != null)
                    node.PropagateSignal();
            }
        }

        // CALCULATE GLOBAL STATE 
        if (InfluenceManager.Instance != null)
        {
            // DECAY PERSISTENT SUPPRESSION
            if (GridManager.Instance != null)
            {
                foreach (var tile in GridManager.Instance.GetAllTiles())
                {
                    if (tile.influenceSuppression > 0)
                    {
                        tile.influenceSuppression = Mathf.Max(0, tile.influenceSuppression - 2);
                    }
                }
            }

            InfluenceManager.Instance.RecalculateGlobalInfluence(players);
        }
        else
        {
            Debug.LogError("TurnManager: Missing InfluenceManager!");
        }

        // PROCESS INCOME
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.ProcessTurnIncome(currentPlayer);
        }
        else
        {
            Debug.LogError("TurnManager: Missing EconomyManager!");
        }

        // --- PHASE 3: RESEARCH PROCESSING ---
        if (ResearchProjectHandler.Instance != null)
        {
            ResearchProjectHandler.Instance.OnTurnEnd(currentPlayer);
        }

        // --- PHASE 1: GLOBAL EVENTS & HAZARDS ---
        if (EventManager.Instance != null)
            EventManager.Instance.ProcessTurnEvents();
            
        if (currentPlayerIndex == 0 && HazardManager.Instance != null)
            HazardManager.Instance.ProcessTurnHazards();
        // ----------------------------------------
        
        // UPDATE GAME WORLD 
        OnGameStatusChanged?.Invoke();

        // Iterate a copy so destroyed units can be safely removed mid-loop
        for (int i = allUnits.Count - 1; i >= 0; i--)
        {
            if (allUnits[i] == null) { allUnits.RemoveAt(i); continue; }
            allUnits[i].OnTurnStart(currentPlayer);
        }

        // TOWER DECAY
        foreach (TowerNode tower in allTowers)
        {
            if (tower.owner == currentPlayer) 
            {
                tower.ProcessTurnDecay();
            }
        }

        // WIRE DECAY
        for (int i = allWires.Count - 1; i >= 0; i--)
        {
            WireNode wire = allWires[i];
            
            if (wire == null) 
            {
                allWires.RemoveAt(i);
                continue;
            }

            if (wire.owner == currentPlayer)
            {
                wire.DecayWire();
            }
        }

        // STRUCTURE TURN START
        for (int i = allStructures.Count - 1; i >= 0; i--)
        {
            if (allStructures[i] == null) { allStructures.RemoveAt(i); continue; }
            if (allStructures[i].owner == currentPlayer)
            {
                allStructures[i].OnTurnStart();
            }
        }

        // UPDATE FOG OF WAR (Always for the human player)
        if (FieldOfViewManager.Instance != null)
        {
            FieldOfViewManager.Instance.UpdateFogOfWar(players[0]);
        }

        // CAMERA TRACKING
        HandleCameraFocus(currentPlayer);

        // AI EXECUTION
        if (currentPlayer.isAI && EnemyAI.Instance != null)
        {
            EnemyAI.Instance.ExecuteTurn(currentPlayer);
        }
    }

    private void HandleCameraFocus(PlayerData player)
    {
        if (CameraController.Instance == null) return;

        // Release Cutscene Mode regardless of player type
        // This stops the camera from sliding to the AI automatically.
        CameraController.Instance.cutsceneMode = false;
        
        if (!player.isAI)
        {
            Vector3 focusPoint = GetPlayerFocusPoint(player);
            CameraController.Instance.FocusOnPosition(focusPoint);
        }
    }

    private Vector3 GetPlayerFocusPoint(PlayerData player)
    {
        Vector3 averagePos = Vector3.zero;
        int count = 0;

        // Collect all owned items for THIS SPECIFIC player
        // Null check guards against units destroyed mid-turn (e.g. BuilderUnit running out of charges)
        foreach (var unit in allUnits)
        {
            if (unit == null) continue;
            if (unit.owner == player)
            {
                averagePos += unit.transform.position;
                count++;
            }
        }
        foreach (var tower in allTowers)
        {
            if (tower.owner == player)
            {
                averagePos += tower.transform.position;
                count++;
            }
        }

        // Also include HQs (SignalNodes) in the calculation
        foreach (var node in player.ownedNodes)
        {
            if (node != null)
            {
                averagePos += node.transform.position;
                count++;
            }
        }

        if (count > 0)
        {
            return averagePos / count;
        }
        
        return CameraController.Instance.transform.position; 
    }

    //  ACCESSORS
    public List<Unit>      GetAllUnits()  => allUnits;
    public List<TowerNode>     GetAllTowers()     => allTowers;   
    public List<WireNode>      GetAllWires()      => allWires;    
    public List<StructureNode> GetAllStructures() => allStructures;
    public List<PlayerData> GetPlayers()  => players;

    //  END TURN
    public void EndTurn()
    {
        currentPlayerIndex++;

        if (currentPlayerIndex >= players.Count)
        {
            currentPlayerIndex = 0;
            currentTurn++;
            UpdateEra();
            CheckGameEnd();
        }

        SaveSystem.SaveGame();
        StartTurn();
    }

    void UpdateEra()
    {
        if (currentTurn > 75) currentEra = GameEra.Futuristic;
        else if (currentTurn > 50) currentEra = GameEra.Retro;
        else if (currentTurn > 25) currentEra = GameEra.EarlyEighties;
        else currentEra = GameEra.Industrial;
        
        Debug.Log($"Game Era: {currentEra}");
        
        // JUICE (Phase 2)
        if (FeedbackController.Instance != null && currentTurn > 1)
        {
            FeedbackController.Instance.PlayEraTransition(currentEra.ToString());
        }
    }

    void CheckGameEnd()
    {
        if (currentTurn > MAX_TURNS)
        {
            PlayerData winner = InfluenceManager.Instance.GetWinner();
            int winningScore = InfluenceManager.Instance.GetTotalInfluence(winner);
            
            Debug.Log($"Game Over! Turn Limit Reached.");
            Debug.Log($"WINNER: {winner.playerName} with {winningScore} Influence!");
        }
    }

    //  REGISTRATION
    public void RegisterUnit(Unit unit)
    {
        if (!allUnits.Contains(unit))
            allUnits.Add(unit);
    }

    // Call this before Destroy(gameObject) on any unit so it is removed from
    // the allUnits list. Prevents MissingReferenceException in GetPlayerFocusPoint.
    public void UnregisterUnit(Unit unit)
    {
        allUnits.Remove(unit);
    }

    public void RegisterTower(TowerNode tower)
    {
        if (!allTowers.Contains(tower))
            allTowers.Add(tower);
    }

    public void RegisterWire(WireNode wire)
    {
        if (!allWires.Contains(wire))
            allWires.Add(wire);
    }

    public void RegisterStructure(StructureNode structure)
    {
        if (!allStructures.Contains(structure))
            allStructures.Add(structure);
    }

    public void UnregisterStructure(StructureNode structure)
    {
        allStructures.Remove(structure);
    }

    public void ResumeFromSave(int playerIndex)
    {
        currentPlayerIndex = playerIndex;
        UpdateEra();
        StartTurn();
        NotifyStatusChanged();
    }

    public string GetCurrentEra()
    {
        return currentEra.ToString();
    }

    //  ERA COMPARISON HELPERS  (System 1)
    // True when the World Era is ahead of the player's Hardware Era.
    // Triggers the obsolete-tech influence debuff.
    public bool IsHardwareObsolete(PlayerData player)
    {
        return (int)currentEra > (int)player.hardwareEra;
    }

    // True when the player's Hardware Era is ahead of their Workforce Era.
    // Triggers the unskilled-labor upkeep penalty.
    public bool HasLaborMismatch(PlayerData player)
    {
        return (int)player.hardwareEra > (int)player.workforceEra;
    }

    // Returns the influence generation multiplier for a player based on how
    // many eras their hardware lags behind the World Era.
    // Gap of 0 → 1.0 (no penalty).  Each era gap → −25 %, floored at 25 %.
    public float GetEraInfluenceMultiplier(PlayerData player)
    {
        int eraGap = (int)currentEra - (int)player.hardwareEra;
        if (eraGap <= 0) return 1.0f;

        return Mathf.Max(0.25f, 1f - eraGap * 0.25f);
    }

    // Returns the upkeep cost multiplier caused by a hardware/workforce era mismatch.
    // Gap of 0 → 1.0 (no penalty).  Each era gap → +50 % upkeep.
    public float GetUpkeepMultiplier(PlayerData player)
    {
        int eraGap = (int)player.hardwareEra - (int)player.workforceEra;
        if (eraGap <= 0) return 1.0f;

        return 1f + eraGap * 0.5f;
    }
}