using UnityEngine;
using System.Collections.Generic;

public class StructurePlacementManager : MonoBehaviour
{
    public static StructurePlacementManager Instance;

    [Header("Building Prefabs")]
    public GameObject serviceCenterPrefab;
    public GameObject bpoCenterPrefab;
    public GameObject tesseractPrefab;
    public GameObject signalBoosterPrefab;
    public GameObject signalJammerPrefab;
    public GameObject powerBoxPrefab;
    public GameObject commercialHubPrefab;
    public GameObject businessCenterPrefab;
    public GameObject workerFactoryPrefab;
    public GameObject droneFactoryPrefab;
    public GameObject rocketshipPrefab;

    private GameObject hologram;
    private HexTile hoveredTile;
    private bool isPlacing;
    private GameObject currentPrefab;
    private string currentFeature;

    public bool IsPlacing => isPlacing;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!isPlacing) return;

        FollowMouse();

        if (Input.GetMouseButtonDown(0) && hoveredTile != null && CanPlace(hoveredTile))
        {
            PlaceStructure();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    public void StartPlacement(GameObject prefab, string requiredFeature)
    {
        if (isPlacing) CancelPlacement();

        if (TechManager.Instance != null && !TechManager.Instance.IsFeatureUnlocked(requiredFeature))
        {
            Debug.LogWarning($"Feature '{requiredFeature}' not researched!");
            return;
        }

        isPlacing = true;
        currentPrefab = prefab;
        currentFeature = requiredFeature;

        hologram = Instantiate(prefab);
        HologramUtil.MakeHologram(hologram, new Color(0f, 1f, 0f, 0.35f));
    }

    void FollowMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HexTile tile = hit.collider.GetComponent<HexTile>();
            if (tile != null)
            {
                hoveredTile = tile;
                hologram.transform.position = tile.transform.position + Vector3.up * 1f;
                
                bool possible = CanPlace(tile);
                HologramUtil.MakeHologram(hologram, possible ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f));
            }
        }
    }

    bool CanPlace(HexTile tile)
    {
        if (tile.IsOccupied()) return false;
        
        // Check for adjacency to owned network (HQ, Tower, Wire, or another Structure)
        PlayerData currentPlayer = TurnManager.Instance.currentPlayer;
        foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(tile))
        {
            if (neighbor.placedNode != null && neighbor.placedNode.owner == currentPlayer) return true;
            if (neighbor.placedTower != null && neighbor.placedTower.owner == currentPlayer) return true;
            if (neighbor.placedWire != null && neighbor.placedWire.owner == currentPlayer) return true;
            if (neighbor.placedStructure != null && neighbor.placedStructure.owner == currentPlayer) return true;
        }
        
        return false;
    }

    void PlaceStructure()
    {
        PlayerData owner = TurnManager.Instance.currentPlayer;
        // Cost logic could be added here
        
        GameObject realStructure = Instantiate(currentPrefab, hoveredTile.transform.position + Vector3.up * 1f, Quaternion.identity);
        StructureNode node = realStructure.GetComponent<StructureNode>();
        if (node != null)
        {
            node.Initialize(hoveredTile, owner);
        }

        CancelPlacement();
    }

    void CancelPlacement()
    {
        if (hologram != null) Destroy(hologram);
        isPlacing = false;
        currentPrefab = null;
    }
}
