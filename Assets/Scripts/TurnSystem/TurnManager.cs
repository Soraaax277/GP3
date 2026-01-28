using UnityEngine;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public enum GameEra { Industrial, EarlyEighties, Retro, Futuristic }
    public GameEra currentEra { get; private set; }
    public int currentTurn { get; set; } = 1;
    public const int MAX_TURNS = 100;

    public PlayerData currentPlayer { get; private set; }

    public List<PlayerData> players;
    private int currentPlayerIndex;

    private List<Unit> allUnits = new List<Unit>();
    private List<TowerNode> allTowers = new List<TowerNode>();

    public event System.Action OnGameStatusChanged;

    public void NotifyStatusChanged()
    {
        OnGameStatusChanged?.Invoke();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (SaveSystem.HasSaveData())
        {
            SaveSystem.LoadGame();
        }
    }

    public void StartGame(List<PlayerData> playerList)
    {
        players = playerList;
        currentTurn = 1;
        currentEra = GameEra.Industrial;
        currentPlayerIndex = 0;

        StartTurn();
    }

    void StartTurn()
    {
        currentPlayer = players[currentPlayerIndex];
        Debug.Log($"Turn {currentTurn} - {currentPlayer.playerName}'s turn");

        OnGameStatusChanged?.Invoke();

        foreach (Unit unit in allUnits)
            unit.OnTurnStart(currentPlayer);

        foreach (TowerNode tower in allTowers)
            tower.CheckForDestruction();

        if (currentPlayer.isAI && EnemyAI.Instance != null)
        {
            EnemyAI.Instance.ExecuteTurn(currentPlayer);
        }
    }

    public List<Unit> GetAllUnits() => allUnits;
    public List<PlayerData> GetPlayers() => players;

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
    }

    void CheckGameEnd()
    {
        if (currentTurn > MAX_TURNS)
        {
            Debug.Log("Game Over! Turn Limit Reached.");
        }
    }

    public void RegisterUnit(Unit unit)
    {
        if (!allUnits.Contains(unit))
            allUnits.Add(unit);
    }

    public void RegisterTower(TowerNode tower)
    {
        if (!allTowers.Contains(tower))
            allTowers.Add(tower);
    }

    public string GetCurrentEra()
    {
        return currentEra.ToString();
    }
}
