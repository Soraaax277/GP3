using UnityEngine;

public class TowerPlacementManager : MonoBehaviour
{
    public static TowerPlacementManager Instance;
    public GameObject towerPrefab;

    private GameObject hologram;
    private HexTile hoveredTile;
    private bool isPlacing;
    private bool canPlace;
    public SignalNode selectedBusiness;
    public BuilderUnit selectedBuilder;
    public float lastPlacementTime;

    public bool IsPlacing => isPlacing;

    // -----------------------------------------------------------------------
    //  TOWER COST  ("TowerCost" multiplier tech)
    //  Base cost to place a tower. Deducted from owner's gold on placement.
    //  Default = 0 so existing behaviour is unchanged if you leave it at 0.
    //  "Tesseract Discovery" applies -0.5 multiplier → 50% cheaper.
    //  Set TechEffect: infraStatName="TowerCost", infraValueMod=-0.5, isMultiplier=✅
    // -----------------------------------------------------------------------
    [Header("Tower Placement Cost")]
    [Tooltip("Base gold cost to place one tower. 0 = free (default).")]
    public int baseTowerCost = 0;

    // Cached at hologram spawn: distance from pivot to bottom of tower's MeshCollider.
    // Used every frame in FollowMouse so we don't re-query bounds on each Update.
    private float _hologramBottomOffset;

    public int GetCurrentTowerCost()
    {
        if (TechManager.Instance == null) return baseTowerCost;
        float multiplier = TechManager.Instance.GetInfraMultiplier(TurnManager.Instance?.currentPlayer, "TowerCost");
        return Mathf.Max(0, Mathf.RoundToInt(baseTowerCost * multiplier));
    }

    private void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (!isPlacing) return;

        FollowMouse();

        if (Time.time < lastPlacementTime + 0.1f) return;

        if (Input.GetMouseButtonDown(0) && hoveredTile != null && canPlace)
        {
            PlaceTower();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    public void StartTowerPlacement(SignalNode business, BuilderUnit builder = null)
    {
        Debug.Log($"[TowerPlacement] Start request. Business: {business?.name}, Builder: {builder?.name}");
        if (isPlacing) 
        {
            Debug.Log("[TowerPlacement] Already placing!");
            return;
        }

        if (towerPrefab == null)
        {
            Debug.LogError("[TowerPlacement] Tower Prefab is MISSING in the Inspector!");
            return;
        }

        if (business != null && !business.CanPlaceTower())
        {
            Debug.LogWarning("[TowerPlacement] Business has reached tower capacity.");
            return;
        }

        isPlacing = true;
        selectedBusiness = business;
        selectedBuilder  = builder;
        lastPlacementTime = Time.time;

        if (BuildingUIManager.Instance != null)
            BuildingUIManager.Instance.ignoreNextClick = true;

        hologram = Instantiate(towerPrefab);
        Debug.Log($"[TowerPlacement] Hologram instantiated: {hologram?.name}");

        // Cache how far below the pivot the tower's mesh collider extends.
        // Used every frame to sit the base flush on the tile surface.
        _hologramBottomOffset = GetTowerBottomOffset(hologram);
        
        HologramUtil.MakeHologram(hologram, new Color(0f, 1f, 0f, 0.35f));

        TowerNode previewNode = hologram.GetComponent<TowerNode>();
        if (previewNode != null)
            previewNode.CreatePreview();
        else
            Debug.LogError("[TowerPlacement] TowerPrefab does not have a TowerNode component!");
    }

    void FollowMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        HexTile tile = hit.collider.GetComponent<HexTile>();
        if (tile == null) return;

        hoveredTile = tile;
        hologram.transform.position = new Vector3(
            tile.transform.position.x,
            GetTileSurfaceY(tile) + _hologramBottomOffset,
            tile.transform.position.z
        );

        // Keep the range circle on the tile surface regardless of tower pivot height.
        TowerNode previewNode = hologram.GetComponent<TowerNode>();
        if (previewNode != null)
            previewNode.SetRangeIndicatorToSurface(tile);

        ValidateTile(tile);
    }

    void ValidateTile(HexTile tile)
    {
        if (hologram == null) return;
        if (selectedBusiness == null && selectedBuilder == null) return;

        TowerNode previewNode = hologram.GetComponent<TowerNode>();
        bool occupied      = tile.HasTower();
        bool isBusinessTile = (selectedBusiness != null) && (tile == selectedBusiness.tile);
        bool inRange        = false;

        if (selectedBusiness != null)
        {
            int hexDist = GridManager.Instance.CubeDistance(tile.cubeCoords, selectedBusiness.tile.cubeCoords);
            inRange = hexDist <= selectedBusiness.CurrentInfluenceRadius;
        }
        else if (selectedBuilder != null)
        {
            int dist = GridManager.Instance.CubeDistance(selectedBuilder.currentTile.cubeCoords, tile.cubeCoords);
            if (dist <= selectedBuilder.buildRange)
            {
                foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
                {
                    if (neighbor.placedNode != null || neighbor.placedTower != null || neighbor.placedWire != null)
                    {
                        inRange = true;
                        break;
                    }
                }
            }
        }

        // Check gold affordability
        PlayerData owner = selectedBusiness != null ? selectedBusiness.owner : selectedBuilder?.owner;
        int cost = GetCurrentTowerCost();
        bool canAfford = owner == null || owner.resources >= cost;

        // Block placement on water. Environmental structures are now clearable.
        bool environmentBlocked = tile.type == HexTile.TileType.Water;

        canPlace = !occupied && !isBusinessTile && inRange && canAfford && !environmentBlocked;

        Color holoColor  = canPlace ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
        Color rangeColor = canPlace ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.25f);

