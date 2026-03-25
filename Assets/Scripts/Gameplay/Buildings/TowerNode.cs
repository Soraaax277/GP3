using UnityEngine;

public class TowerNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile ParentTile => tile;
    public bool isBuilderFinished;
    public bool IsPowered { get; set; }
    
    public bool IsTechnicianActivated 
    { 
        get => false; // Towers are no longer activated by technicians, only wires.
        set { } 
    }

    public enum TowerState
    {
        Hologram,       // Just placed — digital ghost
        Constructed,    // Built by a Builder, but no grid power yet
        Powered,        // Builder built + Grid power arrives — 100% Signal & Solid Gray
        Destroyed       // Needs repair
    }

    public PlayerData owner      { get; private set; }
    public SignalNode parentNode { get; private set; }
    public HexTile    tile;
    
    [Header("Stats")]
    public int   baseRange      = 1; // 7-tile cluster (center + neighbors)
    public int   baseRevenue    = 10; 
    public float baseDurability = 100f;
    public int   visionRange    = 3;

    [Header("Upkeep")]
    public int goldUpkeep = 25;
    
    private bool isRecruited = false;
    public float receivedSignalStrength { get; set; } = 0f;

    public int CurrentRange
    {
        get
        {
            if (TechManager.Instance == null) return baseRange;
            float bonus = TechManager.Instance.GetInfraFlatBonus(owner, "TowerRange");
            float multiplier = TechManager.Instance.GetInfraMultiplier(owner, "TowerRange");
            if (multiplier <= 0) multiplier = 1f;

            // CAPPED for balance: Prevent a single tower from taking over the map via tech stacking.
            int maxAllowedRange = 3;
            int calculated = Mathf.RoundToInt((baseRange + bonus) * multiplier);
            return Mathf.Clamp(calculated, 1, maxAllowedRange);
        }
    }

    public int CurrentRevenue
    {
        get
        {
            if (TechManager.Instance == null) return baseRevenue;
            float multiplier = TechManager.Instance.GetInfraMultiplier(owner, "TowerRevenue");
            if (multiplier <= 0) multiplier = 1f;
            return Mathf.RoundToInt(baseRevenue * multiplier);
        }
    }
    
    public float currentDurability;

    [Header("Era Visuals")]
    public GameObject industrialVisual;
    public GameObject early80sVisual;
    public GameObject retroVisual;
    public GameObject futuristicVisual;
    private GameObject currentVisualObj;
    public void UpdateEraVisuals()
    {
        if (TurnManager.Instance == null) return;
        TurnManager.GameEra era = TurnManager.Instance.GetCurrentEra();

        // 1. Turn OFF all visuals
        if (industrialVisual != null) industrialVisual.SetActive(false);
        if (early80sVisual != null) early80sVisual.SetActive(false);
        if (retroVisual != null) retroVisual.SetActive(false);
        if (futuristicVisual != null) futuristicVisual.SetActive(false);

        // 2. Turn ON the matching era visual
        GameObject activeVisual = industrialVisual;
        if (era == TurnManager.GameEra.EarlyEighties && early80sVisual != null) activeVisual = early80sVisual;
        else if (era == TurnManager.GameEra.Retro && retroVisual != null) activeVisual = retroVisual;
        else if (era == TurnManager.GameEra.Futuristic && futuristicVisual != null) activeVisual = futuristicVisual;

        if (activeVisual != null) 
        {
            activeVisual.SetActive(true);
            foreach (var col in activeVisual.GetComponentsInChildren<Collider>())
                Destroy(col);
        }

        // 3. Apply hologram/solid state to everything currently active
        if (state == TowerState.Hologram)

            HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
        else
            HologramUtil.MakeSolid(gameObject);
    }

    private TowerState _stateCache;
    public TowerState state { get; private set; }
    private GameObject rangeIndicator;
    private Renderer[] _cachedRenderers;

    void IInfrastructure.Initialize(HexTile hexTile, PlayerData player) 
        => Initialize(hexTile, player, null);

    public void Initialize(HexTile hexTile, PlayerData player, SignalNode parent = null)
    {
        tile       = hexTile;
        owner      = player;
        parentNode = parent;
        tile.placedTower = this;
        currentDurability = baseDurability;

        if (parentNode != null) parentNode.towersPlacedCount++;
        TurnManager.Instance.RegisterTower(this);

        isBuilderFinished = false;
        state = TowerState.Hologram;

        UpdateEraVisuals();
        CreateRangeIndicator();
        SetRangeColor(new Color(0f, 0.5f, 1f, 0.25f));
        ShowRange(false);

        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
    }

    private void CacheRenderers()
    {
        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            _cachedRenderers = GetComponentsInChildren<Renderer>();
    }

    public void Build()
    {
        if (isBuilderFinished || state == TowerState.Destroyed) return;

        isBuilderFinished = true;
        Debug.Log($"[Tower {name}] Builder finished construction. Awaiting power from wires to solidify.");

        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
    }

    public void UpdatePowerState(bool powered)
    {
        IsPowered = powered;
        CacheRenderers();

        TowerState newState = state;
        if (state != TowerState.Destroyed)
        {
            if (isBuilderFinished && powered) newState = TowerState.Powered;
            else if (isBuilderFinished) newState = TowerState.Constructed;
            else newState = TowerState.Hologram;
        }

        if (newState != state)
        {
            RemoveInfluence();
            state = newState;
            ApplyInfluence();

            if (state == TowerState.Powered)
            {
                HologramUtil.MakeSolid(gameObject);
                SetRangeColor(new Color(0f, 1f, 0f, 0.4f)); 
            }
            else if (state == TowerState.Constructed)
            {
                // Built but unpowered: Solid grey/white, but not the green pulse
                HologramUtil.MakeSolid(gameObject);
                SetRangeColor(new Color(0.5f, 0.5f, 0.5f, 0.25f));
            }
            else if (state == TowerState.Destroyed)
            {
                // Red/Broken visual handled by DestroyTower() usually, but for safety:
                SetRangeColor(new Color(1f, 0f, 0f, 0.25f));
            }
            else // Hologram
            {
                HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
                SetRangeColor(new Color(0f, 0.5f, 1f, 0.15f));
            }
        }
    }

    public int GetCurrentUpkeep()
    {
        return state == TowerState.Destroyed ? 0 : goldUpkeep;
    }

    public void Power() { } // Unused for towers in this new mechanic

    private float GetStateInfluenceMultiplier()
    {
        switch (state)
        {
            case TowerState.Hologram:    return 0.05f;
            case TowerState.Constructed: return 0.20f;
            case TowerState.Powered:     return 1.00f;
            default:                     return 0f;
        }
    }

    void ApplyInfluence()
    {
        if (state == TowerState.Destroyed) return;
        float stateMultiplier = GetStateInfluenceMultiplier();
        if (stateMultiplier <= 0f) return;

        float eraMultiplier = 1f;
        if (TurnManager.Instance != null && owner != null)
            eraMultiplier = TurnManager.Instance.GetEraInfluenceMultiplier(owner);

        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, CurrentRange);
        foreach (HexTile t in tilesInRange)
        {
            float influenceAmount = (state == TowerState.Powered && receivedSignalStrength > 0f) 
                ? receivedSignalStrength * stateMultiplier * eraMultiplier
                : t.baseInfluence * stateMultiplier * eraMultiplier;

            t.AddInfluence(owner, Mathf.RoundToInt(influenceAmount));
        }

        if (TurnManager.Instance != null) TurnManager.Instance.NotifyStatusChanged();
    }

    void RemoveInfluence()
    {
        if (state == TowerState.Hologram || state == TowerState.Destroyed) return;
        float stateMultiplier = GetStateInfluenceMultiplier();
        if (stateMultiplier <= 0f) return;

        float eraMultiplier = 1f;
        if (TurnManager.Instance != null && owner != null)
            eraMultiplier = TurnManager.Instance.GetEraInfluenceMultiplier(owner);

        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, CurrentRange);
        foreach (HexTile t in tilesInRange)
        {
            float amountToRemove = (state == TowerState.Powered && receivedSignalStrength > 0f)
                ? receivedSignalStrength * stateMultiplier * eraMultiplier
                : t.baseInfluence * stateMultiplier * eraMultiplier;

            t.RemoveInfluence(owner, Mathf.RoundToInt(amountToRemove));
        }
        if (TurnManager.Instance != null) TurnManager.Instance.NotifyStatusChanged();
    }

    public void TakeDamage(float amount)
    {
        if (state == TowerState.Hologram || state == TowerState.Destroyed) return;
        currentDurability -= amount;
        if (currentDurability <= 0) DestroyTower();
    }

    public void ProcessTurnDecay()
    {
        if (state == TowerState.Hologram || state == TowerState.Destroyed) return;
        float decayPercent = 0.50f;
        if (TechManager.Instance != null)
            decayPercent -= TechManager.Instance.GetInfraFlatBonus(owner, "WireDegradation");

        decayPercent = Mathf.Max(0.05f, decayPercent);
        float resistance = 1.0f;
        if (TechManager.Instance != null)
            resistance = TechManager.Instance.GetInfraMultiplier(owner, "TowerDurability");

        if (resistance < 1.0f) resistance = 1.0f;
        currentDurability -= (baseDurability * decayPercent) / resistance;

        if (currentDurability <= 0) DestroyTower();
    }

    public void Repair(float efficiencyMultiplier = 1.0f)
    {
        if (state != TowerState.Destroyed) return;
        isBuilderFinished = true; // Repaired implies built
        state = TowerState.Constructed;
        HologramUtil.MakeSolid(gameObject);
        currentDurability = Mathf.Min(baseDurability * efficiencyMultiplier, baseDurability);
        ApplyInfluence();
        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
    }

    void DestroyTower()
    {
        state = TowerState.Destroyed;
        IsPowered = false;
        
        // JUICE (Phase 2)
        if (FeedbackController.Instance != null)
            FeedbackController.Instance.PlayTowerDestroyed(transform.position);

        ShowRange(false);
        SetRangeColor(new Color(1f, 0f, 0f, 0.25f));
    }

    public void SetBuilt() { isBuilderFinished = true; state = TowerState.Constructed; }
    public bool IsBuilt() => isBuilderFinished || state == TowerState.Powered;
    public bool IsDestroyed() => state == TowerState.Destroyed;

    // Reverts this tower to Hologram/blueprint state.
    // Called by EnemyAI.PlaceBlueprint() after PlaceTowerDirect() — which
    // initialises towers as solid/built — so Builder units can still find it
    // via GetUnbuiltTowers() and physically construct it next turn.
    // Mirrors exactly what Initialize() sets up for a fresh hologram.
    public void SetHologramState()
    {
        isBuilderFinished = false;
        state              = TowerState.Hologram;
        IsPowered          = false;
        HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
        SetRangeColor(new Color(0f, 0.5f, 1f, 0.15f));
        ShowRange(false);
        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
    }
    //  RANGE INDICATOR HELPERS
    public void CreatePreview()
    {
        // Set state first so UpdateEraVisuals knows to tint as hologram
        state = TowerState.Hologram;
        // Spawn the correct era visual and tint it as a hologram
        UpdateEraVisuals();
        CreateRangeIndicator();
        ShowRange(true);
    }

    public void SetRangeColor(Color color) { if (rangeIndicator != null) rangeIndicator.GetComponent<Renderer>().material.color = color; }
    public void ShowRange(bool show) { if (rangeIndicator != null) { if (show) UpdateRangeVisuals(); rangeIndicator.SetActive(show); } }

    // Snaps the range indicator's Y to just above the given tile's surface.
    // Call every frame during placement preview so the circle stays on the ground.
    public void SetRangeIndicatorToSurface(HexTile targetTile)
    {
        if (rangeIndicator == null || targetTile == null) return;

        BoxCollider box = targetTile.GetComponent<BoxCollider>();
        float surfaceY  = targetTile.transform.position.y;
        if (box != null)
        {
            float halfHeight = box.size.y * 0.5f * targetTile.transform.lossyScale.y;
            float centerY    = box.center.y * targetTile.transform.lossyScale.y;
            surfaceY         = targetTile.transform.position.y + centerY + halfHeight + 0.05f;
        }

        Vector3 pos = rangeIndicator.transform.position;
        pos.y = surfaceY;
        rangeIndicator.transform.position = pos;
    }
    
    void CreateRangeIndicator()
    {
        if (rangeIndicator != null) return;
        rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeIndicator.transform.SetParent(transform);
        rangeIndicator.transform.localRotation = Quaternion.identity;

        // Place the indicator at the tile's surface rather than relative to the
        // tower pivot (which may be at the tip). Tile BoxCollider gives us the
        // exact world Y of the top face; we convert that to the tower's local Y.
        float worldSurfaceY = GetTileSurfaceY();
        float localY = transform.InverseTransformPoint(0f, worldSurfaceY + 0.05f, 0f).y;
        rangeIndicator.transform.localPosition = new Vector3(0f, localY, 0f);

        UpdateRangeVisuals();
        Renderer rend = rangeIndicator.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        Destroy(rangeIndicator.GetComponent<Collider>());
    }

    private float GetTileSurfaceY()
    {
        if (tile == null) return transform.position.y;

        BoxCollider box = tile.GetComponent<BoxCollider>();
        if (box == null) return tile.transform.position.y;

        float halfHeight = box.size.y * 0.5f * tile.transform.lossyScale.y;
        float centerY    = box.center.y * tile.transform.lossyScale.y;
        return tile.transform.position.y + centerY + halfHeight;
    }

    public void UpdateRangeVisuals()
    {
        if (rangeIndicator == null) return;
        float hexSpacing = GridManager.Instance.hexSize * 1.732f;
        float visualRadius = CurrentRange * hexSpacing;
        rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
    }

    private void OnMouseDown()
    {
        if (owner == null) return;
        if (TurnManager.Instance != null && owner != TurnManager.Instance.currentPlayer) return;
        if (owner.isAI) return;
        
        BuildingUIManager.Instance?.Open(this);
    }

    private void OnMouseEnter() => ShowRange(true);
    private void OnMouseExit() => ShowRange(false);
    public void Recruit(PlayerData newOwner) { if (!isRecruited) owner = newOwner; isRecruited = true; }
}