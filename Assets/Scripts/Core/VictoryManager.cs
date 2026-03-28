using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// ═══════════════════════════════════════════════════════════════════════════════
//  VictoryManager
//
//  SCENE SETUP:
//    Each victory type loads a dedicated Unity Scene for its cinematic / ending.
//    Assign the exact scene name strings in the Inspector, and make sure every
//    scene is added to File → Build Settings.
//
//    monopolySceneName    → scene that plays the Monopoly ending
//    exodusSceneName      → scene that plays the Exodus ending
//    liquidationSceneName → scene that plays the Liquidation ending
//
//  MONOPOLY CAMERA SEQUENCE (plays before scene load):
//    1. Snaps the main camera directly above the winner's HQ (ownedNodes[0]),
//       locking rotation to (90, 0, 0) — perfectly top-down.
//    2. Calculates the centroid of every hex tile the winner owns.
//    3. Pans from the HQ position to that centroid over monopolyCameraPanDuration
//       seconds, simultaneously rising the camera Y up to monopolyCameraFinalY.
//    4. Holds briefly, then loads the Monopoly scene.
//
//  LIQUIDATION CAMERA SEQUENCE (plays before scene load):
//    1. Finds the AI enemy player's HQ (their ownedNodes[0]).
//    2. Camera smoothly travels to directly above the enemy HQ while snapping
//       to Y = liquidationApproachY and locking rotation to (90, 0, 0).
//       The camera is already at the cruising height before it arrives.
//    3. Once above the enemy HQ, the camera slowly descends from
//       liquidationApproachY down to liquidationDescentEndY.
//    4. The assigned liquidationParticle is instantiated on the enemy HQ and
//       continuously replayed for the full duration of the descent.
//    5. A 3-second countdown runs, then the Liquidation scene loads.
//
//  PASSING DATA TO THE VICTORY SCENE:
//    Before loading, TriggerVictory writes to the static VictoryPayload class.
//    Any script in the loaded scene can read:
//      VictoryPayload.WinnerName        → string
//      VictoryPayload.WinnerIsAI        → bool
//      VictoryPayload.Victory           → VictoryType
//      VictoryPayload.Headline          → string  (formatted headline text)
//      VictoryPayload.MainGameSceneName → string  (scene to return to)
//
//  GRID TRANSITION:
//    LoadVictoryScene now hands the scene load to GridTransitionManager if it
//    exists. The grid wipe animation replaces the sceneLoadDelay wait.
//    Set sceneLoadDelay to 0 in the Inspector — the transition IS the delay.
//    If GridTransitionManager is absent, the original WaitForSeconds fallback
//    is used automatically so nothing hard-breaks.
// ═══════════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────────
//  Static payload — survives scene loads, readable by any script in the
//  victory scene without needing a reference to VictoryManager.
// ─────────────────────────────────────────────────────────────────────────────
public static class VictoryPayload
{
    public static VictoryType Victory           { get; internal set; } = VictoryType.None;
    public static string      WinnerName        { get; internal set; } = string.Empty;
    public static bool        WinnerIsAI        { get; internal set; } = false;
    public static string      Headline          { get; internal set; } = string.Empty;
    public static string      MainGameSceneName { get; internal set; } = string.Empty;

    internal static void Write(VictoryType type, PlayerData winner, string headline)
    {
        Victory           = type;
        WinnerName        = winner != null ? winner.playerName : "Unknown";
        WinnerIsAI        = winner != null && winner.isAI;
        Headline          = headline;
        MainGameSceneName = SceneManager.GetActiveScene().name;
    }

