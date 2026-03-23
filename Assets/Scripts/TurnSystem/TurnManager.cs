using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

// One era → one color mapping, shown as a dropdown + color picker in the Inspector.
[System.Serializable]
public class EraColorEntry
{
    public TurnManager.GameEra era;
    public Color color = Color.white;
}

// An Image that should recolor whenever the world era matches one of its entries.
[System.Serializable]
public class EraIconTint
{
    public Image icon;
    [Tooltip("Add one entry per era you want to recolor this icon. " +
             "Eras not listed leave the icon color unchanged.")]
    public EraColorEntry[] eraColors;
}

[System.Serializable]
public class EraSpriteSwap
{
    public TurnManager.GameEra triggerEra;
    public List<ImageSpriteSwap> swaps;
    public TextMeshProUGUI[] textTargets;
    public Color textColor;
}

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    //  WORLD ERA  (advances every 25 turns, shared by all players)
    public enum GameEra { Industrial, EarlyEighties, Retro, Futuristic }
    public GameEra currentEra { get; private set; }

    //  PLAYER ERA  (per-player tech levels — stored on PlayerData)
    public enum PlayerEra { Industrial, EarlyEighties, Retro, Futuristic }

    public int currentTurn { get; set; } = 1;
    public const int MAX_TURNS = 100;

    public PlayerData currentPlayer { get; private set; }

    public List<PlayerData> players = new List<PlayerData>();
    public int currentPlayerIndex { get; private set; }

    private List<Unit>          allUnits      = new List<Unit>();
    private List<TowerNode>     allTowers     = new List<TowerNode>();
    private List<WireNode>      allWires      = new List<WireNode>();
    private List<StructureNode> allStructures = new List<StructureNode>();

    public event System.Action OnGameStatusChanged;
    public event System.Action<GameEra> OnEraChanged;

    // Fired at the start of every player's turn, AFTER fog-of-war has been
    // updated. DebugCheatManager subscribes to this so it can re-apply the map
    // reveal + enemy unit visibility after FieldOfViewManager runs each turn.
    public event System.Action<PlayerData> OnTurnStarted;

    [Header("Era Sprite Swaps")]
    [Tooltip("Each entry binds a GameEra to Image + replacement Sprite pairs. " +
             "Swaps fire at the same moment OnEraChanged fires.")]
    [SerializeField] private EraSpriteSwap[] eraSpriteSwaps;

    [Header("Era Icon Tints")]
    [Tooltip("Assign any Image icon here, then define what color it should be in each era. " +
             "The color updates instantly whenever the world era changes.")]
    [SerializeField] private EraIconTint[] eraIconTints;

    [Header("Futuristic Era – Rotating Image")]
    [Tooltip("GameObject with an Image component. Rotates on Z and pulses scale when the world era is Futuristic.")]
    [SerializeField] private GameObject futuristicRotatingObject;
    [Tooltip("Degrees per second for the Z rotation.")]
    [SerializeField] private float futuristicRotationSpeed = 30f;
    [Tooltip("Minimum scale during the pulse (base).")]
    [SerializeField] private float futuristicScaleMin = 1f;
    [Tooltip("Maximum scale during the pulse.")]
    [SerializeField] private float futuristicScaleMax = 1.05f;
    [Tooltip("How many full pulse cycles per second.")]
    [SerializeField] private float futuristicPulseSpeed = 1f;

    [Header("Turn Indicator – Hover Button")]
    [Tooltip("Button whose image tints green on hover when it is the player's turn, red during an AI turn.")]
    [SerializeField] private Button turnIndicatorButton;
    [Tooltip("Tint applied on hover during the human player's turn.")]
    [SerializeField] private Color hoverPlayerColor = new Color(0.2f, 0.9f, 0.2f, 1f);
    [Tooltip("Tint applied on hover during an AI turn.")]
    [SerializeField] private Color hoverEnemyColor  = new Color(0.9f, 0.2f, 0.2f, 1f);
    [Tooltip("How quickly the tint fades in and out (seconds).")]
    [SerializeField] private float hoverFadeDuration = 0.15f;

    // Runtime state for the hover button
    private Image  _turnButtonImage;
    private Color  _turnButtonBaseColor;
    private Coroutine _hoverFadeCoroutine;

    public void NotifyStatusChanged()
    {
        OnGameStatusChanged?.Invoke();
    }

    //  LIFECYCLE
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // GameManager handled LoadGame logic in its SetupGame coroutine

        // ---- Turn Indicator Button hover setup ----
        if (turnIndicatorButton != null)
        {
            _turnButtonImage     = turnIndicatorButton.GetComponent<Image>();
            _turnButtonBaseColor = _turnButtonImage != null ? _turnButtonImage.color : Color.white;

            var trigger = turnIndicatorButton.GetComponent<EventTrigger>()
                       ?? turnIndicatorButton.gameObject.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ => OnTurnButtonHoverEnter());
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ => OnTurnButtonHoverExit());
            trigger.triggers.Add(exitEntry);
        }
    }

    private void Update()
    {
        // ---- Futuristic rotating image ----
        if (futuristicRotatingObject != null && currentEra == GameEra.Futuristic)
        {
            // Z rotation
            futuristicRotatingObject.transform.Rotate(
                0f, 0f, futuristicRotationSpeed * Time.deltaTime);

            // Scale pulse — Mathf.Sin oscillates -1..1, remap to min..max
            float t      = (Mathf.Sin(Time.time * futuristicPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            float scale  = Mathf.Lerp(futuristicScaleMin, futuristicScaleMax, t);
            futuristicRotatingObject.transform.localScale = Vector3.one * scale;
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

    //  TURN FLOW
    void StartTurn()
    {
        currentPlayer = players[currentPlayerIndex];

        // CLEANUP UI
        // Ensure no old menus are stuck open from the previous player
        if (BuildingUIManager.Instance != null) 
            BuildingUIManager.Instance.Close();

        // SIGNAL PROPAGATION
        // Propagate signal from every player's HQs before influence is recalculated,
        // so that towers have up-to-date receivedSignalStrength values.
        foreach (PlayerData p in players)
        {
            foreach (SignalNode node in p.ownedNodes)
            {
                if (node != null)
                    node.PropagateSignal();
            }
        }

        // CALCULATE GLOBAL STATE 
        if (InfluenceManager.Instance != null)
        {
            // DECAY PERSISTENT SUPPRESSION
            if (GridManager.Instance != null)
            {
                foreach (var tile in GridManager.Instance.GetAllTiles())
                {
                    if (tile.influenceSuppression > 0)
                    {
                        tile.influenceSuppression = Mathf.Max(0, tile.influenceSuppression - 2);
                    }
                }
            }

            InfluenceManager.Instance.RecalculateGlobalInfluence(players);
        }
        else
        {
            Debug.LogError("TurnManager: Missing InfluenceManager!");
        }

        // PROCESS INCOME
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.ProcessTurnIncome(currentPlayer);
        }
        else
        {
            Debug.LogError("TurnManager: Missing EconomyManager!");
        }

        // --- RESEARCH TICK ---
        // Decrements in-progress research counters for the current player.
        // Any tech whose counter reaches 0 is completed here (effects fire,
        // UI is notified) before the player takes their actions this turn.
        if (TechManager.Instance != null)
        {
            TechManager.Instance.TickResearch(currentPlayer);
        }

        // --- PHASE 3: RESEARCH PROCESSING ---
        if (ResearchProjectHandler.Instance != null)
        {
            ResearchProjectHandler.Instance.OnTurnEnd(currentPlayer);
        }

        // --- PHASE 1: GLOBAL EVENTS & HAZARDS ---
        if (EventManager.Instance != null)
            EventManager.Instance.ProcessTurnEvents();
            
        if (currentPlayerIndex == 0 && HazardManager.Instance != null)
            HazardManager.Instance.ProcessTurnHazards();
        // ----------------------------------------
        
        // UPDATE GAME WORLD 
        OnGameStatusChanged?.Invoke();

        // Iterate a copy so destroyed units can be safely removed mid-loop
        for (int i = allUnits.Count - 1; i >= 0; i--)
        {
            if (allUnits[i] == null) { allUnits.RemoveAt(i); continue; }
            allUnits[i].OnTurnStart(currentPlayer);
        }

        // TOWER DECAY
        foreach (TowerNode tower in allTowers)
        {
            if (tower.owner == currentPlayer) 
            {
                tower.ProcessTurnDecay();
            }
        }

        // WIRE DECAY
        for (int i = allWires.Count - 1; i >= 0; i--)
        {
            WireNode wire = allWires[i];
            
            if (wire == null) 
            {
                allWires.RemoveAt(i);
                continue;
            }

            if (wire.owner == currentPlayer)
            {
                wire.DecayWire();
            }
        }

        // STRUCTURE TURN START
        for (int i = allStructures.Count - 1; i >= 0; i--)
        {
            if (allStructures[i] == null) { allStructures.RemoveAt(i); continue; }
            if (allStructures[i].owner == currentPlayer)
            {
                allStructures[i].OnTurnStart();
            }
        }

        // UPDATE FOG OF WAR (Always for the human player)
        if (FieldOfViewManager.Instance != null)
        {
            FieldOfViewManager.Instance.UpdateFogOfWar(players[0]);
        }

        // Fire OnTurnStarted AFTER fog-of-war has been updated.
        OnTurnStarted?.Invoke(currentPlayer);

        // FINAL STEP: Update borders now that EVERYTHING (Fog of War & Influence) is ready
        if (InfluenceBorderRenderer.Instance != null)
        {
            InfluenceBorderRenderer.Instance.UpdateBorders();
        }

        // CAMERA TRACKING
        HandleCameraFocus(currentPlayer);

        // AI EXECUTION
        if (currentPlayer.isAI && EnemyAI.Instance != null)
        {
            EnemyAI.Instance.ExecuteTurn(currentPlayer);
        }
    }

    private void HandleCameraFocus(PlayerData player)
    {
        if (CameraController.Instance == null) return;

        // Release Cutscene Mode regardless of player type
        // This stops the camera from sliding to the AI automatically.
        CameraController.Instance.cutsceneMode = false;
        
        if (!player.isAI)
        {
            Vector3 focusPoint = GetPlayerFocusPoint(player);
            CameraController.Instance.FocusOnPosition(focusPoint);
        }
    }

    private Vector3 GetPlayerFocusPoint(PlayerData player)
    {
        Vector3 averagePos = Vector3.zero;
        int count = 0;

        // Collect all owned items for THIS SPECIFIC player
        // Null check guards against units destroyed mid-turn (e.g. BuilderUnit running out of charges)
        foreach (var unit in allUnits)
        {
            if (unit == null) continue;
            if (unit.owner == player)
            {
                averagePos += unit.transform.position;
                count++;
            }
        }
        foreach (var tower in allTowers)
        {
            if (tower.owner == player)
            {
                averagePos += tower.transform.position;
                count++;
            }
        }

        // Also include HQs (SignalNodes) in the calculation
        foreach (var node in player.ownedNodes)
        {
            if (node != null)
            {
                averagePos += node.transform.position;
                count++;
            }
        }

        if (count > 0)
        {
            return averagePos / count;
        }
        
        return CameraController.Instance.transform.position; 
    }

    //  ACCESSORS
    public List<Unit>          GetAllUnits()      => allUnits;
    public List<TowerNode>     GetAllTowers()     => allTowers;   
    public List<WireNode>      GetAllWires()      => allWires;    
    public List<StructureNode> GetAllStructures() => allStructures;
    public List<PlayerData>    GetPlayers()       => players;

    //  END TURN
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

    private void OnApplicationQuit()
    {
        SaveSystem.SaveGame();
    }

    public void ResumeFromSave(int playerIndex)
    {
        currentPlayerIndex = playerIndex;
        UpdateEra(true); // Force era update to trigger sounds/UI on load
        StartTurn();
        NotifyStatusChanged();
    }

    public void UpdateEra(bool force = false)
    {
        GameEra newEra;
        if      (currentTurn > 75) newEra = GameEra.Futuristic;
        else if (currentTurn > 50) newEra = GameEra.Retro;
        else if (currentTurn > 25) newEra = GameEra.EarlyEighties;
        else                       newEra = GameEra.Industrial;

        // Only fire announcement when era actually changes
        bool eraChanged = (newEra != currentEra) || force;
        currentEra = newEra;

        Debug.Log($"Game Era: {currentEra}");

        if (eraChanged)
        {
            // Update visuals to match the new global era
            foreach (var tower in GetAllTowers()) tower?.UpdateEraVisuals();
            foreach (var structNode in GetAllStructures())
                if (structNode is Canteen canteen) canteen.UpdateEraVisuals();
            if (GridManager.Instance != null) GridManager.Instance.RefreshEraBuildings(currentEra);

            if (FeedbackController.Instance != null)
                FeedbackController.Instance.PlayEraTransition(currentEra.ToString());

            if (EraAnnouncementController.Instance != null)
                EraAnnouncementController.Instance.TriggerAnnouncement(currentEra);

            OnEraChanged?.Invoke(currentEra);
            ApplyEraSpriteSwaps(currentEra);
            ApplyEraIconTints(currentEra);
        }
    }

    // -------------------------------------------------------------------------
    // TURN INDICATOR BUTTON HOVER
    // -------------------------------------------------------------------------
    private void OnTurnButtonHoverEnter()
    {
        if (_turnButtonImage == null) return;
        Color target = (currentPlayer != null && !currentPlayer.isAI)
            ? hoverPlayerColor
            : hoverEnemyColor;

        if (_hoverFadeCoroutine != null) StopCoroutine(_hoverFadeCoroutine);
        _hoverFadeCoroutine = StartCoroutine(FadeButtonColor(_turnButtonImage, _turnButtonImage.color, target, hoverFadeDuration));
    }

    private void OnTurnButtonHoverExit()
    {
        if (_turnButtonImage == null) return;

        if (_hoverFadeCoroutine != null) StopCoroutine(_hoverFadeCoroutine);
        _hoverFadeCoroutine = StartCoroutine(FadeButtonColor(_turnButtonImage, _turnButtonImage.color, _turnButtonBaseColor, hoverFadeDuration));
    }

    private System.Collections.IEnumerator FadeButtonColor(Image img, Color from, Color to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            img.color = Color.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        img.color = to;
    }

    // -------------------------------------------------------------------------
    private void ApplyEraSpriteSwaps(GameEra era)
    {
        if (eraSpriteSwaps == null) return;
        foreach (var entry in eraSpriteSwaps)
        {
            if (entry == null || entry.triggerEra != era) continue;
            if (entry.swaps != null)
            {
                foreach (var swap in entry.swaps)
                {
                    if (swap == null) continue;
                    if (swap.targetImage != null && swap.newSprite != null)
                        swap.targetImage.sprite = swap.newSprite;
                }
            }
            if (entry.textTargets != null)
            {
                foreach (var tmp in entry.textTargets)
                    if (tmp != null) tmp.color = entry.textColor;
            }
        }
    }

    private void ApplyEraIconTints(GameEra era)
    {
        if (eraIconTints == null) return;
        foreach (var entry in eraIconTints)
        {
            if (entry == null || entry.icon == null || entry.eraColors == null) continue;
            foreach (var eraColor in entry.eraColors)
            {
                if (eraColor.era == era)
                {
                    entry.icon.color = eraColor.color;
                    break;
                }
            }
        }
    }

    void CheckGameEnd()
    {
        if (currentTurn > MAX_TURNS)
        {
            PlayerData winner = InfluenceManager.Instance.GetWinner();
            int winningScore = InfluenceManager.Instance.GetTotalInfluence(winner);
            
            Debug.Log($"Game Over! Turn Limit Reached.");
            Debug.Log($"WINNER: {winner.playerName} with {winningScore} Influence!");
        }
    }

    //  REGISTRATION
    public void RegisterUnit(Unit unit)
    {
        if (!allUnits.Contains(unit))
            allUnits.Add(unit);
    }

    // Call this before Destroy(gameObject) on any unit so it is removed from
    // the allUnits list. Prevents MissingReferenceException in GetPlayerFocusPoint.
    public void UnregisterUnit(Unit unit)
    {
        allUnits.Remove(unit);
    }

    public void RegisterTower(TowerNode tower)
    {
        if (!allTowers.Contains(tower))
            allTowers.Add(tower);
    }

    public void RegisterWire(WireNode wire)
    {
        if (!allWires.Contains(wire))
            allWires.Add(wire);
    }

    public void RegisterStructure(StructureNode structure)
    {
        if (!allStructures.Contains(structure))
            allStructures.Add(structure);
    }

    public void UnregisterStructure(StructureNode structure)
    {
        allStructures.Remove(structure);
    }

    public GameEra GetCurrentEra()
    {
        return currentEra;
    }

    //  ERA COMPARISON HELPERS  (System 1)
    // True when the World Era is ahead of the player's Hardware Era.
    // Triggers the obsolete-tech influence debuff.
    public bool IsHardwareObsolete(PlayerData player)
    {
        return (int)currentEra > (int)player.hardwareEra;
    }

    // True when the player's Hardware Era is ahead of their Workforce Era.
    // Triggers the unskilled-labor upkeep penalty.
    public bool HasLaborMismatch(PlayerData player)
    {
        return (int)player.hardwareEra > (int)player.workforceEra;
    }

    // Returns the influence generation multiplier for a player based on how
    // many eras their hardware lags behind the World Era.
    // Gap of 0 → 1.0 (no penalty).  Each era gap → −25 %, floored at 25 %.
    public float GetEraInfluenceMultiplier(PlayerData player)
    {
        int eraGap = (int)currentEra - (int)player.hardwareEra;
        if (eraGap <= 0) return 1.0f;

        return Mathf.Max(0.25f, 1f - eraGap * 0.25f);
    }

    // Returns the upkeep cost multiplier caused by a hardware/workforce era mismatch.
    // Gap of 0 → 1.0 (no penalty).  Each era gap → +50 % upkeep.
    public float GetUpkeepMultiplier(PlayerData player)
    {
        int eraGap = (int)player.hardwareEra - (int)player.workforceEra;
        if (eraGap <= 0) return 1.0f;

        return 1f + eraGap * 0.5f;
    }
}