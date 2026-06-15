using UnityEngine;

public class TowerNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile ParentTile => tile;
    public bool isBuilderFinished;
    public bool IsPowered { get; set; }

    public bool IsTechnicianActivated
    {
        get => false;
        set { }
    }

    public enum TowerState { Hologram, Constructed, Powered, Destroyed }

    public PlayerData owner      { get; private set; }
    public SignalNode parentNode { get; private set; }
    public HexTile    tile;

    [Header("Stats")]
    public int   baseRange      = 1;
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
            float bonus      = TechManager.Instance.GetInfraFlatBonus(owner, "TowerRange");
            float multiplier = TechManager.Instance.GetInfraMultiplier(owner, "TowerRange");
            if (multiplier <= 0) multiplier = 1f;
            int calculated = Mathf.RoundToInt((baseRange + bonus) * multiplier);
            return Mathf.Clamp(calculated, 1, 3);
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

    public void UpdateEraVisuals()
    {
        if (TurnManager.Instance == null) return;
        TurnManager.GameEra era = TurnManager.Instance.GetCurrentEra();

        if (industrialVisual != null) industrialVisual.SetActive(false);
        if (early80sVisual   != null) early80sVisual.SetActive(false);
        if (retroVisual      != null) retroVisual.SetActive(false);
        if (futuristicVisual != null) futuristicVisual.SetActive(false);

        GameObject activeVisual = industrialVisual;
        if (era == TurnManager.GameEra.EarlyEighties && early80sVisual != null) activeVisual = early80sVisual;
        else if (era == TurnManager.GameEra.Retro     && retroVisual    != null) activeVisual = retroVisual;
        else if (era == TurnManager.GameEra.Futuristic && futuristicVisual != null) activeVisual = futuristicVisual;

        if (activeVisual != null)
        {
            activeVisual.SetActive(true);
            foreach (var col in activeVisual.GetComponentsInChildren<Collider>())
                Destroy(col);
        }

        if (state == TowerState.Hologram)
            HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
        else
            HologramUtil.MakeSolid(gameObject);
    }

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
        tile.placedTower  = this;
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

        // Do NOT call ApplyIdle here — tower is hologram, baseline would be wrong.
        // ApplyIdle is called when transitioning to Constructed or Powered state.
    }

    private void OnDestroy()
    {
        BuildingSelectionManager.Instance?.NotifyDestroyed(gameObject);
        HighlightUtil.Remove(gameObject);
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
        Debug.Log($"[Tower {name}] Builder finished. Awaiting power.");
        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
    }

    private Color GetOwnerGlowColor()
    {
        PlayerData current = TurnManager.Instance?.currentPlayer;
        return (owner == null || owner == current)
            ? new Color(0.2f, 0.6f, 1f)
            : new Color(1f, 0.2f, 0.2f);
    }

    public void UpdatePowerState(bool powered)
    {
        IsPowered = powered;
        CacheRenderers();

        TowerState newState = state;
        if (state != TowerState.Destroyed)
        {
            if (isBuilderFinished && powered)  newState = TowerState.Powered;
            else if (isBuilderFinished)         newState = TowerState.Constructed;
            else                                newState = TowerState.Hologram;
        }

        if (newState != state)
        {
            RemoveInfluence();
            state = newState;
            ApplyInfluence();

            if (state == TowerState.Powered)
            {
                if (owner != null && !owner.isAI && AudioManager.Instance != null && AudioManager.Instance.powerSFX != null)
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.powerSFX);

                HighlightUtil.Remove(gameObject);
                HologramUtil.MakeSolid(gameObject);
                HighlightUtil.ApplyIdle(gameObject, GetOwnerGlowColor());
                SetRangeColor(new Color(0f, 1f, 0f, 0.4f));
            }
            else if (state == TowerState.Constructed)
            {
                HighlightUtil.Remove(gameObject);
                HologramUtil.MakeSolid(gameObject);
                HighlightUtil.ApplyIdle(gameObject, GetOwnerGlowColor());
                SetRangeColor(new Color(0.5f, 0.5f, 0.5f, 0.25f));
            }
            else if (state == TowerState.Destroyed)
            {
                SetRangeColor(new Color(1f, 0f, 0f, 0.25f));
            }
            else // Hologram
            {
                HighlightUtil.Remove(gameObject);
                HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
                SetRangeColor(new Color(0f, 0.5f, 1f, 0.15f));
            }
        }
    }

    void ApplyInfluence()
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
            float amount = (state == TowerState.Powered && receivedSignalStrength > 0f)
                ? receivedSignalStrength * stateMultiplier * eraMultiplier
                : t.baseInfluence * stateMultiplier * eraMultiplier;
            t.AddInfluence(owner, Mathf.RoundToInt(amount));
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
        isBuilderFinished = true;
        state = TowerState.Constructed;

        HighlightUtil.Remove(gameObject);
        HologramUtil.MakeSolid(gameObject);
        HighlightUtil.ApplyIdle(gameObject, GetOwnerGlowColor());

        currentDurability = Mathf.Min(baseDurability * efficiencyMultiplier, baseDurability);
        ApplyInfluence();
        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
    }

    void DestroyTower()
    {
        state     = TowerState.Destroyed;
        IsPowered = false;

        if (VictoryManager.Instance != null && TurnManager.Instance != null)
        {
            PlayerData activePlayer = TurnManager.Instance.currentPlayer;
            if (activePlayer != null && activePlayer != owner)
                VictoryManager.Instance.RecordDenial(activePlayer);
        }

        if (FeedbackController.Instance != null)
            FeedbackController.Instance.PlayTowerDestroyed(transform.position);

        HighlightUtil.Remove(gameObject);
        ShowRange(false);
        SetRangeColor(new Color(1f, 0f, 0f, 0.25f));
    }

    float GetStateInfluenceMultiplier() => state switch
    {
        TowerState.Powered     => 1.0f,
        TowerState.Constructed => 0.5f,
        _                      => 0f
    };

    public int  GetCurrentUpkeep()  => goldUpkeep;
    public void SetBuilt()          { isBuilderFinished = true; state = TowerState.Constructed; }
    public bool IsBuilt()           => isBuilderFinished || state == TowerState.Powered;
    public bool IsDestroyed()       => state == TowerState.Destroyed;

    public void SetHologramState()
    {
        isBuilderFinished = false;
        state             = TowerState.Hologram;
        IsPowered         = false;
        HighlightUtil.Remove(gameObject);
        HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
        SetRangeColor(new Color(0f, 0.5f, 1f, 0.15f));
        ShowRange(false);
        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
    }

    public void CreatePreview()
    {
        state = TowerState.Hologram;
        UpdateEraVisuals();
        CreateRangeIndicator();
        ShowRange(true);
    }

    public void SetRangeColor(Color color) { if (rangeIndicator != null) rangeIndicator.GetComponent<Renderer>().material.color = color; }
    public void ShowRange(bool show)       { if (rangeIndicator != null) { if (show) UpdateRangeVisuals(); rangeIndicator.SetActive(show); } }

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

        float worldSurfaceY = GetTileSurfaceY();
        float localY = transform.InverseTransformPoint(0f, worldSurfaceY + 0.05f, 0f).y;
        rangeIndicator.transform.localPosition = new Vector3(0f, localY, 0f);

        UpdateRangeVisuals();
        Renderer rend = rangeIndicator.GetComponent<Renderer>();

        Shader indicatorShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (indicatorShader == null) indicatorShader = Shader.Find("Sprites/Default");

        Material mat = new Material(indicatorShader);
        mat.SetFloat("_Surface", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        rend.material = mat;

        Destroy(rangeIndicator.GetComponent<Collider>());
    }

    private float GetTileSurfaceY()
    {
        if (tile == null) return transform.position.y;
        return tile.GetSurfaceY();
    }

    public void UpdateRangeVisuals()
    {
        if (rangeIndicator == null) return;
        float hexSpacing   = GridManager.Instance.hexSize * 1.732f;
        float visualRadius = CurrentRange * hexSpacing;
        rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
    }

    // FIX: Select fires for all, UI only opens for current player's own buildings.
    private void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (PauseMenuUI.GameIsPaused) return;

        BuildingSelectionManager.Instance?.Select(gameObject, owner);

        if (owner == null) return;
        if (TurnManager.Instance != null && owner != TurnManager.Instance.currentPlayer) return;
        if (owner.isAI) return;
        BuildingUIManager.Instance?.Open(this);
    }

    private void OnMouseEnter() => ShowRange(true);
    private void OnMouseExit()  => ShowRange(false);
    public void Recruit(PlayerData newOwner) { if (!isRecruited) owner = newOwner; isRecruited = true; }
}