    internal static void Clear()
    {
        Victory           = VictoryType.None;
        WinnerName        = string.Empty;
        WinnerIsAI        = false;
        Headline          = string.Empty;
        MainGameSceneName = string.Empty;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public enum VictoryType { None, Monopoly, Exodus, Liquidation }

public class VictoryManager : MonoBehaviour
{
    public static VictoryManager Instance;

    // ── Thresholds ────────────────────────────────────────────────────────────
    [Header("Thresholds")]
    [Tooltip("Fraction of all hex tiles one player must own to trigger Monopoly victory.")]
    [Range(0f, 1f)]
    public float monopolyThreshold = 0.75f;

    [Tooltip("Fraction of ALL enemy assets that must be denied to trigger Liquidation victory.")]
    [Range(0f, 1f)]
    public float liquidationThreshold = 0.60f;

    // ── Victory Scenes ────────────────────────────────────────────────────────
    [Header("Victory Scenes")]
    [Tooltip("Exact name of the Unity scene to load for the Monopoly ending. Must be in Build Settings.")]
    public string monopolySceneName = "VictoryScene_Monopoly";

    [Tooltip("Exact name of the Unity scene to load for the Exodus ending. Must be in Build Settings.")]
    public string exodusSceneName = "VictoryScene_Exodus";

    [Tooltip("Exact name of the Unity scene to load for the Liquidation ending. Must be in Build Settings.")]
    public string liquidationSceneName = "VictoryScene_Liquidation";

    [Tooltip("Fallback delay in seconds before loading the victory scene when GridTransitionManager " +
             "is NOT present. Set to 0 when using the grid transition — the animation IS the delay.")]
    [Min(0f)]
    public float sceneLoadDelay = 0f;

    // ── Monopoly Camera Pan ───────────────────────────────────────────────────
    [Header("Monopoly — Camera Pan")]
    [Tooltip("How many seconds the camera smoothly travels from its current position " +
             "to directly above the player HQ before the main pan begins.")]
    [Min(0.1f)]
    public float monopolyCameraApproachDuration = 2f;

    [Tooltip("How many seconds the camera takes to pan from above the player HQ to the " +
             "centroid of all their owned tiles.")]
    [Min(0.1f)]
    public float monopolyCameraPanDuration = 4f;

    [Tooltip("How many seconds the camera holds at the centroid before the scene loads.")]
    [Min(0f)]
    public float monopolyCameraHoldDuration = 1f;

    [Tooltip("The Y height the camera rises to during the pan. " +
             "The camera starts at whatever Y it is currently at when victory fires.")]
    public float monopolyCameraFinalY = 30f;

    [Tooltip("Easing curve for the camera pan. Left = start of pan, right = end. " +
             "Default is a smooth ease-in-out.")]
    public AnimationCurve monopolyCameraEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ── Liquidation Camera Sequence ───────────────────────────────────────────
    [Header("Liquidation — Camera Sequence")]
    [Tooltip("How many seconds the camera takes to travel from its current position " +
             "to directly above the enemy HQ at liquidationApproachY height.")]
    [Min(0.1f)]
    public float liquidationApproachDuration = 2.5f;

    [Tooltip("The Y height the camera snaps to and holds while approaching the enemy HQ. " +
             "The camera arrives at this height before the descent begins.")]
    public float liquidationApproachY = 30f;

    [Tooltip("The Y height the camera slowly descends to while hovering above the enemy HQ.")]
    public float liquidationDescentEndY = 10f;

    [Tooltip("How many seconds the camera takes to descend from liquidationApproachY " +
             "down to liquidationDescentEndY.")]
    [Min(0.1f)]
    public float liquidationDescentDuration = 5f;

    [Tooltip("How many seconds the camera holds at the bottom before the scene loads. " +
             "The countdown fires during this hold.")]
    [Min(0f)]
    public float liquidationCountdownDuration = 3f;

    [Tooltip("Parent GameObject prefab whose children are all the ParticleSystems for the Liquidation ending. " +
             "Drag the parent object here — every ParticleSystem found anywhere in its hierarchy will be " +
             "played continuously for the full duration of the descent and countdown.")]
    public GameObject liquidationParticleParent;

    [Tooltip("Easing curve for the approach travel and the descent. " +
             "Default is a smooth ease-in-out.")]
    public AnimationCurve liquidationCameraEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ── Runtime State ─────────────────────────────────────────────────────────
    public bool        VictoryTriggered { get; private set; }
    public VictoryType CurrentVictory   { get; private set; } = VictoryType.None;

    private Dictionary<PlayerData, int> deniedCounts               = new Dictionary<PlayerData, int>();
    private int                         totalEnemyAssetsAtGameStart = 0;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted += OnTurnStarted;
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Turn hook
    // ─────────────────────────────────────────────────────────────────────────
    private void OnTurnStarted(PlayerData currentPlayer)
    {
        if (VictoryTriggered) return;

        EvaluateMonopolyVictory();
        EvaluateLiquidationVictory();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  1. MONOPOLY
    // ═════════════════════════════════════════════════════════════════════════
    private void EvaluateMonopolyVictory()
    {
        if (GridManager.Instance == null || TurnManager.Instance == null) return;

        var allTiles = GridManager.Instance.GetAllTiles().ToList();
        int total    = allTiles.Count;
        if (total == 0) return;

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
                Debug.Log($"[VictoryManager] MONOPOLY VICTORY — {kvp.Key.playerName} owns " +
                          $"{kvp.Value}/{total} tiles ({fraction * 100f:F1}%).");
                TriggerVictory(VictoryType.Monopoly, kvp.Key);
                return;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  2. EXODUS — called externally when the exodus condition is met
    // ═════════════════════════════════════════════════════════════════════════
    public void TriggerExodusVictory(PlayerData winner)
    {
        if (VictoryTriggered) return;

        Debug.Log($"[VictoryManager] EXODUS VICTORY — {winner.playerName} triggered the exodus.");
        TriggerVictory(VictoryType.Exodus, winner);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  3. LIQUIDATION
    // ═════════════════════════════════════════════════════════════════════════
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

        if (totalEnemyAssetsAtGameStart == 0)
            CacheEnemyAssets();

        if (totalEnemyAssetsAtGameStart == 0) return;

        foreach (var kvp in deniedCounts)
        {
            float fraction = (float)kvp.Value / totalEnemyAssetsAtGameStart;
            if (fraction >= liquidationThreshold)
            {
                Debug.Log($"[VictoryManager] LIQUIDATION VICTORY — {kvp.Key.playerName} denied " +
                          $"{kvp.Value}/{totalEnemyAssetsAtGameStart} assets ({fraction * 100f:F1}%).");
                TriggerVictory(VictoryType.Liquidation, kvp.Key);
                return;
            }
        }
    }

    private void CacheEnemyAssets()
    {
        if (TurnManager.Instance == null) return;

        PlayerData humanPlayer = TurnManager.Instance.players.FirstOrDefault(p => !p.isAI);

        int count = 0;
        foreach (PlayerData p in TurnManager.Instance.players)
        {
            if (p == humanPlayer) continue;

            count += TurnManager.Instance.GetAllUnits().Count(u => u != null && u.owner == p);
            count += TurnManager.Instance.GetAllTowers().Count(t => t != null && t.owner == p);
            count += TurnManager.Instance.GetAllStructures().Count(s => s != null && s.owner == p);
            count += TurnManager.Instance.GetAllWires().Count(w => w != null && w.owner == p);
            count += p.ownedNodes.Count;
        }

        totalEnemyAssetsAtGameStart = Mathf.Max(1, count);
        Debug.Log($"[VictoryManager] Enemy asset baseline: {totalEnemyAssetsAtGameStart}.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CORE TRIGGER
    // ═════════════════════════════════════════════════════════════════════════
    public void TriggerVictory(VictoryType type, PlayerData winner)
    {
        if (VictoryTriggered) return;

        VictoryTriggered = true;
        CurrentVictory   = type;

        string headline = type switch
        {
            VictoryType.Monopoly    => $"MONOPOLY VICTORY\n\"{(winner != null ? winner.playerName : "Unknown")} owns the airwaves.\"",
            VictoryType.Exodus      => $"EXODUS VICTORY\n\"{(winner != null ? winner.playerName : "Unknown")} escapes to the stars.\"",
            VictoryType.Liquidation => $"LIQUIDATION VICTORY\n\"{(winner != null ? winner.playerName : "Unknown")} dissolved the competition.\"",
            _                       => "VICTORY"
        };

        string targetScene = type switch
        {
            VictoryType.Monopoly    => monopolySceneName,
            VictoryType.Exodus      => exodusSceneName,
            VictoryType.Liquidation => liquidationSceneName,
            _                       => string.Empty
        };

        VictoryPayload.Write(type, winner, headline);

        // Restore time so coroutines and camera movement run normally.
        Time.timeScale = 1f;

        // Play the victory BGM via the AudioManager
        if (AudioManager.Instance != null)
        {
            AudioClip victoryBGM = type switch
            {
                VictoryType.Monopoly => AudioManager.Instance.bgmVictoryMonopoly,
                VictoryType.Exodus => AudioManager.Instance.bgmVictoryExodus,
                VictoryType.Liquidation => AudioManager.Instance.bgmVictoryLiquidation,
                _ => null
            };

            if (victoryBGM != null)
            {
                // Play it seamlessly. It will crossfade and continue into the victory scene!
                AudioManager.Instance.PlayBGM(victoryBGM, false);
            }
        }

        if (type == VictoryType.Monopoly)
            StartCoroutine(MonopolyCameraSequence(winner, targetScene));
        else if (type == VictoryType.Liquidation)
            StartCoroutine(LiquidationCameraSequence(winner, targetScene));
        else
            StartCoroutine(LoadVictoryScene(targetScene));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  MONOPOLY CAMERA SEQUENCE
    //
    //  INPUT LOCK:
    //    At sequence start, CameraController.cutsceneMode is set to true and
    //    the EventSystem is disabled so no UI clicks or camera drag can fire.
    //    Both are left in their locked state — the scene load destroys everything
    //    so there is nothing to restore.
    //
    //  Step 1 — Smooth approach to above HQ:
    //    The camera glides from wherever it currently is to (HQ.x, currentY, HQ.z)
    //    while simultaneously rotating to (90, 0, 0). Uses the ease curve.
    //
    //  Step 2 — Calculate owned-tile centroid:
    //    All hex tiles owned by the winner are averaged in world space.
    //
    //  Step 3 — Pan from HQ to centroid, rise to monopolyCameraFinalY:
    //    X/Z move to the centroid; Y rises to monopolyCameraFinalY. Ease curve
    //    controls both phases. Rotation stays locked at (90, 0, 0).
    //
    //  Step 4 — Hold then load scene.
    // ═════════════════════════════════════════════════════════════════════════
    private IEnumerator MonopolyCameraSequence(PlayerData winner, string targetScene)
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogWarning("[VictoryManager] MonopolyCameraSequence: No main camera found. " +
                             "Skipping pan and loading scene directly.");
            yield return StartCoroutine(LoadVictoryScene(targetScene));
            yield break;
        }

        // ── Lock all player input ─────────────────────────────────────────────
        // Tell CameraController it is in cutscene mode so it stops processing
        // drag, zoom, and focus requests.
        if (CameraController.Instance != null)
            CameraController.Instance.cutsceneMode = true;

        // Disable the EventSystem so UI buttons and click events cannot fire.
        UnityEngine.EventSystems.EventSystem eventSystem =
            UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
            eventSystem.enabled = false;

        Debug.Log("[VictoryManager] Player input locked for Monopoly camera sequence.");

        // ── Find HQ world position ────────────────────────────────────────────
        // The player base is ownedNodes[0] — their main HQ SignalNode.
        Vector3 hqWorldPos = cam.transform.position; // fallback: stay put
        if (winner != null && winner.ownedNodes != null && winner.ownedNodes.Count > 0)
        {
            SignalNode hq = winner.ownedNodes[0];
            if (hq != null)
                hqWorldPos = hq.transform.position;
            else
                Debug.LogWarning("[VictoryManager] ownedNodes[0] is null; using camera position as HQ.");
        }
        else
        {
            Debug.LogWarning("[VictoryManager] Winner has no ownedNodes; using camera position as HQ.");
        }

        // The approach target: same Y the camera is at right now, directly above HQ.
        float   startY        = cam.transform.position.y;
        Vector3 approachStart = cam.transform.position;
        Vector3 approachEnd   = new Vector3(hqWorldPos.x, startY, hqWorldPos.z);

        Quaternion rotStart = cam.transform.rotation;
        Quaternion rotEnd   = Quaternion.Euler(90f, 0f, 0f);

        // ── Step 1: Smooth approach to above HQ ───────────────────────────────
        Debug.Log($"[VictoryManager] Approaching HQ at ({approachEnd.x:F1}, {approachEnd.y:F1}, {approachEnd.z:F1}).");

        float elapsed = 0f;
        while (elapsed < monopolyCameraApproachDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / monopolyCameraApproachDuration);
            float eased = monopolyCameraEase.Evaluate(t);

            cam.transform.position = Vector3.Lerp(approachStart, approachEnd, eased);
            cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, eased);

            yield return null;
        }

        // Snap exactly to approach end to remove float drift.
        cam.transform.position = approachEnd;
        cam.transform.rotation = rotEnd;

        // ── Step 2: Calculate centroid of all winner-owned tiles ───────────────
        Vector3 centroid       = hqWorldPos;
        int     ownedTileCount = 0;

        if (GridManager.Instance != null)
        {
            Vector3 sum = Vector3.zero;
            foreach (HexTile tile in GridManager.Instance.GetAllTiles())
            {
                if (tile == null) continue;
                if (tile.GetOwner() != winner) continue;
                sum += tile.transform.position;
                ownedTileCount++;
            }

            if (ownedTileCount > 0)
            {
                centroid = sum / ownedTileCount;
                Debug.Log($"[VictoryManager] Centroid of {ownedTileCount} owned tiles: " +
                          $"({centroid.x:F1}, {centroid.z:F1}).");
            }
            else
            {
                Debug.LogWarning("[VictoryManager] No owned tiles found for winner; " +
                                 "centroid defaults to HQ position.");
            }
        }
        else
        {
            Debug.LogWarning("[VictoryManager] GridManager not found; centroid defaults to HQ position.");
        }

        // Pan goes from current camera position (above HQ) to centroid at final Y.
        Vector3 panStart = cam.transform.position;
        Vector3 panEnd   = new Vector3(centroid.x, monopolyCameraFinalY, centroid.z);

        // ── Step 3: Pan to centroid, rise to final Y ──────────────────────────
        elapsed = 0f;
        while (elapsed < monopolyCameraPanDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / monopolyCameraPanDuration);
            float eased = monopolyCameraEase.Evaluate(t);

            cam.transform.position = Vector3.Lerp(panStart, panEnd, eased);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // keep locked
            yield return null;
        }

        cam.transform.position = panEnd;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Debug.Log($"[VictoryManager] Camera pan complete. Holding for {monopolyCameraHoldDuration:F1}s.");

        // ── Step 4: Hold then load ────────────────────────────────────────────
        if (monopolyCameraHoldDuration > 0f)
            yield return new WaitForSeconds(monopolyCameraHoldDuration);

        yield return StartCoroutine(LoadVictoryScene(targetScene));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  LIQUIDATION CAMERA SEQUENCE
    //
    //  INPUT LOCK:
    //    CameraController.cutsceneMode is set to true and the EventSystem is
    //    disabled exactly as in the Monopoly sequence. Both stay locked until
    //    the scene load destroys everything.
    //
    //  Step 1 — Locate enemy HQ:
    //    Find the first AI player in TurnManager.players and read their
    //    ownedNodes[0] SignalNode world position.
    //
    //  Step 2 — Smooth approach to cruise height above enemy HQ:
    //    From wherever the camera currently is (any position, any rotation),
    //    lerp position to (enemyHQ.x, liquidationApproachY, enemyHQ.z) and
    //    slerp rotation to (90, 0, 0) simultaneously — exactly like the Monopoly
    //    approach. The camera rises to Y=30 and locks rotation as it travels.
    //
    //  Step 3 — Spawn particles then begin slow descent:
    //    The liquidationParticleParent GameObject is instantiated on the enemy HQ.
    //    Every ParticleSystem in its hierarchy is played and continuously replayed.
    //    The 3-second countdown starts the moment descent begins.
    //    When (liquidationCountdownDuration - sceneLoadDelay) seconds have elapsed
    //    since descent started, LoadVictoryScene fires as a parallel coroutine so
    //    the scene transition overlaps the end of the descent — the camera is still
    //    moving when the scene loads.
    //
    //  Step 4 — Countdown tail:
    //    After the descent loop ends the coroutine keeps particles alive until the
    //    parallel LoadVictoryScene destroys the scene. Nothing more to yield on.
    // ═════════════════════════════════════════════════════════════════════════
    private IEnumerator LiquidationCameraSequence(PlayerData winner, string targetScene)
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogWarning("[VictoryManager] LiquidationCameraSequence: No main camera found. " +
                             "Skipping sequence and loading scene directly.");
            yield return StartCoroutine(LoadVictoryScene(targetScene));
            yield break;
        }

        // ── Lock all player input ─────────────────────────────────────────────
        if (CameraController.Instance != null)
            CameraController.Instance.cutsceneMode = true;

        UnityEngine.EventSystems.EventSystem eventSystem =
            UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
            eventSystem.enabled = false;

        Debug.Log("[VictoryManager] Player input locked for Liquidation camera sequence.");

        // ── Step 1: Find the enemy AI player's HQ ────────────────────────────
        // The enemy is any AI player in the player list. Their HQ is ownedNodes[0].
        Vector3 enemyHqPos = cam.transform.position; // fallback: stay put

        PlayerData enemyPlayer = TurnManager.Instance != null
            ? TurnManager.Instance.players.FirstOrDefault(p => p.isAI)
            : null;

        if (enemyPlayer != null && enemyPlayer.ownedNodes != null && enemyPlayer.ownedNodes.Count > 0)
        {
            SignalNode enemyHq = enemyPlayer.ownedNodes[0];
            if (enemyHq != null)
            {
                enemyHqPos = enemyHq.transform.position;
                Debug.Log($"[VictoryManager] Enemy HQ found at ({enemyHqPos.x:F1}, {enemyHqPos.z:F1}) " +
                          $"for player '{enemyPlayer.playerName}'.");
            }
            else
            {
                Debug.LogWarning("[VictoryManager] Enemy ownedNodes[0] is null; using camera position as fallback.");
            }
        }
        else
        {
            Debug.LogWarning("[VictoryManager] No AI player or no enemy HQ found; using camera position as fallback.");
        }

        // ── Step 2: Smooth approach — rise to cruise height above enemy HQ ────
        // Lerp the full position (including Y) from wherever the camera currently
        // is up to liquidationApproachY, while simultaneously slerping rotation
        // to (90, 0, 0). Identical pattern to the Monopoly approach step.
        Vector3    approachStart = cam.transform.position;
        Vector3    approachEnd   = new Vector3(enemyHqPos.x, liquidationApproachY, enemyHqPos.z);
        Quaternion rotStart      = cam.transform.rotation;
        Quaternion rotEnd        = Quaternion.Euler(90f, 0f, 0f);

        Debug.Log($"[VictoryManager] Approaching enemy HQ: rising to Y={liquidationApproachY:F1} " +
                  $"and locking rotation to (90,0,0).");

        float elapsed = 0f;
        while (elapsed < liquidationApproachDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / liquidationApproachDuration);
            float eased = liquidationCameraEase.Evaluate(t);

            cam.transform.position = Vector3.Lerp(approachStart, approachEnd, eased);
            cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, eased);

            yield return null;
        }

