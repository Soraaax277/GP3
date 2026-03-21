using UnityEngine;
using System.Collections.Generic;

// DEBUG / TESTING ONLY — attach to any persistent GameObject in the scene.
// 
// HOW TO USE:
//   1. Attach this script to any GameObject (e.g. a "DebugManager" GameObject).
//   2. Toggle the cheat booleans in the Inspector, or press the hotkeys at runtime.
//   3. Click "Apply Cheats Now" in the Inspector (via context menu) or press the keys.
//
// HOTKEYS (in Play Mode):
//   F1  — Unlock all tech nodes for the current player
//   F2  — Give unlimited gold (sets to 9,999,999)
//   F3  — Give unlimited research points (sets to 9,999,999)
//   F4  — Apply ALL active cheats at once
//   F5  — Reveal entire map (disable fog of war) + reveal all enemy units
//   F6  — Force-unlock all building/unit features by name
//   F7  — Toggle instant research (0-turn completion)
//
// REMOVE THIS FILE BEFORE SHIPPING.
public class DebugCheatManager : MonoBehaviour
{
    public static DebugCheatManager Instance;

    [Header("─── Master Switch ───────────────────────────────")]
    [Tooltip("If false, this component does nothing at all. Safe kill-switch.")]
    public bool cheatsEnabled = true;

    [Header("─── Cheat Toggles ───────────────────────────────")]
    [Tooltip("Unlock every TechNode for the local human player on apply.")]
    public bool cheatUnlockAllTech = true;

    [Tooltip("Force-unlock every building and unit feature by their exact string keys,\n" +
             "bypassing TechNode ScriptableObject reliance entirely.\n" +
             "Fixes cases where TechNodes are missing or have mismatched featureName strings.")]
    public bool cheatUnlockAllFeatures = true;

    [Tooltip("Set gold to 9,999,999 on apply AND clamp it every frame so it never drops.")]
    public bool cheatUnlimitedGold = true;

    [Tooltip("Set RP to 9,999,999 on apply AND clamp it every frame so it never drops.")]
    public bool cheatUnlimitedRP = true;

    [Tooltip("Also enable TechManager.freeResearchMode so researching nodes costs nothing.")]
    public bool cheatFreeResearch = true;

    [Tooltip("Make all tech nodes complete instantly regardless of their researchTurns value. Does NOT modify ScriptableObject data.")]
    public bool cheatInstantResearch = true;

    [Tooltip("Set every tile to isExplored + isVisible on apply, and hide the fog mesh/particles.\n" +
             "Re-applies automatically every turn start so FieldOfViewManager cannot undo it.\n" +
             "Also reveals all enemy unit renderers independently of tile state.")]
    public bool cheatRevealMap = true;

    [Header("─── Values ──────────────────────────────────────")]
    public int goldAmount = 9_999_999;
    public int rpAmount   = 9_999_999;

    [Header("─── Force Era ───────────────────────────────────")]
    [Tooltip("Enable this then press End Turn to jump to the chosen era and its starting turn immediately.")]
    public bool cheatForceEra = false;
    [Tooltip("Which era to jump to when cheatForceEra is active.")]
    public TurnManager.GameEra forceEraTarget = TurnManager.GameEra.Industrial;

    [Header("─── Auto-Apply ──────────────────────────────────")]
    [Tooltip("Apply all active cheats automatically on Start.")]
    public bool applyOnStart = true;

    [Tooltip("Re-apply gold/RP cheats every frame so they can never be reduced by upkeep.")]
    public bool clampEveryFrame = true;

    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        // every turn AFTER FieldOfViewManager has run, preventing the fog from
        // creeping back in. OnTurnStarted is fired at the end of StartTurn()
        // in TurnManager, after UpdateFogOfWar() has already executed.
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted += OnTurnStarted;
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    // Called every time TurnManager fires OnTurnStarted (after fog-of-war runs).
    // Waits one extra frame as a safety margin, then re-applies reveal + enemy visibility.
    // Also intercepts cheatForceEra here so "tick bool + press End Turn" works at runtime.
    private void OnTurnStarted(PlayerData currentPlayer)
    {
        if (!cheatsEnabled) return;

        // Era force — checked first so the renderer swap happens before anything else
        if (cheatForceEra)
            CheatForceEra();

        if (cheatRevealMap)
            StartCoroutine(ReapplyRevealAfterFOV());
    }

