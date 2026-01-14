using UnityEngine;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public int currentTurn { get; private set; }
    public PlayerData currentPlayer { get; private set; }

    private List<PlayerData> players;
    private int currentPlayerIndex;

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
}
