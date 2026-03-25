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
    public int   tilesOccupied  = 1;  // 1, 2, or 4 tiles 
    public bool  autoScaleToFit = true; // Automatically scale to fit the occupied tiles

    [Header("Expansion Settings")]
    public int expansionRadius     = 2; // claimed hexes when built
    public int baseInfluenceAmount = 5; 
    public int baseGoldCost = 100;

    [Header("Vision Settings")]
    public int visionRange = 3; // Allows specific prefabs (like Tesseract/BPOCenter) to have larger vision

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
            UpdateEraVisuals(); // Ensure we are scaling the correct model (hide placeholders)
            AutoScaleToFitTiles();
        }
        
        HologramUtil.MakeHologram(gameObject, new Color(0f, 0.5f, 1f, 0.35f)); 
        
        foreach (var t in occupiedTiles)
        {
            t.hasStructure = true; 
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
        {
            Build();
        }
    }

    public void AutoScaleToFitTiles()
    {
        // 0. RESET SCALE to prevent cumulative scaling bugs if called multiple times
        transform.localScale = Vector3.one;

        // 1. Calculate the target width based on tile footprint (Reduced slightly after feedback)
        float hexSize = (GridManager.Instance != null) ? GridManager.Instance.hexSize : 1f;
        float targetWidth = hexSize * 1.35f; // Decreased from 1.6
        
        if (tilesOccupied == 2) targetWidth = hexSize * 1.9f; // Decreased from 3.0
        else if (tilesOccupied >= 4 && tilesOccupied < 7) targetWidth = hexSize * 2.8f; // Decreased from 4.5
        else if (tilesOccupied >= 7) targetWidth = hexSize * 4.2f; // Decreased from 6.5

        // 2. Measure current model bounds in LOCAL space
        // Skip tiny/degenerate renderers that might cause 'division by zero' or massive scaling
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        bool first = true;
        foreach (var r in rends)
        {
            if (r == null || !r.enabled || r.gameObject == rangeIndicator || r.gameObject.name.Contains("Cylinder")) continue;
            
            // IGNORE tiny renderers (often used for logic, markers, or degenerate geometry)
            // If they are smaller than 0.1 units, they are likely not the main building mesh.
            if (r.bounds.size.magnitude < 0.1f) continue;
            
            Vector3 localCenter = transform.InverseTransformPoint(r.bounds.center);
            Vector3 localSize = transform.InverseTransformVector(r.bounds.size);
            
            if (first) { b = new Bounds(localCenter, localSize); first = false; }
            else b.Encapsulate(new Bounds(localCenter, localSize));
        }

        if (first) return;

        // 3. APPLY CENTERING
        Vector3 centerOffset = new Vector3(b.center.x, 0f, b.center.z);
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform ch = transform.GetChild(i);
            if (ch.gameObject == rangeIndicator) continue;
            ch.localPosition -= centerOffset;
        }

        // 4. APPLY SCALING: Fit to target width
        float currentMaxDim = Mathf.Max(b.size.x, b.size.z);
        if (currentMaxDim > 0.01f)
        {
            float factor = targetWidth / currentMaxDim;
            // SAFETY CLAMPS:
            // Prevents buildings from becoming microscopically small or covering the entire map
            // due to deceptive bounds in the prefab.
            factor = Mathf.Clamp(factor, 0.02f, 25f); 
            transform.localScale = new Vector3(factor, factor, factor);
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
            {
                QuestManager.Instance.SetQuestFlag(owner, "RepairedStructure");
            }
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

    public virtual void ApplyInfluence()
    {
        if (ParentTile == null || owner == null) return;

        float amount = baseInfluenceAmount;
        if (!IsBuilt) amount *= 0.1f;
        if (!IsPowered && IsBuilt) amount *= 0.5f;

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
        float visualRadius = expansionRadius * hexSpacing;
        rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
    }

    public void SetRangeColor(Color color) { if (rangeIndicator != null) rangeIndicator.GetComponent<Renderer>().material.color = color; }
    public void ShowRange(bool show) { if (rangeIndicator != null) { if (show) UpdateRangeVisuals(); rangeIndicator.SetActive(show); } }

    /// <summary>Checks if any tile occupied by this building contains a units of type T owned by the player.</summary>
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
