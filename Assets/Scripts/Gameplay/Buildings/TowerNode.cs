using UnityEngine;

public class TowerNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile ParentTile => tile;
    private bool _isBuilderFinished;
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
    public int   baseRange      = 3;
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
            float bonus = TechManager.Instance.GetInfraFlatBonus("TowerRange");
            float multiplier = TechManager.Instance.GetInfraMultiplier("TowerRange");
            if (multiplier <= 0) multiplier = 1f;
            return Mathf.Max(1, Mathf.RoundToInt((baseRange + bonus) * multiplier));
        }
    }

    public int CurrentRevenue
    {
        get
        {
            if (TechManager.Instance == null) return baseRevenue;
            float multiplier = TechManager.Instance.GetInfraMultiplier("TowerRevenue");
            if (multiplier <= 0) multiplier = 1f;
            return Mathf.RoundToInt(baseRevenue * multiplier);
        }
    }
    
    public float currentDurability;
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

        _isBuilderFinished = false;
        state = TowerState.Hologram;

        HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
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
        if (_isBuilderFinished || state == TowerState.Destroyed) return;

        _isBuilderFinished = true;
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
            if (_isBuilderFinished && powered) newState = TowerState.Powered;
            else if (_isBuilderFinished) newState = TowerState.Constructed;
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
            else
            {
                HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
                SetRangeColor(new Color(0f, 0.8f, 0f, 0.15f));
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
            decayPercent -= TechManager.Instance.GetInfraFlatBonus("WireDegradation");

        decayPercent = Mathf.Max(0.05f, decayPercent);
        float resistance = 1.0f;
        if (TechManager.Instance != null)
            resistance = TechManager.Instance.GetInfraMultiplier("TowerDurability");

        if (resistance < 1.0f) resistance = 1.0f;
        currentDurability -= (baseDurability * decayPercent) / resistance;

        if (currentDurability <= 0) DestroyTower();
    }

    public void Repair(float efficiencyMultiplier = 1.0f)
    {
        if (state != TowerState.Destroyed) return;
        _isBuilderFinished = true; // Repaired implies built
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

    public void SetBuilt() { _isBuilderFinished = true; state = TowerState.Constructed; }
    public bool IsBuilt() => _isBuilderFinished || state == TowerState.Powered;
    public bool IsDestroyed() => state == TowerState.Destroyed;
    //  RANGE INDICATOR HELPERS
    public void CreatePreview()
    {
        CreateRangeIndicator();
        ShowRange(true);
    }

    public void SetRangeColor(Color color) { if (rangeIndicator != null) rangeIndicator.GetComponent<Renderer>().material.color = color; }
    public void ShowRange(bool show) { if (rangeIndicator != null) { if (show) UpdateRangeVisuals(); rangeIndicator.SetActive(show); } }
    
    void CreateRangeIndicator()
    {
        if (rangeIndicator != null) return;
        rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeIndicator.transform.SetParent(transform);
        rangeIndicator.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        rangeIndicator.transform.localRotation = Quaternion.identity;
        UpdateRangeVisuals();
        Renderer rend = rangeIndicator.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        Destroy(rangeIndicator.GetComponent<Collider>());
    }

    public void UpdateRangeVisuals()
    {
        if (rangeIndicator == null) return;
        float hexSpacing = GridManager.Instance.hexSize * 1.732f;
        float visualRadius = CurrentRange * hexSpacing;
        rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
    }

    private void OnMouseEnter() => ShowRange(true);
    private void OnMouseExit() => ShowRange(false);
    public void Recruit(PlayerData newOwner) { if (!isRecruited) owner = newOwner; isRecruited = true; }
}