        HologramUtil.MakeHologram(hologram, holoColor);
        previewNode.SetRangeColor(rangeColor);
        previewNode.ShowRange(true);
    }

    void PlaceTower()
    {
        if (!canPlace || hoveredTile == null) return;

        if (selectedBusiness != null && !selectedBusiness.CanPlaceTower())
        {
            Debug.LogError("[TowerPlacement] Limit reached just before placement!");
            CancelPlacement();
            return;
        }

        // DEDUCT TOWER COST
        PlayerData towerOwner = selectedBusiness != null ? selectedBusiness.owner : selectedBuilder?.owner;
        int cost = GetCurrentTowerCost();
        if (towerOwner != null && cost > 0)
        {
            if (towerOwner.resources < cost)
            {
                Debug.Log($"[TowerPlacement] Cannot afford tower! Need {cost}, have {towerOwner.resources}");
                CancelPlacement();
                return;
            }
            towerOwner.resources -= cost;
            Debug.Log($"[TowerPlacement] Tower placed for {cost} gold. Remaining: {towerOwner.resources}");
        }

        if (hoveredTile.hasStructure)
            hoveredTile.ClearEnvironmentalStructures();

        Destroy(hologram);

        if (AudioManager.Instance != null && AudioManager.Instance.placeTowerSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.placeTowerSFX);

        GameObject realTower = Instantiate(
            towerPrefab,
            new Vector3(hoveredTile.transform.position.x, GetTowerPlacementY(hoveredTile, towerPrefab), hoveredTile.transform.position.z),
            Quaternion.identity
        );

        TowerNode node = realTower.GetComponent<TowerNode>();
        node.Initialize(hoveredTile, towerOwner, selectedBusiness);
        
        ActionLogUI.PostFiltered(towerOwner, "Placed Tower blueprint.", ActionLogUI.Colors.Construction);
        
        // Update Fog of War immediately when a tower is placed
        if (FieldOfViewManager.Instance != null && towerOwner != null && !towerOwner.isAI)
        {
            FieldOfViewManager.Instance.UpdateFogOfWar(towerOwner);
        }

        if (selectedBusiness != null)
        {
            if (BuildingUIManager.Instance != null && BuildingUIManager.Instance.panel.activeSelf)
            {
                // [FIX] Added these braces so the compiler doesn't crash on an empty 'if' statement!
                // If you wanted to refresh the UI here, add that code inside these brackets.
            }
        }

        isPlacing = false;
        selectedBuilder = null;
        lastPlacementTime = Time.time;
    }

    void CancelPlacement()
    {
        if (hologram != null) Destroy(hologram);
        isPlacing = false;
        lastPlacementTime = Time.time;
    }

    public TowerNode PlaceTowerDirect(HexTile tile, PlayerData owner, SignalNode parentNode = null)
    {
        if (tile == null || towerPrefab == null) return null;

        GameObject realTower = Instantiate(
            towerPrefab,
            new Vector3(tile.transform.position.x, GetTowerPlacementY(tile, towerPrefab), tile.transform.position.z),
            Quaternion.identity
        );

        HologramUtil.MakeSolid(realTower);

        TowerNode node = realTower.GetComponent<TowerNode>();
        node.Initialize(tile, owner, parentNode);

        if (FieldOfViewManager.Instance != null && owner != null && !owner.isAI)
        {
            FieldOfViewManager.Instance.UpdateFogOfWar(owner);
        }

        return node;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Collider-based placement helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the world-space Y of the top surface of the tile's BoxCollider.</summary>
    private float GetTileSurfaceY(HexTile tile)
    {
        BoxCollider box = tile.GetComponent<BoxCollider>();
        if (box == null) return tile.transform.position.y;

        float halfHeight = box.size.y * 0.5f * tile.transform.lossyScale.y;
        float centerY    = box.center.y * tile.transform.lossyScale.y;
        return tile.transform.position.y + centerY + halfHeight;
    }

    /// <summary>
    /// Returns how far above its pivot the tower's MeshCollider bottom sits.
    /// Positive value means pivot is above the mesh bottom (tower would clip without this offset).
    /// </summary>
    private float GetTowerBottomOffset(GameObject towerObj)
    {
        MeshCollider mc = towerObj.GetComponentInChildren<MeshCollider>();
        if (mc == null) return 0f;

        return towerObj.transform.position.y - mc.bounds.min.y;
    }

    /// <summary>
    /// Temporarily instantiates the prefab to measure its bottom offset, then destroys it.
    /// Used for PlaceTower / PlaceTowerDirect where no live hologram exists to read from.
    /// </summary>
    private float GetTowerPlacementY(HexTile tile, GameObject prefab)
    {
        float surfaceY     = GetTileSurfaceY(tile);
        GameObject temp    = Instantiate(prefab);
        float bottomOffset = GetTowerBottomOffset(temp);
        Destroy(temp);
        return surfaceY + bottomOffset;
    }
}