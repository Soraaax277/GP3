using UnityEngine;

public class TowerPlacementManager : MonoBehaviour
{
    public GameObject towerPrefab;

    private GameObject hologram;
    private HexTile hoveredTile;
    private bool isPlacing;
    private bool canPlace;
    public SignalNode selectedBusiness;
    public float lastPlacementTime;

    public bool IsPlacing => isPlacing;

    void Update()
    {
        if (!isPlacing) return;

        FollowMouse();

        if (Input.GetMouseButtonDown(0) && hoveredTile != null && canPlace)
        {
            PlaceTower();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    public void StartTowerPlacement(SignalNode business)
    {
        if (isPlacing || business == null) return;

        isPlacing = true;
        selectedBusiness = business;

        BuildUIManager.Instance.ignoreNextClick = true;

        hologram = Instantiate(towerPrefab);
        HologramUtil.MakeHologram(hologram, new Color(0f, 1f, 0f, 0.35f));

        TowerNode previewNode = hologram.GetComponent<TowerNode>();
        previewNode.CreatePreview();
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
        if (hologram == null || selectedBusiness == null) return;

        TowerNode previewNode = hologram.GetComponent<TowerNode>();
        bool occupied = tile.HasTower();
        bool isBusinessTile = tile == selectedBusiness.tile;

        Vector3 businessPos = selectedBusiness.tile.transform.position;
        Vector3 tilePos = tile.transform.position;
        float distance = Vector3.Distance(tilePos, businessPos);

        float visualRadius = selectedBusiness.GetVisualRadius();
        bool outsideVisual = distance > visualRadius;

        canPlace = !occupied && !isBusinessTile && !outsideVisual;

        Color holoColor = canPlace ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
        Color rangeColor = canPlace ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.25f);

        HologramUtil.MakeHologram(hologram, holoColor);
        previewNode.SetRangeColor(rangeColor);
        previewNode.ShowRange(true);
    }

    void PlaceTower()
    {
        if (!canPlace || hoveredTile == null) return;

        Destroy(hologram);

        GameObject realTower = Instantiate(
            towerPrefab,
            hoveredTile.transform.position + Vector3.up * 1.2f,
            Quaternion.identity
        );

        HologramUtil.MakeSolid(realTower);

        TowerNode node = realTower.GetComponent<TowerNode>();
        node.Initialize(hoveredTile);

        isPlacing = false;
        lastPlacementTime = Time.time;
    }


    void CancelPlacement()
    {
        Destroy(hologram);
        isPlacing = false;
        lastPlacementTime = Time.time;
    }

}
