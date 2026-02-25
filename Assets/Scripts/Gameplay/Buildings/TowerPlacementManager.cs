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

    public int GetCurrentTowerCost()
    {
        if (TechManager.Instance == null) return baseTowerCost;
        float multiplier = TechManager.Instance.GetInfraMultiplier("TowerCost");
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

        if (BuildUIManager.Instance != null)
            BuildUIManager.Instance.ignoreNextClick = true;

        hologram = Instantiate(towerPrefab);
        Debug.Log($"[TowerPlacement] Hologram instantiated: {hologram?.name}");
        
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
        hologram.transform.position = tile.transform.position + Vector3.up * 1.2f;

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

        GameObject realTower = Instantiate(
            towerPrefab,
            hoveredTile.transform.position + Vector3.up * 1.2f,
            Quaternion.identity
        );

        TowerNode node = realTower.GetComponent<TowerNode>();
        node.Initialize(hoveredTile, towerOwner, selectedBusiness);
        
        if (selectedBusiness != null)
        {
            if (BuildUIManager.Instance != null && BuildUIManager.Instance.buildPanel.activeSelf)
                BuildUIManager.Instance.UpdateBuildButtons();
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
            tile.transform.position + Vector3.up * 1.2f,
            Quaternion.identity
        );

        HologramUtil.MakeSolid(realTower);

        TowerNode node = realTower.GetComponent<TowerNode>();
        node.Initialize(tile, owner, parentNode);

        return node;
    }
}