    private System.Collections.IEnumerator ReapplyRevealAfterFOV()
    {
        // One extra frame safety margin — ensures any coroutine-deferred
        // fog work in FieldOfViewManager has also finished.
        yield return null;
        CheatRevealMap();
        RevealAllEnemyUnits();
    }

    private void Start()
    {
        if (!cheatsEnabled) return;

        // GameManager.CreatePlayers() runs inside a coroutine that waits for
        // GridManager to be ready — players won't exist yet at Start().
        // We defer cheat application until the player list is actually populated.
        if (applyOnStart)
            StartCoroutine(ApplyAfterPlayersReady());
    }

    private System.Collections.IEnumerator ApplyAfterPlayersReady()
    {
        // Wait until GameManager exists and has at least one player registered
        while (GameManager.Instance == null ||
               GameManager.Instance.players == null ||
               GameManager.Instance.players.Count == 0)
            yield return null;

        // One extra frame so TurnManager.StartGame() can also finish
        yield return null;

        // Subscribe now that TurnManager is guaranteed to exist
        // (OnEnable may have fired before TurnManager.Instance was set)
        TurnManager.Instance.OnTurnStarted -= OnTurnStarted; // avoid double-subscribe
        TurnManager.Instance.OnTurnStarted += OnTurnStarted;

        // Sync freeResearchMode now that everything is initialised
        if (TechManager.Instance != null)
        {
            TechManager.Instance.freeResearchMode = cheatFreeResearch;
            TechManager.Instance.instantResearchMode = cheatInstantResearch;
        }

        ApplyAllCheats();
    }

