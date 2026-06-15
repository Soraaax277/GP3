using UnityEngine;
using System.Collections.Generic;

public class SignalNode : MonoBehaviour
{
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

    [Header("Tower Settings")]
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
    public bool CanPlaceTower() => towersPlacedCount < CurrentMaxTowers;

    [Header("Influence Settings")]
    public int baseInfluenceRadius = 3;

    public int CurrentInfluenceRadius
    {
        get
        {
            if (TechManager.Instance == null) return baseInfluenceRadius;
            float flatBonus  = TechManager.Instance.GetInfraFlatBonus(owner, "InfluenceRadius");
            float multiplier = TechManager.Instance.GetInfraMultiplier(owner, "InfluenceRadius");
            if (multiplier <= 0f) multiplier = 1f;
            int result = Mathf.RoundToInt((baseInfluenceRadius + flatBonus) * multiplier);
            return Mathf.Clamp(result, 1, 6);
        }
    }

    [Header("Signal")]
    public float baseSignalStrength = 50f;

    public List<TowerNode> connectedTowers { get; private set; } = new List<TowerNode>();
    private GameObject rangeIndicator;

    public void Initialize(HexTile hexTile, PlayerData player)
    {
        tile  = hexTile;
        owner = player;

        if (tile != null && tile.hasStructure)
            tile.ClearEnvironmentalStructures();

        tile.placedNode       = this;
        tile.placedSignalNode = this;
        tile.hasStructure     = true;

        if (!player.ownedNodes.Contains(this))
            player.ownedNodes.Add(this);

        RefreshVisuals();

        CreateRangeIndicator();
        SetRangeColor(new Color(0f, 1f, 0f, 0.25f));
        ShowRange(false);

        ApplyInfluence();

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RegisterSource(this);
            PowerGridManager.Instance.RefreshGrid();
        }

        SetRangeColor(new Color(0f, 1f, 0f, 0.4f));

        ActionLogUI.PostFiltered(player, "established a NEW CONNECTION!", ActionLogUI.Colors.Construction);
        Debug.Log($"[SignalNode] Initialized for {player.playerName} at {hexTile.name}");

        PlayerData current = TurnManager.Instance?.currentPlayer;
        Color glowColor = (owner == null || owner == current)
            ? new Color(0.2f, 0.6f, 1f)
            : new Color(1f, 0.2f, 0.2f);
        HighlightUtil.ApplyIdle(gameObject, glowColor);
    }

    private void OnDestroy()
    {
        BuildingSelectionManager.Instance?.NotifyDestroyed(gameObject);
        HighlightUtil.Remove(gameObject);
    }

    public void RefreshVisuals()
    {
        int techLevel = 1;
        if (TechManager.Instance != null && owner != null)
            techLevel += Mathf.RoundToInt(TechManager.Instance.GetInfraFlatBonus(owner, "HQLevel"));

        currentLevel = Mathf.Clamp(techLevel, 1, 4);

        GameObject prefab = level1Visual;
        if (currentLevel == 2) prefab = level2Visual;
        else if (currentLevel == 3) prefab = level3Visual;
        else if (currentLevel == 4) prefab = level4Visual;

        if (prefab == null) return;

        if (currentVisualObj != null && currentVisualObj != gameObject)
            Destroy(currentVisualObj);

        currentVisualObj = Instantiate(prefab, transform.position, transform.rotation, transform);
        currentVisualObj.name = $"Visual_Level{currentLevel}";
    }

    public float GetBaseSignalStrength()
    {
        float techBoost = 0f;
        if (TechManager.Instance != null)
            techBoost = TechManager.Instance.GetInfraFlatBonus(owner, "BaseSignalBoost");
        return baseSignalStrength + techBoost;
    }

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

        bool microwaveRelays = TechManager.Instance != null &&
                               TechManager.Instance.IsFeatureUnlocked("MicrowaveRelays");

        float startSignal = GetBaseSignalStrength();
        var queue   = new Queue<(HexTile tile, float signal)>();
        var visited = new HashSet<HexTile>();
        visited.Add(tile);

        if (microwaveRelays)
        {
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
            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
            {
                if (neighbor == null || visited.Contains(neighbor)) continue;
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
                        tower.receivedSignalStrength = signal;
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
    }

    public void ApplyInfluence()
    {
        if (tile == null || owner == null) return;
        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, CurrentInfluenceRadius);
        foreach (HexTile t in tilesInRange)
            t.AddInfluence(owner, t.baseInfluence, true);
        if (TurnManager.Instance != null) TurnManager.Instance.NotifyStatusChanged();
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
        float hexSpacing   = GridManager.Instance.hexSize * 1.732f;
        float visualRadius = CurrentInfluenceRadius * hexSpacing;
        rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
    }

    public void SetRangeColor(Color color) { if (rangeIndicator != null) rangeIndicator.GetComponent<Renderer>().material.color = color; }
    public void ShowRange(bool show)       { if (rangeIndicator != null) { if (show) UpdateRangeVisuals(); rangeIndicator.SetActive(show); } }

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
}