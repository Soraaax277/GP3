using UnityEngine;
using System.Collections.Generic;

public class SignalNode : MonoBehaviour
{
    //  IDENTITY
    public PlayerData owner { get; private set; }
    public HexTile    tile  { get; private set; }

    public HexTile ParentTile => tile;

    [Header("Visual Levels")]
    public GameObject level1Visual;
    public GameObject level2Visual;
    public GameObject level3Visual;
    public GameObject level4Visual;
    public int currentLevel = 1;
    private GameObject currentVisualObj;

    //  TOWER CAPACITY
    [Header("Tower Settings")]
    [Tooltip("Base number of towers this HQ can support before any tech upgrades.")]
    public int maxTowers = 2;

    public int CurrentMaxTowers
    {
        get
        {
            int bonus = 0;
            if (TechManager.Instance != null)
                bonus = Mathf.RoundToInt(TechManager.Instance.GetInfraFlatBonus(owner, "TowerCapacity"));
            return maxTowers + bonus;
        }
    }

    public int towersPlacedCount = 0;

    public bool CanPlaceTower()
    {
        return towersPlacedCount < CurrentMaxTowers;
    }

    // -----------------------------------------------------------------------
    //  INFLUENCE / PLACEMENT RADIUS
    //  Supports BOTH flat bonus AND multiplier via the same "InfluenceRadius" key.
    //
    //  Flat  (isMultiplier ☐): adds directly to the base radius.
    //         e.g. base 3 + bonus 1 = 4 tiles
    //
    //  Mult  (isMultiplier ✅): scales the result after flat bonus is added.
    //         e.g. (base 3 + 0) * 1.1 = 3.3 → 3 tiles  (at +10%)
    //         e.g. (base 3 + 0) * 0.8 = 2.4 → 2 tiles  (Modern SatComm -20%)
    //
    //  Both can be stacked — flat first, then the multiplier.
    // -----------------------------------------------------------------------
    [Header("Influence Settings")]
    [Tooltip("Base hex radius within which towers can be placed from this HQ.")]
    public int baseInfluenceRadius = 3;

    public int CurrentInfluenceRadius
    {
        get
        {
            if (TechManager.Instance == null) return baseInfluenceRadius;

            float flatBonus  = TechManager.Instance.GetInfraFlatBonus(owner, "InfluenceRadius");
            float multiplier = TechManager.Instance.GetInfraMultiplier(owner, "InfluenceRadius");

            // Safety: prevent zero/negative multiplier collapsing the radius
            if (multiplier <= 0f) multiplier = 1f;

            // CAPPED for balance: Prevents HQ from claiming the entire map at once via stacked tech.
            int maxAllowedRadius = 6;
            int result = Mathf.RoundToInt((baseInfluenceRadius + flatBonus) * multiplier);
            return Mathf.Clamp(result, 1, maxAllowedRadius);
        }
    }

    //  SIGNAL  (System 2)
    [Header("Signal")]
    [Tooltip("Base signal strength broadcast from this HQ each turn.")]
    public float baseSignalStrength = 50f;

    public List<TowerNode> connectedTowers { get; private set; } = new List<TowerNode>();
    private GameObject rangeIndicator;

    //  INITIALIZATION
    public void Initialize(HexTile hexTile, PlayerData player)
    {
        tile  = hexTile;
        owner = player;

        // Ensure the tile is clear of environmental test structures
        if (tile != null && tile.hasStructure)
            tile.ClearEnvironmentalStructures();

        tile.placedNode       = this;
        tile.placedSignalNode = this;
        tile.hasStructure    = true; // Block other buildings from overlapping the HQ

        if (!player.ownedNodes.Contains(this))
            player.ownedNodes.Add(this);

        RefreshVisuals();

        CreateRangeIndicator();
        SetRangeColor(new Color(0f, 1f, 0f, 0.25f)); // Green for HQ
        ShowRange(false);

        ApplyInfluence();

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RegisterSource(this);
            PowerGridManager.Instance.RefreshGrid();
        }

        // VISUALS: HQs are ALWAYS powered and active
        SetRangeColor(new Color(0f, 1f, 0f, 0.4f)); 

