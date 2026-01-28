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
            Debug.LogError("[TowerPlacement] Tower Prefab is MISSING in the Inspector! Please assign it to the TowerPlacementManager.");
            return;
        }

        if (business != null && !business.CanPlaceTower())
        {
            return;
        }

        isPlacing = true;
        selectedBusiness = business;
        selectedBuilder = builder;
        lastPlacementTime = Time.time;

        if (BuildUIManager.Instance != null)
            BuildUIManager.Instance.ignoreNextClick = true;

        hologram = Instantiate(towerPrefab);
        Debug.Log($"[TowerPlacement] Hologram instantiated: {hologram?.name}");
        
        HologramUtil.MakeHologram(hologram, new Color(0f, 1f, 0f, 0.35f));

        TowerNode previewNode = hologram.GetComponent<TowerNode>();
        if (previewNode != null)
        {
            previewNode.CreatePreview();
        }
        else
        {
            Debug.LogError("[TowerPlacement] The TowerPrefab does not have a TowerNode component attached!");
        }
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
        bool occupied = tile.HasTower();
        bool isBusinessTile = (selectedBusiness != null) && (tile == selectedBusiness.tile);

        bool inRange = false;
        if (selectedBusiness != null)
        {
            float distance = Vector3.Distance(tile.transform.position, selectedBusiness.tile.transform.position);
            inRange = distance <= selectedBusiness.GetVisualRadius();
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

        canPlace = !occupied && !isBusinessTile && inRange;

        Color holoColor = canPlace ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
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

        Destroy(hologram);

        GameObject realTower = Instantiate(
            towerPrefab,
            hoveredTile.transform.position + Vector3.up * 1.2f,
            Quaternion.identity
        );

        HologramUtil.MakeSolid(realTower);

        TowerNode node = realTower.GetComponent<TowerNode>();
        node.Initialize(hoveredTile, selectedBusiness != null ? selectedBusiness.owner : selectedBuilder.owner, selectedBusiness);
        
        if (selectedBusiness != null)
        {
            if (BuildUIManager.Instance != null && BuildUIManager.Instance.buildPanel.activeSelf)
            {
                BuildUIManager.Instance.UpdateBuildButtons();
            }
        }

        if (selectedBuilder != null)
        {
        }

        isPlacing = false;
        selectedBuilder = null;
        lastPlacementTime = Time.time;
    }


    void CancelPlacement()
    {
        Destroy(hologram);
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