    private void Update()
    {
        if (!cheatsEnabled) return;

        // ── Hotkeys ────────────────────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.F1)) CheatUnlockAllTech();
        if (Input.GetKeyDown(KeyCode.F2)) CheatSetGold();
        if (Input.GetKeyDown(KeyCode.F3)) CheatSetRP();
        if (Input.GetKeyDown(KeyCode.F4)) ApplyAllCheats();
        if (Input.GetKeyDown(KeyCode.F5)) { CheatRevealMap(); RevealAllEnemyUnits(); }
        if (Input.GetKeyDown(KeyCode.F6)) CheatUnlockAllFeatures();
        if (Input.GetKeyDown(KeyCode.F7)) ToggleInstantResearch();
        if (Input.GetKeyDown(KeyCode.F8)) CheatForceEra();

        // ── Per-frame clamp ────────────────────────────────────────────────
        if (!clampEveryFrame) return;

        PlayerData player = GetHumanPlayer();
        if (player == null) return;

        if (cheatUnlimitedGold && player.resources < goldAmount)
            player.resources = goldAmount;

        if (cheatUnlimitedRP && player.researchPoints < rpAmount)
            player.researchPoints = rpAmount;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Public API — call these from other scripts or Inspector context menus
    // ──────────────────────────────────────────────────────────────────────────

    [ContextMenu("Apply All Cheats")]
    public void ApplyAllCheats()
    {
        if (!cheatsEnabled) return;

        if (cheatUnlockAllTech)     CheatUnlockAllTech();
        if (cheatUnlockAllFeatures) CheatUnlockAllFeatures();
        if (cheatUnlimitedGold)     CheatSetGold();
        if (cheatUnlimitedRP)       CheatSetRP();
        if (cheatRevealMap)
        {
            CheatRevealMap();
            RevealAllEnemyUnits(); // FIX: always pair map reveal with unit reveal
        }

        if (TechManager.Instance != null)
        {
            TechManager.Instance.freeResearchMode = cheatFreeResearch;
            TechManager.Instance.instantResearchMode = cheatInstantResearch;
        }

        if (cheatForceEra) CheatForceEra();
        Debug.Log("[DebugCheatManager] All cheats applied.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Individual cheats
    // ──────────────────────────────────────────────────────────────────────────

    // Force-unlocks every TechNode asset found in the project for the human player.
    // Uses TechManager.ResearchTechForce-equivalent logic: no cost, all effects fire.
    [ContextMenu("Cheat: Unlock All Tech")]
    public void CheatUnlockAllTech()
    {
        PlayerData player = GetHumanPlayer();
        if (player == null)
        {
            Debug.LogWarning("[DebugCheatManager] CheatUnlockAllTech: Could not find human player.");
            return;
        }

        if (TechManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheatManager] CheatUnlockAllTech: TechManager not found.");
            return;
        }

        TechNode[] allNodes = Resources.FindObjectsOfTypeAll<TechNode>();
        if (allNodes.Length == 0)
        {
            Debug.LogWarning("[DebugCheatManager] CheatUnlockAllTech: No TechNodes found in Resources.");
            return;
        }

        int unlocked = 0;
        foreach (TechNode node in allNodes)
        {
            if (node == null) continue;
            if (node.IsUnlockedBy(player)) continue;

            // 1. Register the unlock
            TechManager.Instance.MarkNodeUnlocked(player, node);

            // 2. Fire effects — handle the two special cases that ActivateEffect()
            //    intentionally leaves as no-ops (they normally run inside ResearchTech).
            if (node.unlockEffects != null)
            {
                foreach (TechEffect effect in node.unlockEffects)
                {
                    if (effect == null) continue;
                    switch (effect.type)
                    {
                        // ActivateEffect() is a deliberate no-op for UnlockUnit.
                        // Unit name registration normally happens in ResearchTech() —
                        // we replicate that logic here using the explicit-player API.
                        case EffectType.UnlockUnit:
                            if (effect.targetUnits != null)
                            {
                                HashSet<string> unitNames = TechManager.Instance.GetUnlockedUnitNamesFor(player);
                                foreach (GameObject unitGO in effect.targetUnits)
                                    if (unitGO != null) unitNames.Add(unitGO.name);
                            }
                            break;

                        // UnlockFeature uses TurnManager.currentPlayer internally.
                        // Use the explicit-player overload so this is safe
                        // even if F4 is pressed during the AI's turn.
                        case EffectType.UnlockFeature:
                            TechManager.Instance.UnlockFeatureFor(player, effect.featureName);
                            break;

                        // Everything else (UpgradeInfrastructure, UpgradePlayerEra,
                        // UpgradeUnitStat, UnlockSkill) — fire normally.
                        default:
                            effect.ActivateEffect();
                            break;
                    }
                }
            }

            // 3. Sabotage tab — must set the flag in TechManager, not just refresh the button
            if (node.unlocksSabotageTab)
                TechManager.Instance.SetSabotageTabUnlocked(player);

            unlocked++;
        }

        // Refresh the entire tech tree UI so fog, lines, buttons, and sabotage tab reflect the new state.
        if (!player.isAI && TechTreeWindowManager.Instance != null)
        {
            TechTreeWindowManager.Instance.RefreshAllTechButtons();
            TechTreeWindowManager.Instance.UpdateAllLines();
            TechTreeWindowManager.Instance.RefreshAllEraFog(instant: true);
            TechTreeWindowManager.Instance.RefreshSabotageButton();
        }

        Debug.Log($"[DebugCheatManager] Unlocked {unlocked} tech node(s) for {player.playerName}.");
    }

    [ContextMenu("Cheat: Toggle Instant Research")]
    public void ToggleInstantResearch()
    {
        cheatInstantResearch = !cheatInstantResearch;
        if (TechManager.Instance != null)
            TechManager.Instance.instantResearchMode = cheatInstantResearch;
        Debug.Log($"[DebugCheatManager] Instant research: {(cheatInstantResearch ? "ON" : "OFF")}");
    }

    // <summary>Sets gold to goldAmount for the human player.</summary>
    [ContextMenu("Cheat: Set Gold")]
    public void CheatSetGold()
    {
        PlayerData player = GetHumanPlayer();
        if (player == null) return;
        player.resources = goldAmount;
        Debug.Log($"[DebugCheatManager] Gold set to {goldAmount} for {player.playerName}.");
    }

    // <summary>Sets research points to rpAmount for the human player.</summary>
    [ContextMenu("Cheat: Set RP")]
    public void CheatSetRP()
    {
        PlayerData player = GetHumanPlayer();
        if (player == null) return;
        player.researchPoints = rpAmount;
        Debug.Log($"[DebugCheatManager] RP set to {rpAmount} for {player.playerName}.");
    }

    // Sets every HexTile to isExplored = true and isVisible = true,
    // then tells HexFogRenderer to do an INSTANT teardown of all fog geometry.
    // Uses HexFogRenderer.RevealAllInstant() which destroys the mesh and stops
    // particles directly — no DOTween tweens are spawned, avoiding the pool
    // expansion flood that UpdateFog() would cause on a full map reveal.
    [ContextMenu("Cheat: Reveal Map")]
    public void CheatRevealMap()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheatManager] CheatRevealMap: GridManager not found.");
            return;
        }

