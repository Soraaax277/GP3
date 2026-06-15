using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public List<PlayerData> players = new List<PlayerData>();

    [Header("References")]
    public BusinessSpawner businessSpawner;

    // Prevents Start() from running on a duplicate that was Destroy()d in Awake().
    // Destroy() only schedules destruction — Start() still fires in the same frame
    // without this guard, which would launch a second SetupGame() coroutine.
    private bool _isDuplicate = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            _isDuplicate = true;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;

        // --- PHASE 1 COMPONENTS ---
        // --- CORE COMPONENTS ON GAME OBJECT ---
        if (GetComponent<PowerGridOverlay>() == null) gameObject.AddComponent<PowerGridOverlay>();
        if (GetComponent<EventManager>() == null) gameObject.AddComponent<EventManager>();
        if (GetComponent<HazardManager>() == null) gameObject.AddComponent<HazardManager>();
        if (GetComponent<FieldOfViewManager>() == null) gameObject.AddComponent<FieldOfViewManager>();

        // --- PHASE 2 COMPONENTS ---
        if (GetComponent<InfluenceBorderRenderer>() == null) gameObject.AddComponent<InfluenceBorderRenderer>();
        if (GetComponent<FeedbackController>() == null) gameObject.AddComponent<FeedbackController>();

        // --- SEED RECOVERY ---
        // If we have a save, we must give the seeds to GridManager BEFORE it starts its Start() logic (if possible)
        float sx, sy;
        if (SaveSystem.TryPeekMapSeeds(out sx, out sy))
        {
             if (GridManager.Instance != null)
                 GridManager.Instance.SeedMap(sx, sy);
        }
    }

    private void Start()
    {
        if (_isDuplicate) return;
        StartCoroutine(SetupGame());
    }

    IEnumerator SetupGame()
    {
        while (GridManager.Instance == null || !GridManager.Instance.IsReady)
            yield return null;

        CreatePlayers();

        // BUG FIX: TurnManager.Instance can be null if its Awake/Start hasn't run yet.
        // A missing null-guard here throws a NullReferenceException that silently kills
        // the coroutine in Unity, so SpawnInitialBusinesses() never gets called.
        while (TurnManager.Instance == null)
            yield return null;

        TurnManager.Instance.players = this.players;

        if (SaveSystem.HasSaveData())
        {
            Debug.Log("GameManager: Found save data, loading...");
            bool success = SaveSystem.LoadGame();
            if (success)
            {
                // BUG FIX: Validate that the save actually contains player bases.
                // Stale/corrupt saves from a prior broken run can "load successfully"
                // while having no nodes, causing yield break to skip SpawnInitialBusinesses().
                bool basesLoaded = players.Count > 0
                    && players[0].ownedNodes != null
                    && players[0].ownedNodes.Count > 0;

                if (basesLoaded)
                {
                    yield break;
                }

                Debug.LogWarning("GameManager: Save loaded but no player bases found — discarding save and spawning fresh.");
            }
            else
            {
                Debug.LogWarning("GameManager: Load failed, starting new game.");
            }
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