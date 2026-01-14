using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public List<PlayerData> players = new List<PlayerData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        CreatePlayers();
        TurnManager.Instance.StartGame(players);
    }

    void CreatePlayers()
    {
        players.Add(new PlayerData(0, "Player 1"));
        players.Add(new PlayerData(1, "Player 2"));
    }
}