        // 1. Mark every tile as explored + visible
        int count = 0;
        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
        {
            if (tile == null) continue;
            tile.isExplored = true;
            tile.isVisible  = true;
            tile.UpdateAppearance();
            count++;
        }

        if (HexFogRenderer.Instance == null)
        {
            Debug.LogWarning("[DebugCheatManager] CheatRevealMap: HexFogRenderer not found — tiles revealed but fog mesh may linger.");
            return;
        }

        // 2. Kill ALL active DOTween tweens before touching the fog renderer.
        //    This prevents any in-flight AnimateTileRise / SpawnFadeQuad tweens
        //    from fighting the instant teardown we're about to do.
        DG.Tweening.DOTween.KillAll();

        // 3. Use the instant path on HexFogRenderer — no new tweens are created.
        //    RevealAllInstant() destroys fog mesh quads and particles directly
        //    and clears previouslyUnexplored so the next UpdateFog() won't
        //    misidentify all tiles as newly revealed.
        HexFogRenderer.Instance.RevealAllInstant();

        Debug.Log($"[DebugCheatManager] Revealed {count} tile(s). Fog disabled (instant, no tweens).");
    }

    // Force-shows all enemy unit GameObjects and their Renderers.
    // PlayerData does not store a unit list — units are registered with
    // TurnManager. We use TurnManager.GetAllUnits() and filter by owner.isAI.
    // The fog system hides enemy units by toggling GameObjects or Renderers
    // independently of tile visibility state, so CheatRevealMap() alone is not
    // enough to make enemies visible. This method re-enables the GameObject and
    // all child Renderers for every unit owned by an AI player.
    // Called automatically after CheatRevealMap() and after every turn start
    // via OnTurnStarted → ReapplyRevealAfterFOV(), so enemies stay visible
    // even after FieldOfViewManager re-evaluates fog.
    [ContextMenu("Cheat: Reveal All Enemy Units")]
    public void RevealAllEnemyUnits()
    {
        if (TurnManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheatManager] RevealAllEnemyUnits: TurnManager not found.");
            return;
        }

        int revealed = 0;

        // FIX: Units are stored in TurnManager.allUnits, not on PlayerData.
        // Filter by owner.isAI to target only enemy units.
        foreach (Unit unit in TurnManager.Instance.GetAllUnits())
        {
            if (unit == null) continue;
            if (unit.owner == null || !unit.owner.isAI) continue;

            // Re-activate the GameObject itself in case fog hid the whole object
            if (!unit.gameObject.activeSelf)
                unit.gameObject.SetActive(true);

            // Re-enable every Renderer in the hierarchy (includes LODs, decals, etc.)
            foreach (Renderer r in unit.GetComponentsInChildren<Renderer>(includeInactive: true))
                r.enabled = true;

            revealed++;
        }

        Debug.Log($"[DebugCheatManager] Revealed {revealed} enemy unit(s).");
    }

    // Force-unlocks all building features and unit names using their exact string keys
    // as defined in BuildingUIManager — hardcoded here so this is guaranteed to work
    // even if TechNode ScriptableObjects are missing entries or have mismatched strings.
    //
    // IMPORTANT: These strings must stay in sync with BuildingUIManager.cs.
    //   Building features → ShowHQConstruct() → TryAddStructureButton() calls
    //   Unit names        → ShowHQDeploy(), ShowServiceCenter(), ShowCanteen()
    [ContextMenu("Cheat: Unlock All Features")]
    public void CheatUnlockAllFeatures()
    {
        PlayerData player = GetHumanPlayer();
        if (player == null)
        {
            Debug.LogWarning("[DebugCheatManager] CheatUnlockAllFeatures: Could not find human player.");
            return;
        }

        if (TechManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheatManager] CheatUnlockAllFeatures: TechManager not found.");
            return;
        }

        // ── Building feature keys ─────────────────────────────────────────────
        // Must match exactly what BuildingUIManager.ShowHQConstruct() passes to
        // TryAddStructureButton(). If you add a new building, add its key here too.
        string[] buildingFeatures = new string[]
        {
            "TelecomTowers",
            "ServiceCenter",
            "AdvancedServiceCenter",
            "BPOCenters",
            "CommercialHubs",
            "BusinessCenters",
            "AdvancedBusinessCenters",
            "WorkerFactories",
            "DroneFactories",
            "SignalBooster",
            "SignalJammers",
            "PowerBoxes",
            "Tesseract",
            "Canteens",
            "Rocketship",
        };

        // ── Unit name keys ────────────────────────────────────────────────────
        // Must match exactly what BuildingUIManager passes as techUnlockName to
        // TryAddUnitButton() in ShowHQDeploy(), ShowServiceCenter(), ShowCanteen().
        string[] unitNames = new string[]
        {
            "Builder",
            "WireSpecialist",   
            "Scout",
            "Technician",
            "Businessman",
            "SalesMarketer",
            "Saboteur",
            "RoboWorker",
            "RoboMarshall",
            "MaintenanceCrew",
            "Foreman",
            "ITPersonnel",      
        };

        HashSet<string> featureSet = TechManager.Instance.GetOrCreateFeatureSetFor(player);
        HashSet<string> unitSet    = TechManager.Instance.GetUnlockedUnitNamesFor(player);

        foreach (string f in buildingFeatures) featureSet.Add(f);
        foreach (string u in unitNames)        unitSet.Add(u);

        Debug.Log($"[DebugCheatManager] Force-unlocked {buildingFeatures.Length} building features " +
                  $"and {unitNames.Length} unit types for {player.playerName}.");
    }

    // Forces the world era and sets currentTurn to the first turn of that era.
    // Fires immediately — era UI, renderer features, and announcements all update
    // on the NEXT turn start (i.e. press End Turn once after enabling the bool).
    [ContextMenu("Cheat: Force Era")]
    public void CheatForceEra()
    {
        if (TurnManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheatManager] CheatForceEra: TurnManager not found.");
            return;
        }

        // Map each era to the first turn of that era bracket
        int targetTurn;
        switch (forceEraTarget)
        {
            case TurnManager.GameEra.Industrial:    targetTurn = 1;  break;
            case TurnManager.GameEra.EarlyEighties: targetTurn = 26; break;
            case TurnManager.GameEra.Retro:         targetTurn = 51; break;
            case TurnManager.GameEra.Futuristic:    targetTurn = 76; break;
            default:                                targetTurn = 1;  break;
        }

        TurnManager.Instance.currentTurn = targetTurn;

        // Force UpdateEra() via reflection since it is private —
        // UpdateEra() reads currentTurn, sets currentEra, and fires the
        // announcement + EraRendererController via OnTurnStarted.
        var method = typeof(TurnManager).GetMethod(
            "UpdateEra",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(TurnManager.Instance, null);
        }
        else
        {
            Debug.LogWarning("[DebugCheatManager] CheatForceEra: Could not reflect UpdateEra(). " +
                             "Era turn set but era enum not updated yet.");
        }

        // Sync EraRendererController so the correct renderer feature activates immediately
        if (EraRendererController.Instance != null)
            EraRendererController.Instance.ForceSync();

        // Force the announcement sequence — bypasses _isPlaying guard so it
        // always plays even if a previous announcement hasn't finished yet
        if (EraAnnouncementController.Instance != null)
            EraAnnouncementController.Instance.ForceTriggerAnnouncement(forceEraTarget);

        Debug.Log($"[DebugCheatManager] Era forced to {forceEraTarget} " +
                  $"(turn set to {targetTurn}). Announcement triggered immediately.");

        // Auto-disable so it doesn't keep re-firing every turn
        cheatForceEra = false;
    }

    // Returns the human player (isAI = false) from GameManager.players.
    // GameManager always creates players as: players[0] = Player 1 (human), players[1] = Enemy AI.
    private PlayerData GetHumanPlayer()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheatManager] GameManager.Instance is null.");
            return null;
        }

        List<PlayerData> players = GameManager.Instance.players;
        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("[DebugCheatManager] GameManager.players is empty — players may not be created yet.");
            return null;
        }

        foreach (PlayerData p in players)
        {
            if (p != null && !p.isAI) return p;
        }

        Debug.LogWarning("[DebugCheatManager] No human player found in GameManager.players.");
        return null;
    }
}