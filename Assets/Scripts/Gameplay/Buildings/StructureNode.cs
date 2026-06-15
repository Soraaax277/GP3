using UnityEngine;
using System.Collections.Generic;

public abstract class StructureNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile ParentTile { get; protected set; }
    public PlayerData owner { get; protected set; }
    public bool IsPowered { get; set; }
    public bool IsTechnicianActivated { get; set; }
    public bool IsBuilt { get; protected set; }

    [Header("Base Stats")]
    public float baseDurability = 100f;
    public float currentDurability;
    public int   goldUpkeep     = 10;

    [Header("Size Settings")]
    public int   tilesOccupied  = 1;
    public bool  autoScaleToFit = true;
    public float verticalOffset = 0f;

    [Header("Expansion Settings")]
    public int expansionRadius     = 2;
    public int baseInfluenceAmount = 5;
    public int baseGoldCost = 100;

    [Header("Vision Settings")]
    public int visionRange = 3;

    [Header("Hidden Stats")]
    public float  hiddenDurability = 50f;
    public float  currentHiddenDurability;
    public bool IsBroken { get; protected set; }

    protected GameObject rangeIndicator;
    protected List<HexTile> occupiedTiles = new List<HexTile>();

    public virtual void Initialize(HexTile tile, PlayerData player)
    {
        Initialize(new List<HexTile> { tile }, player);
    }

    public virtual void Initialize(List<HexTile> tiles, PlayerData player)
    {
        if (tiles == null || tiles.Count == 0) return;

        ParentTile = tiles[0];
        occupiedTiles = new List<HexTile>(tiles);
        owner = player;
        currentDurability = baseDurability;
        currentHiddenDurability = hiddenDurability;
        IsBroken = false;
        IsBuilt = false;

        if (autoScaleToFit)
        {
            UpdateEraVisuals();
            AutoScaleToFitTiles();
        }

        HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));

        // Do NOT call ApplyIdle here — materials are hologram at this point.
        // ApplyIdle is called in Build() after MakeSolid.

        if (!player.isAI && AudioManager.Instance != null && AudioManager.Instance.placeBuildingSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.placeBuildingSFX);

        foreach (var t in occupiedTiles)
        {
            t.hasStructure    = true;
            t.placedStructure = this;
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterStructure(this);

        CreateRangeIndicator();
        SetRangeColor(new Color(0f, 0.5f, 1f, 0.15f));
        ShowRange(false);

        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();

        ApplyInfluence();

        if (TechManager.Instance != null && TechManager.Instance.IsFeatureUnlockedFor(player, "3DPrinter"))
            Build();
    }

    public void AutoScaleToFitTiles()
    {
        transform.localScale = Vector3.one;

        float hexSize    = (GridManager.Instance != null) ? GridManager.Instance.hexSize : 1f;
        float targetWidth = hexSize * 1.35f;

        if (tilesOccupied == 2)                          targetWidth = hexSize * 1.9f;
        else if (tilesOccupied >= 4 && tilesOccupied < 7) targetWidth = hexSize * 2.8f;
        else if (tilesOccupied >= 7)                     targetWidth = hexSize * 4.2f;

        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        bool first = true;
        foreach (var r in rends)
        {
            if (r == null || !r.enabled || r.gameObject == rangeIndicator || r.gameObject.name.Contains("Cylinder")) continue;
            if (r.bounds.size.magnitude < 0.1f) continue;

            Vector3 localCenter = transform.InverseTransformPoint(r.bounds.center);
            Vector3 localSize   = transform.InverseTransformVector(r.bounds.size);

            if (first) { b = new Bounds(localCenter, localSize); first = false; }
            else b.Encapsulate(new Bounds(localCenter, localSize));
        }

        if (first) return;

        Vector3 centerOffset = new Vector3(b.center.x, 0f, b.center.z);
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform ch = transform.GetChild(i);
            if (ch.gameObject == rangeIndicator) continue;
            ch.localPosition -= centerOffset;
        }

        float currentMaxDim = Mathf.Max(b.size.x, b.size.z);
        if (currentMaxDim > 0.01f)
        {
            float factor = targetWidth / currentMaxDim;
            factor = Mathf.Clamp(factor, 0.02f, 25f);
            transform.localScale = new Vector3(factor, factor, factor);
        }
    }

    public virtual void Build()
    {
        RemoveInfluence();
        IsBuilt = true;

        HighlightUtil.Remove(gameObject);
        HologramUtil.MakeSolid(gameObject);

        PlayerData current = TurnManager.Instance?.currentPlayer;
        Color glowColor = (owner == null || owner == current)
            ? new Color(0.2f, 0.6f, 1f)
            : new Color(1f, 0.2f, 0.2f);
        HighlightUtil.ApplyIdle(gameObject, glowColor);

        Debug.Log($"[Structure] {name} has been constructed!");

        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
        ApplyInfluence();

        if (QuestManager.Instance != null && owner != null && expansionRadius >= 3)
            QuestManager.Instance.SetQuestFlag(owner, "PlacedHighTierBuilding");
    }

    public virtual void UpdatePowerState(bool powered)
    {
        if (IsPowered == powered) return;

        RemoveInfluence();
        IsPowered = powered;

        if (IsPowered && owner != null && !owner.isAI && AudioManager.Instance != null && AudioManager.Instance.powerSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.powerSFX);

        SetRangeColor(IsPowered ? new Color(0f, 1f, 0f, 0.4f) : new Color(0.5f, 0.5f, 0.5f, 0.25f));
        ApplyInfluence();
    }

    public virtual void OnTurnStart() { }

    public virtual void TakeDamage(float amount)
    {
        if (IsBroken)
        {
            currentHiddenDurability -= amount;
            if (currentHiddenDurability <= 0) DestroyStructure();
        }
        else
        {
            currentDurability -= amount;
            if (currentDurability <= 0) BreakStructure();
        }
    }

    protected virtual void BreakStructure()
    {
        IsBroken = true;
        currentDurability = 0;
        IsPowered = false;
        Debug.Log($"[Structure] {name} is BROKEN and needs repair!");

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) rend.material.color = Color.Lerp(rend.material.color, Color.black, 0.5f);
    }

    public virtual void Repair(float amount)
    {
        if (IsBroken)
        {
            IsBroken = false;
            currentHiddenDurability = hiddenDurability;
            Debug.Log($"[Structure] {name} has been REPAIRED!");

            if (QuestManager.Instance != null && owner != null)
                QuestManager.Instance.SetQuestFlag(owner, "RepairedStructure");
        }
        currentDurability = Mathf.Min(currentDurability + amount, baseDurability);
        HologramUtil.MakeSolid(gameObject);
    }

    protected virtual void DestroyStructure()
    {
        foreach (var t in occupiedTiles)
        {
            if (t != null) t.hasStructure = false;
        }

        if (VictoryManager.Instance != null && TurnManager.Instance != null)
        {
            PlayerData activePlayer = TurnManager.Instance.currentPlayer;
            if (activePlayer != null && activePlayer != owner)
                VictoryManager.Instance.RecordDenial(activePlayer);
        }

        Destroy(gameObject);
    }

    public virtual int GetCurrentUpkeep() => goldUpkeep;

    // FIX: Select() fires for ALL buildings first, then UI guard runs separately.
    protected virtual void OnMouseDown()
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

    private void OnDestroy()
    {
        BuildingSelectionManager.Instance?.NotifyDestroyed(gameObject);
        HighlightUtil.Remove(gameObject);
    }

    protected virtual void OnMouseEnter() { ShowRange(true); }
    protected virtual void OnMouseExit()  { ShowRange(false); }

    public virtual void ApplyInfluence()
    {
        if (ParentTile == null || owner == null) return;

        float amount = baseInfluenceAmount;
        if (!IsBuilt) amount *= 0.1f;
        if (!IsPowered && IsBuilt) amount *= 0.5f;

        var tiles = GridManager.Instance.GetTilesInRange(ParentTile, expansionRadius);
        foreach (HexTile t in tiles)
            t.AddInfluence(owner, Mathf.RoundToInt(amount));

        if (TurnManager.Instance != null) TurnManager.Instance.NotifyStatusChanged();
    }

    public virtual void RemoveInfluence()
    {
        if (ParentTile == null || owner == null) return;

        float amount = baseInfluenceAmount;
        if (!IsBuilt) amount *= 0.1f;
        if (!IsPowered && IsBuilt) amount *= 0.5f;

        var tiles = GridManager.Instance.GetTilesInRange(ParentTile, expansionRadius);
        foreach (HexTile t in tiles)
            t.RemoveInfluence(owner, Mathf.RoundToInt(amount));

        if (TurnManager.Instance != null) TurnManager.Instance.NotifyStatusChanged();
    }

    protected void CreateRangeIndicator()
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

    protected float GetTileSurfaceY()
    {
        if (ParentTile == null) return transform.position.y;
        BoxCollider box = ParentTile.GetComponent<BoxCollider>();
        if (box == null) return ParentTile.transform.position.y;
        float halfHeight = box.size.y * 0.5f * ParentTile.transform.lossyScale.y;
        float centerY    = box.center.y * ParentTile.transform.lossyScale.y;
        return ParentTile.transform.position.y + centerY + halfHeight;
    }

    public void UpdateRangeVisuals()
    {
        if (rangeIndicator == null) return;
        float hexSpacing    = GridManager.Instance.hexSize * 1.732f;
        float visualRadius  = expansionRadius * hexSpacing;
        rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
    }

    public void SetRangeColor(Color color) { if (rangeIndicator != null) rangeIndicator.GetComponent<Renderer>().material.color = color; }
    public void ShowRange(bool show)       { if (rangeIndicator != null) { if (show) UpdateRangeVisuals(); rangeIndicator.SetActive(show); } }

    public bool IsMannedBy<T>() where T : Unit
    {
        if (occupiedTiles == null) return false;
        foreach (var tile in occupiedTiles)
        {
            if (tile != null && tile.placedUnit is T u && u.owner == owner)
                return true;
        }
        return false;
    }

    public List<Unit> GetStationedUnits()
    {
        var result = new List<Unit>();
        if (occupiedTiles == null) return result;
        foreach (var tile in occupiedTiles)
        {
            if (tile != null && tile.placedUnit != null && tile.placedUnit.owner == owner)
                result.Add(tile.placedUnit);
        }
        return result;
    }

    public List<HexTile> GetTilesInRange()
    {
        List<HexTile> inRange = new List<HexTile>();
        if (GridManager.Instance == null || occupiedTiles == null) return inRange;
        foreach (var t in occupiedTiles)
        {
            if (t == null) continue;
            foreach (var n in GridManager.Instance.GetTilesInRange(t, expansionRadius))
            {
                if (n != null && !inRange.Contains(n)) inRange.Add(n);
            }
        }
        return inRange;
    }

    public virtual void UpdateEraVisuals() { }
    public abstract string GetRequiredTechFeature();
}