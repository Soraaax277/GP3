using UnityEngine;

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

    [Header("Expansion Settings")]
    public int expansionRadius     = 2; // claimed hexes when built
    public int baseInfluenceAmount = 5; 
    public int baseGoldCost        = 100;

    [Header("Hidden Stats")]
    public float hiddenDurability = 50f;
    public float currentHiddenDurability;
    public bool IsBroken { get; protected set; }

    protected GameObject rangeIndicator;

    public virtual void Initialize(HexTile tile, PlayerData player)
    {
        ParentTile = tile;
        owner = player;
        currentDurability = baseDurability;
        currentHiddenDurability = hiddenDurability;
        IsBroken = false;
        IsBuilt = false; // Starts as unbuilt/hologram
        
        HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f));
        
        tile.hasStructure = true; 
        tile.placedStructure = this;
        
        if (TurnManager.Instance != null)
            TurnManager.Instance.RegisterStructure(this);
            
        CreateRangeIndicator();
        SetRangeColor(new Color(0f, 0.5f, 1f, 0.15f));
        ShowRange(false);

        // SYNC: Ensure the power grid and borders update immediately on placement
        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
        
        ApplyInfluence(); 

        // 3D PRINTER: Instant Construction
        if (TechManager.Instance != null && TechManager.Instance.IsFeatureUnlockedFor(player, "3DPrinter"))
        {
            Build();
        }
    }

    public virtual void Build()
    {
        RemoveInfluence();
        IsBuilt = true;
        HologramUtil.MakeSolid(gameObject);
        Debug.Log($"[Structure] {name} has been constructed!");
        
        if (PowerGridManager.Instance != null) PowerGridManager.Instance.RefreshGrid();
        ApplyInfluence();

        // QUEST HOOK: High Tier Building
        if (QuestManager.Instance != null && owner != null && expansionRadius >= 3)
        {
            QuestManager.Instance.SetQuestFlag(owner, "PlacedHighTierBuilding");
        }
    }

    public virtual void UpdatePowerState(bool powered)
    {
        if (IsPowered == powered) return;

        RemoveInfluence();
        IsPowered = powered;
        
        SetRangeColor(IsPowered ? new Color(0f, 1f, 0f, 0.4f) : new Color(0.5f, 0.5f, 0.5f, 0.25f));
        ApplyInfluence();
    }

    public virtual void OnTurnStart() 
    {
        // Subclasses can implement income, production, etc.
    }

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
            if (currentDurability <= 0) 
            {
                BreakStructure();
            }
        }
    }

    protected virtual void BreakStructure()
    {
        IsBroken = true;
        currentDurability = 0;
        IsPowered = false;
        Debug.Log($"[Structure] {name} is BROKEN and needs repair!");
        
        // Visual feedback: darkening
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
            {
                QuestManager.Instance.SetQuestFlag(owner, "RepairedStructure");
            }
        }
        currentDurability = Mathf.Min(currentDurability + amount, baseDurability);
        
        // Reset color if subclass doesn't handle visuals
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) rend.material.color = Color.white; 
    }

    protected virtual void DestroyStructure()
    {
        if (ParentTile != null) ParentTile.hasStructure = false;
        Destroy(gameObject);
    }

    public virtual int GetCurrentUpkeep() => goldUpkeep;

    protected virtual void OnMouseDown()
    {
        if (owner == null) return;
        if (TurnManager.Instance != null && owner != TurnManager.Instance.currentPlayer) return;
        if (owner.isAI) return;
        
        BuildingUIManager.Instance?.Open(this);
    }

    protected virtual void OnMouseEnter() { ShowRange(true); }
    protected virtual void OnMouseExit() { ShowRange(false); }

    // ─────────────────────────────────────────────────────────────────────────
    //  Influence & Territory
    // ─────────────────────────────────────────────────────────────────────────

    public virtual void ApplyInfluence()
    {
        if (ParentTile == null || owner == null) return;

        float amount = baseInfluenceAmount;
        if (!IsBuilt) amount *= 0.1f; // Blueprints give minimal influence
        if (!IsPowered && IsBuilt) amount *= 0.5f; // Unpowered built structures give half

        var tiles = GridManager.Instance.GetTilesInRange(ParentTile, expansionRadius);
        foreach (HexTile t in tiles)
        {
            t.AddInfluence(owner, Mathf.RoundToInt(amount));
        }

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
        {
            t.RemoveInfluence(owner, Mathf.RoundToInt(amount));
        }

        if (TurnManager.Instance != null) TurnManager.Instance.NotifyStatusChanged();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Range Indicator Visuals
    // ─────────────────────────────────────────────────────────────────────────

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
        rend.material = new Material(Shader.Find("Sprites/Default"));
        Destroy(rangeIndicator.GetComponent<Collider>());
    }

    protected float GetTileSurfaceY()
    {
        if (ParentTile == null) return transform.position.y;
        BoxCollider box = ParentTile.GetComponent<BoxCollider>();
        if (box == null) return ParentTile.transform.position.y;
        float halfHeight = box.size.y * 0.5f * ParentTile.transform.lossyScale.y;
        float centerY = box.center.y * ParentTile.transform.lossyScale.y;
        return ParentTile.transform.position.y + centerY + halfHeight;
    }

    public void UpdateRangeVisuals()
    {
        if (rangeIndicator == null) return;
        float hexSpacing = GridManager.Instance.hexSize * 1.732f;
        // Now uses the specific expansionRadius set for this building type
        float visualRadius = expansionRadius * hexSpacing;
        rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
    }

    public void SetRangeColor(Color color) { if (rangeIndicator != null) rangeIndicator.GetComponent<Renderer>().material.color = color; }
    public void ShowRange(bool show) { if (rangeIndicator != null) { if (show) UpdateRangeVisuals(); rangeIndicator.SetActive(show); } }

    public abstract string GetRequiredTechFeature();
}
