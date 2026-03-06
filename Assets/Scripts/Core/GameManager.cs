using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public List<PlayerData> players = new List<PlayerData>();

    [Header("References")]
    public BusinessSpawner businessSpawner;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;

        // --- PHASE 1 COMPONENTS ---
        if (GetComponent<EventManager>() == null) gameObject.AddComponent<EventManager>();
        if (GetComponent<HazardManager>() == null) gameObject.AddComponent<HazardManager>();
        if (GetComponent<FieldOfViewManager>() == null) gameObject.AddComponent<FieldOfViewManager>();

        // --- PHASE 2 COMPONENTS ---
        if (GetComponent<InfluenceBorderRenderer>() == null) gameObject.AddComponent<InfluenceBorderRenderer>();
        if (GetComponent<PowerGridOverlay>() == null) gameObject.AddComponent<PowerGridOverlay>();
        if (GetComponent<FeedbackController>() == null) gameObject.AddComponent<FeedbackController>();
    }

    private void Start()
    {
        StartCoroutine(SetupGame());
    }

    IEnumerator SetupGame()
    {
        while (GridManager.Instance == null || !GridManager.Instance.IsReady)
            yield return null;

        CreatePlayers();
        TurnManager.Instance.players = this.players;

        if (SaveSystem.HasSaveData())
        {
            Debug.Log("GameManager: Found save data, loading...");
            bool success = SaveSystem.LoadGame();
            if (success)
            {
                yield break;
            }
            Debug.LogWarning("GameManager: Load failed, starting new game.");
        }

        SpawnInitialBusinesses();
        TurnManager.Instance.StartGame(players);
    }

    void CreatePlayers()
    {
        players.Add(new PlayerData(0, "Player 1", false)); 
        players.Add(new PlayerData(1, "Enemy AI", true));  
    }

    void SpawnInitialBusinesses()
    {
        for (int i = 0; i < players.Count; i++)
        {
            SignalNode node = businessSpawner.SpawnInitialBusiness(players[i]);

            if (i == 0 && node != null)
            {
                CameraController.Instance.FocusOnPosition(node.transform.position, 5f, 3f, 1f);
            }
        }
    }
}
