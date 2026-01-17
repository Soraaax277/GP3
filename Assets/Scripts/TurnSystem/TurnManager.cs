using UnityEngine;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public int currentTurn { get; private set; }
    public PlayerData currentPlayer { get; private set; }

    private List<PlayerData> players;
    private int currentPlayerIndex;

    private List<Unit> allUnits = new List<Unit>();
    private List<TowerNode> allTowers = new List<TowerNode>();

    private void Awake()
    {
        Instance = this;
    }

    public void StartGame(List<PlayerData> playerList)
    {
        players = playerList;
        currentTurn = 1;
        currentPlayerIndex = 0;

        StartTurn();
    }

    void StartTurn()
    {
        currentPlayer = players[currentPlayerIndex];
        Debug.Log($"Turn {currentTurn} - {currentPlayer.playerName}'s turn");

        foreach (Unit unit in allUnits)
            unit.OnTurnStart(currentPlayer);

        foreach (TowerNode tower in allTowers)
            tower.CheckForDestruction();
    }

    public void EndTurn()
    {
        currentPlayerIndex++;

        if (currentPlayerIndex >= players.Count)
        {
            currentPlayerIndex = 0;
            currentTurn++;
        }

        StartTurn();
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