        ActionLogUI.PostFiltered(player, "established a NEW CONNECTION!", ActionLogUI.Colors.Construction);
        Debug.Log($"[SignalNode] Initialized and Registered as Power Source for {player.playerName} at {hexTile.name}");
    }

    public void RefreshVisuals()
    {
        // 1. Determine level from tech tree or manual setting
        int techLevel = 1;
        if (TechManager.Instance != null && owner != null)
            techLevel += Mathf.RoundToInt(TechManager.Instance.GetInfraFlatBonus(owner, "HQLevel"));
            
        currentLevel = Mathf.Clamp(techLevel, 1, 4);

        // 2. Select prefab
        GameObject prefab = level1Visual;
        if (currentLevel == 2) prefab = level2Visual;
        else if (currentLevel == 3) prefab = level3Visual;
        else if (currentLevel == 4) prefab = level4Visual;

        if (prefab == null) return;

        // 3. Swap
        if (currentVisualObj != null && currentVisualObj != gameObject)
            Destroy(currentVisualObj);

        currentVisualObj = Instantiate(prefab, transform.position, transform.rotation, transform);
        currentVisualObj.name = $"Visual_Level{currentLevel}";
    }

    //  BASE SIGNAL
    public float GetBaseSignalStrength()
    {
        float techBoost = 0f;
        if (TechManager.Instance != null)
            techBoost = TechManager.Instance.GetInfraFlatBonus(owner, "BaseSignalBoost");
        return baseSignalStrength + techBoost;
    }

    //  SIGNAL PROPAGATION  (System 2)
    public void PropagateSignal()
    {
        if (GridManager.Instance == null || tile == null) return;

        foreach (TowerNode tower in TurnManager.Instance.GetAllTowers())
        {
            if (tower != null && tower.owner == owner)
                tower.receivedSignalStrength = 0f;
        }

        float decayRate = 0.50f;
        if (TechManager.Instance != null)
        {
            float reduction = TechManager.Instance.GetInfraFlatBonus(owner, "SignalDecayReduction");
            decayRate = Mathf.Max(0.05f, decayRate - reduction);
        }

        // "MicrowaveRelays" feature: HQ broadcasts directly to all owned towers
        // without needing a wire chain — signal still decays per hop distance.
        bool microwaveRelays = TechManager.Instance != null &&
                               TechManager.Instance.IsFeatureUnlocked("MicrowaveRelays");

        float startSignal = GetBaseSignalStrength();

        var queue   = new Queue<(HexTile tile, float signal)>();
        var visited = new HashSet<HexTile>();

        visited.Add(tile);

        if (microwaveRelays)
        {
            // Bypass wire requirement — seed directly from all owned towers regardless
            // of wire connectivity. Distance from HQ determines hop count.
            foreach (TowerNode tower in TurnManager.Instance.GetAllTowers())
            {
                if (tower == null || tower.owner != owner || tower.tile == null) continue;
                int hops = GridManager.Instance.CubeDistance(tile.cubeCoords, tower.tile.cubeCoords);
                float signal = startSignal * Mathf.Pow(1f - decayRate, hops);
                if (signal > tower.receivedSignalStrength)
                    tower.receivedSignalStrength = signal;
                if (!connectedTowers.Contains(tower))
                    connectedTowers.Add(tower);
            }
        }
        else
        {
            // Standard BFS through owned wires
            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
            {
                if (neighbor == null || visited.Contains(neighbor)) continue;

                // CHECK FOR POWERED INFRASTRUCTURE
                bool hasPoweredWire  = neighbor.placedWire  != null && neighbor.placedWire.owner  == owner && neighbor.placedWire.IsPowered;
                bool hasPoweredTower = neighbor.placedTower != null && neighbor.placedTower.owner == owner && neighbor.placedTower.IsPowered;

                if (hasPoweredWire || hasPoweredTower)
                    queue.Enqueue((neighbor, startSignal * (1f - decayRate)));
            }

            connectedTowers.Clear();

            while (queue.Count > 0)
            {
                var (current, signal) = queue.Dequeue();

                if (visited.Contains(current)) continue;
                visited.Add(current);

                if (current.placedTower != null && current.placedTower.owner == owner && current.placedTower.IsPowered)
                {
                    TowerNode tower = current.placedTower;
                    if (signal > tower.receivedSignalStrength)
                    {
                        tower.receivedSignalStrength = signal;
                        Debug.Log($"[Signal] {owner.playerName}'s {tower.name} receives: {signal:F2}");
                    }
                    if (!connectedTowers.Contains(tower))
                        connectedTowers.Add(tower);
                }

                if (current.placedWire != null && current.placedWire.owner == owner && current.placedWire.IsPowered)
                {
                    foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(current))
                    {
                        if (neighbor == null || visited.Contains(neighbor)) continue;

                        bool hasPoweredWire  = neighbor.placedWire  != null && neighbor.placedWire.owner  == owner && neighbor.placedWire.IsPowered;
                        bool hasPoweredTower = neighbor.placedTower != null && neighbor.placedTower.owner == owner && neighbor.placedTower.IsPowered;

                        if (hasPoweredWire || hasPoweredTower)
                            queue.Enqueue((neighbor, signal * (1f - decayRate)));
                    }
                }
            }
        }

        Debug.Log($"[Signal] {owner.playerName}'s HQ propagated " +
                  $"(base: {startSignal}, decay: {decayRate * 100:F0}%/hop, " +
                  $"microwave: {microwaveRelays}, connected towers: {connectedTowers.Count})");
    }

    // -----------------------------------------------------------------------
    //  INFLUENCE & VISUALS
    // -----------------------------------------------------------------------

    public void ApplyInfluence()
    {
        if (tile == null || owner == null) return;

        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, CurrentInfluenceRadius);
        foreach (HexTile t in tilesInRange)
        {
            // HQ provides a solid influence boost and ALWAYS bypasses dominance to anchor the city.
            t.AddInfluence(owner, t.baseInfluence, true);
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.NotifyStatusChanged();
    }

    void CreateRangeIndicator()
    {
        if (rangeIndicator != null) return;

        rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeIndicator.transform.SetParent(transform);
        rangeIndicator.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        rangeIndicator.transform.localRotation = Quaternion.identity;

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

    public void UpdateRangeVisuals()
    {
        if (rangeIndicator == null) return;

        float hexSpacing = GridManager.Instance.hexSize * 1.732f;
        float visualRadius = CurrentInfluenceRadius * hexSpacing;

        rangeIndicator.transform.localScale = 
            new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
    }

    public void SetRangeColor(Color color)
    {
        if (rangeIndicator == null) return;
        rangeIndicator.GetComponent<Renderer>().material.color = color;
    }

    public void ShowRange(bool show)
    {
        if (rangeIndicator != null)
        {
            if (show) UpdateRangeVisuals();
            rangeIndicator.SetActive(show);
        }
    }

    private void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (PauseMenuUI.GameIsPaused) return;
        if (owner == null) return;
        if (TurnManager.Instance != null && owner != TurnManager.Instance.currentPlayer) return;
        if (owner.isAI) return;
        BuildingUIManager.Instance?.Open(this);
    }

    private void OnMouseEnter()
    {
        ShowRange(true);
    }

    private void OnMouseExit()
    {
        ShowRange(false);
    }
}