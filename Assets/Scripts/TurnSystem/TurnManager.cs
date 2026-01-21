using UnityEngine;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public enum GameEra { Industrial, EarlyEighties, Retro, Futuristic }
    public GameEra currentEra { get; private set; }
    public int currentTurnNumber { get; private set; } = 1;
    public const int MAX_TURNS = 100;

    public PlayerData currentPlayer { get; private set; }

    private List<PlayerData> players;
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

    public void StartGame(List<PlayerData> playerList)
    {
        players = playerList;
        currentTurnNumber = 1;
        currentEra = GameEra.Industrial;
        currentPlayerIndex = 0;

        StartTurn();
    }

    void StartTurn()
    {
        currentPlayer = players[currentPlayerIndex];
        Debug.Log($"Turn {currentTurnNumber} - {currentPlayer.playerName}'s turn");

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
            currentTurnNumber++;
            UpdateEra();
            CheckGameEnd();
        }

        StartTurn();
    }

    void UpdateEra()
    {
        if (currentTurnNumber > 75) currentEra = GameEra.Futuristic;
        else if (currentTurnNumber > 50) currentEra = GameEra.Retro;
        else if (currentTurnNumber > 25) currentEra = GameEra.EarlyEighties;
        else currentEra = GameEra.Industrial;
        
        Debug.Log($"Game Era: {currentEra}");
    }

    void CheckGameEnd()
    {
        if (currentTurnNumber > MAX_TURNS)
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
}
