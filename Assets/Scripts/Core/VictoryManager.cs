using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ═══════════════════════════════════════════════════════════════════════════════
//  VictoryManager  —  evaluates all three end-game victory conditions each turn.
//
//  CINEMACHINE INTEGRATION:
//    Assign your Timeline (PlayableDirector) to the respective slots in the Inspector.
//    When a victory fires, the manager will automatically play that Timeline and WAIT
//    for it to finish. Once the director stops, the victory UI panel will appear.
//    (If no director is assigned, the UI just appears instantly).
// ═══════════════════════════════════════════════════════════════════════════════

public enum VictoryType { None, Monopoly, Exodus, Liquidation }

public class VictoryManager : MonoBehaviour
{
    public static VictoryManager Instance;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Thresholds")]
    [Tooltip("Fraction of all hex tiles one player must own to trigger Monopoly victory.")]
    [Range(0f, 1f)]
    public float monopolyThreshold = 0.75f;

    [Tooltip("Fraction of ALL enemy assets (units + structures + towers) that must be denied to trigger Liquidation victory.")]
    [Range(0f, 1f)]
    public float liquidationThreshold = 0.60f;

    [Header("Cinematics (Timeline / Cinemachine)")]
    [Tooltip("Timeline director that plays the Monopoly ending cinematic.")]
    public UnityEngine.Playables.PlayableDirector monopolyDirector;
    
    [Tooltip("Timeline director that plays the Exodus rocket launch cinematic.")]
    public UnityEngine.Playables.PlayableDirector exodusDirector;
    
    [Tooltip("Timeline director that plays the Liquidation corporate wipeout cinematic.")]
    public UnityEngine.Playables.PlayableDirector liquidationDirector;

    [Header("UI References")]
    [Tooltip("Root canvas/panel shown when a victory fires. Keep it OFF in the scene—" +
             "VictoryManager enables it.")]
    public GameObject victoryScreenCanvas;

    [Tooltip("(Optional) TMP text for the victory headline message.")]
    public TMPro.TextMeshProUGUI victoryHeadlineText;

    // ── Runtime state ─────────────────────────────────────────────────────────
    public bool VictoryTriggered { get; private set; }
    public VictoryType CurrentVictory { get; private set; } = VictoryType.None;