        // Snap to remove float drift, exactly like the Monopoly approach finalize.
        cam.transform.position = approachEnd;
        cam.transform.rotation = rotEnd;

        Debug.Log("[VictoryManager] Arrived above enemy HQ at cruise height. Beginning descent.");

        // ── Step 3: Spawn particle parent on the enemy HQ ────────────────────
        ParticleSystem[] spawnedParticles = null;
        if (liquidationParticleParent != null)
        {
            GameObject spawnedParent = Instantiate(liquidationParticleParent,
                                                   new Vector3(enemyHqPos.x, enemyHqPos.y, enemyHqPos.z),
                                                   Quaternion.identity);
            spawnedParticles = spawnedParent.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            foreach (var ps in spawnedParticles)
                ps.Play();
            Debug.Log($"[VictoryManager] Liquidation particle parent spawned " +
                      $"({spawnedParticles.Length} ParticleSystem(s)).");
        }
        else
        {
            Debug.LogWarning("[VictoryManager] liquidationParticleParent is not assigned; " +
                             "skipping particle effect.");
        }

        // ── Step 4: Descent — countdown runs in parallel ──────────────────────
        // The countdown begins the instant descent starts.
        // Once (liquidationCountdownDuration - sceneLoadDelay) seconds have passed,
        // LoadVictoryScene is kicked off as a fire-and-forget coroutine so the
        // scene transition begins while the camera is still descending.
        // With GridTransitionManager active, set sceneLoadDelay = 0 so the
        // trigger fires exactly at liquidationCountdownDuration seconds.
        Vector3 descentStart          = cam.transform.position;
        Vector3 descentEnd            = new Vector3(enemyHqPos.x, liquidationDescentEndY, enemyHqPos.z);
        float   countdownElapsed      = 0f;
        float   sceneLoadTriggerTime  = liquidationCountdownDuration - sceneLoadDelay;
        bool    sceneLoadFired        = false;