    // Liquidation tracking: maps each player to how many enemy assets they have denied.
    // "Denied" = unit eliminated via Saboteur/SalesMarketer or building destroyed.
    // Increment this with RecordDenial(attacker) from the relevant unit/building code.
    private Dictionary<PlayerData, int> deniedCounts  = new Dictionary<PlayerData, int>();
    private int totalEnemyAssetsAtGameStart = 0;   // Cached on first evaluation

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Subscribe to end-of-turn events so we check every full round.
        // TurnManager.EndTurn() calls CheckGameEnd() then StartTurn().
        // We piggyback on OnTurnStarted — it fires after every player's turn begins.
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted += OnTurnStarted;
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Turn hook — evaluates after each player turn begins
    // ─────────────────────────────────────────────────────────────────────────
    private void OnTurnStarted(PlayerData currentPlayer)
    {
        if (VictoryTriggered) return;

        EvaluateMonopolyVictory();
        EvaluateLiquidationVictory();
        // Exodus is event-driven (see TriggerExodusVictory), not polled.
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  1. MONOPOLY — 75 % of all hex tiles owned by one player
    // ═════════════════════════════════════════════════════════════════════════
    private void EvaluateMonopolyVictory()
    {
        if (GridManager.Instance == null || TurnManager.Instance == null) return;

        var allTiles = GridManager.Instance.GetAllTiles().ToList();
        int total = allTiles.Count;
        if (total == 0) return;

        // Count tiles owned per player (GetOwner returns the dominant player or null)
        var ownedCounts = new Dictionary<PlayerData, int>();
        foreach (var tile in allTiles)
        {
            PlayerData owner = tile.GetOwner();
            if (owner == null) continue;
            if (!ownedCounts.ContainsKey(owner)) ownedCounts[owner] = 0;
            ownedCounts[owner]++;
        }

        foreach (var kvp in ownedCounts)
        {
            float fraction = (float)kvp.Value / total;
            if (fraction >= monopolyThreshold)
            {
                Debug.Log($"[VictoryManager] 🏆 MONOPOLY VICTORY triggered! " +
                          $"{kvp.Key.playerName} owns {kvp.Value}/{total} tiles " +
                          $"({fraction * 100f:F1}% ≥ {monopolyThreshold * 100f:F0}% threshold).");
                TriggerVictory(VictoryType.Monopoly, kvp.Key);
                return;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  2. EXODUS — called directly when the Rocketship "Launch" button is pressed
    // ═════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Call this from Rocketship.Launch() (or BuildingUIManager's Launch button handler)
    /// when the player confirms the launch.
    /// </summary>
    public void TriggerExodusVictory(PlayerData winner)
    {
        if (VictoryTriggered) return;

        Debug.Log($"[VictoryManager] 🚀 EXODUS VICTORY triggered! " +
                  $"{winner.playerName} launched the interplanetary rocket and escaped to orbit.");
        TriggerVictory(VictoryType.Exodus, winner);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  3. LIQUIDATION — 60 % of enemy assets denied by one player
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call this whenever a player successfully eliminates/denies an enemy asset
    /// (unit kill, building destroy, recruitment conversion, etc.).
    /// </summary>
    public void RecordDenial(PlayerData denier, int count = 1)
    {
        if (!deniedCounts.ContainsKey(denier)) deniedCounts[denier] = 0;
        deniedCounts[denier] += count;
        Debug.Log($"[VictoryManager] Denial recorded for {denier.playerName} " +
                  $"(total: {deniedCounts[denier]}).");
    }

    private void EvaluateLiquidationVictory()
    {
        if (TurnManager.Instance == null) return;

        // Cache baseline on first evaluation (after all units/buildings are spawned)
        if (totalEnemyAssetsAtGameStart == 0)
            CacheEnemyAssets();

        if (totalEnemyAssetsAtGameStart == 0) return;   // Nothing to deny yet

        foreach (var kvp in deniedCounts)
        {
            float fraction = (float)kvp.Value / totalEnemyAssetsAtGameStart;
            if (fraction >= liquidationThreshold)
            {
                Debug.Log($"[VictoryManager] ☠️  LIQUIDATION VICTORY triggered! " +
                          $"{kvp.Key.playerName} denied {kvp.Value}/{totalEnemyAssetsAtGameStart} assets " +
                          $"({fraction * 100f:F1}% ≥ {liquidationThreshold * 100f:F0}% threshold).");
                TriggerVictory(VictoryType.Liquidation, kvp.Key);
                return;
            }
        }
    }

    // Counts every unit, tower, structure, and wire owned by enemy players
    // (relative to the first human player) at the time of first evaluation.
    private void CacheEnemyAssets()
    {
        if (TurnManager.Instance == null) return;

        // "Enemy" = all players that are AI (or not the first human player)
        PlayerData humanPlayer = TurnManager.Instance.players.FirstOrDefault(p => !p.isAI);

        int count = 0;
        foreach (PlayerData p in TurnManager.Instance.players)
        {
            if (p == humanPlayer) continue;   // Skip the human

            count += TurnManager.Instance.GetAllUnits().Count(u => u != null && u.owner == p);
            count += TurnManager.Instance.GetAllTowers().Count(t => t != null && t.owner == p);
            count += TurnManager.Instance.GetAllStructures().Count(s => s != null && s.owner == p);
            count += TurnManager.Instance.GetAllWires().Count(w => w != null && w.owner == p);
            count += p.ownedNodes.Count;   // HQ Signal Nodes
        }

        totalEnemyAssetsAtGameStart = Mathf.Max(1, count);
        Debug.Log($"[VictoryManager] Enemy asset baseline cached: {totalEnemyAssetsAtGameStart} total assets.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CORE TRIGGER
    // ═════════════════════════════════════════════════════════════════════════
    private void TriggerVictory(VictoryType type, PlayerData winner)
    {
        if (VictoryTriggered) return;

        VictoryTriggered = true;
        CurrentVictory   = type;

        // Pause gameplay
        Time.timeScale = 0f;

        // ── Build headline ────────────────────────────────────────────────────
        string headline = type switch
        {
            VictoryType.Monopoly    => $"MONOPOLY VICTORY\n\"{winner.playerName} owns the airwaves.\"",
            VictoryType.Exodus      => $"EXODUS VICTORY\n\"{winner.playerName} escapes to the stars.\"",
            VictoryType.Liquidation => $"LIQUIDATION VICTORY\n\"{winner.playerName} dissolved the competition.\"",
            _                       => "VICTORY"
        };

        // ── Get the mapped cinematic timeline ─────────────────────────────────
        UnityEngine.Playables.PlayableDirector director = type switch
        {
            VictoryType.Monopoly    => monopolyDirector,
            VictoryType.Exodus      => exodusDirector,
            VictoryType.Liquidation => liquidationDirector,
            _                       => null
        };

        // ── Play Cinematic or Show UI immediately ─────────────────────────────
        if (director != null)
        {
            Debug.Log($"[VictoryManager] Playing cinematic for {type}. UI will show when it finishes.");
            
            // Unsubscribe just in case, then subscribe to show UI when the timeline ends
            director.stopped -= OnCinematicFinished;
            director.stopped += OnCinematicFinished;
            
            // We pass the headline text as a string to the event using a closure
            // but unity's Playable object doesn't pass args. So we stash it.
            _pendingHeadline = headline;
            
            director.Play();
        }
        else
        {
            // No cinematic assigned in Inspector — show UI immediately.
            ShowVictoryUI(headline);
        }
    }

    private string _pendingHeadline;
    private void OnCinematicFinished(UnityEngine.Playables.PlayableDirector d)
    {
        d.stopped -= OnCinematicFinished;
        ShowVictoryUI(_pendingHeadline);
    }

    /// <summary>
    /// Shows the victory screen overlay.
    /// Call this from the Cinemachine Timeline's "stopped" callback once the cutscene ends.
    /// </summary>
    public void ShowVictoryUI(string headline)
    {
        if (victoryHeadlineText != null)
            victoryHeadlineText.text = headline;

        if (victoryScreenCanvas != null)
            victoryScreenCanvas.SetActive(true);
        else
            Debug.Log($"[VictoryManager] Victory screen: {headline} " +
                      "(No victoryScreenCanvas assigned — assign one in the Inspector.)");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  DEBUG — called by DebugCheatManager to force a specific victory next tick
    // ═════════════════════════════════════════════════════════════════════════

    // Set by DebugCheatManager from the Inspector. Checked on next OnTurnStarted.
    [HideInInspector] public bool debugForceMonopoly    = false;
    [HideInInspector] public bool debugForceExodus      = false;
    [HideInInspector] public bool debugForceLiquidation = false;

    /// <summary>Called by DebugCheatManager on each OnTurnStarted to fire forced victories.</summary>
    public void DebugCheckForcedVictory()
    {
        if (VictoryTriggered) return;

        PlayerData humanPlayer = TurnManager.Instance != null
            ? TurnManager.Instance.players.FirstOrDefault(p => !p.isAI)
            : null;

        if (debugForceMonopoly)
        {
            debugForceMonopoly = false;
            Debug.Log("[VictoryManager] [DEBUG] Forcing MONOPOLY victory.");
            TriggerVictory(VictoryType.Monopoly, humanPlayer);
        }
        else if (debugForceExodus)
        {
            debugForceExodus = false;
            Debug.Log("[VictoryManager] [DEBUG] Forcing EXODUS victory.");
            TriggerVictory(VictoryType.Exodus, humanPlayer);
        }
        else if (debugForceLiquidation)
        {
            debugForceLiquidation = false;
            Debug.Log("[VictoryManager] [DEBUG] Forcing LIQUIDATION victory.");
            TriggerVictory(VictoryType.Liquidation, humanPlayer);
        }
    }
}