        elapsed = 0f;
        while (elapsed < liquidationDescentDuration)
        {
            elapsed          += Time.deltaTime;
            countdownElapsed += Time.deltaTime;

            float t     = Mathf.Clamp01(elapsed / liquidationDescentDuration);
            float eased = liquidationCameraEase.Evaluate(t);

            cam.transform.position = Vector3.Lerp(descentStart, descentEnd, eased);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Replay any child PS that finished.
            if (spawnedParticles != null)
                foreach (var ps in spawnedParticles)
                    if (ps != null && !ps.isPlaying) ps.Play();

            // Fire the scene load once the countdown reaches its trigger point.
            if (!sceneLoadFired && countdownElapsed >= sceneLoadTriggerTime)
            {
                sceneLoadFired = true;
                Debug.Log($"[VictoryManager] Countdown trigger reached " +
                          $"({sceneLoadTriggerTime:F2}s). Firing scene load in parallel.");
                StartCoroutine(LoadVictoryScene(targetScene));
            }

            yield return null;
        }

        cam.transform.position = descentEnd;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // If descent finished before the countdown trigger (e.g. very short descent
        // duration), fire the scene load now as a safety net.
        if (!sceneLoadFired)
        {
            Debug.Log("[VictoryManager] Descent ended before countdown trigger; firing scene load now.");
            StartCoroutine(LoadVictoryScene(targetScene));
        }

        // Keep particles alive while LoadVictoryScene's delay ticks down.
        // The scene load will destroy this object, so this loop is self-terminating.
        while (true)
        {
            if (spawnedParticles != null)
                foreach (var ps in spawnedParticles)
                    if (ps != null && !ps.isPlaying) ps.Play();
            yield return null;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  SCENE LOAD
    //
    //  Path A — GridTransitionManager exists:
    //    Hands the scene name off to GridTransitionManager.LoadScene(), which
    //    plays the grid wipe and then calls SceneManager.LoadScene internally.
    //    The coroutine yield breaks immediately after — the transition owns it.
    //
    //  Path B — GridTransitionManager absent (fallback):
    //    Waits sceneLoadDelay seconds then calls SceneManager.LoadScene directly.
    //    This matches the original behaviour and keeps things working even if
    //    the transition manager was never set up.
    // ═════════════════════════════════════════════════════════════════════════
    private IEnumerator LoadVictoryScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[VictoryManager] Victory scene name is empty! " +
                           "Assign a scene name in the Inspector under Victory Scenes.");
            yield break;
        }

        // Confirm the scene is in Build Settings before loading.
        bool sceneFound = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) { sceneFound = true; break; }
        }

        if (!sceneFound)
        {
            Debug.LogError($"[VictoryManager] Scene '{sceneName}' is not in Build Settings! " +
                            "Add it via File → Build Settings.");
            yield break;
        }

        // ── Path A: GridTransitionManager is alive — hand off to it ──────────
        if (GridTransitionManager.Instance != null)
        {
            Debug.Log($"[VictoryManager] Handing scene load '{sceneName}' to GridTransitionManager.");
            GridTransitionManager.Instance.LoadScene(sceneName);
            // GridTransitionManager now owns the animation + scene load.
            // We yield break here; the scene change will destroy this object.
            yield break;
        }

        // ── Path B: Fallback — no transition manager, use plain delay ─────────
        Debug.LogWarning("[VictoryManager] GridTransitionManager not found. " +
                         "Falling back to plain scene load with sceneLoadDelay.");

        Debug.Log($"[VictoryManager] Loading victory scene '{sceneName}' " +
                  $"in {sceneLoadDelay:F2}s (fallback).");

        if (sceneLoadDelay > 0f)
            yield return new WaitForSeconds(sceneLoadDelay);

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  DEBUG
    // ═════════════════════════════════════════════════════════════════════════
    [HideInInspector] public bool debugForceMonopoly    = false;
    [HideInInspector] public bool debugForceExodus      = false;
    [HideInInspector] public bool debugForceLiquidation = false;

    public void DebugCheckForcedVictory()
    {
        if (VictoryTriggered) return;

        PlayerData humanPlayer = TurnManager.Instance != null
            ? TurnManager.Instance.players.FirstOrDefault(p => !p.isAI)
            : null;

        if (debugForceMonopoly)
        {
            debugForceMonopoly = false;
            Debug.Log("[VictoryManager] [DEBUG] Forcing MONOPOLY.");
            TriggerVictory(VictoryType.Monopoly, humanPlayer);
        }
        else if (debugForceExodus)
        {
            debugForceExodus = false;
            Debug.Log("[VictoryManager] [DEBUG] Forcing EXODUS.");
            TriggerVictory(VictoryType.Exodus, humanPlayer);
        }
        else if (debugForceLiquidation)
        {
            debugForceLiquidation = false;
            Debug.Log("[VictoryManager] [DEBUG] Forcing LIQUIDATION.");
            TriggerVictory(VictoryType.Liquidation, humanPlayer);
        }
    }
}