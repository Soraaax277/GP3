using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class StructurePlacementManager : MonoBehaviour
{
    public static StructurePlacementManager Instance;

    [Header("Building Prefabs")]
    public GameObject serviceCenterPrefab;
    public GameObject advancedServiceCenterPrefab;
    public GameObject bpoCenterPrefab;
    public GameObject tesseractPrefab;
    public GameObject signalBoosterPrefab;
    public GameObject signalJammerPrefab;
    public GameObject powerBoxPrefab;
    public GameObject commercialHubPrefab;
    public GameObject businessCenterPrefab;
    public GameObject advancedBusinessCenterPrefab;
    public GameObject workerFactoryPrefab;
    public GameObject droneFactoryPrefab;
    public GameObject rocketshipPrefab;
    public GameObject canteenPrefab;

    private GameObject hologram;
    private HexTile hoveredTile;
    private bool isPlacing;
    private GameObject currentPrefab;
    private string currentFeature;
    private float _hologramBottomOffset;
    private float currentYRotation = 0f;

    public bool IsPlacing => isPlacing;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!isPlacing || PauseMenuUI.GameIsPaused) return;
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        HandleRotation();
        FollowMouse();

        if (Mouse.current.leftButton.wasPressedThisFrame && hoveredTile != null && CanPlace(hoveredTile))
        {
            PlaceStructure();
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelPlacement();
        }
    }

    private void HandleRotation()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            currentYRotation += 60f;
            if (hologram != null) hologram.transform.rotation = Quaternion.Euler(0, currentYRotation, 0);
        }
    }

    public void StartPlacement(GameObject prefab, string requiredFeature)
    {
        if (isPlacing) CancelPlacement();

        if (prefab == null)
        {
            Debug.LogError($"[StructurePlacementManager] StartPlacement failed: Prefab is NULL for feature {requiredFeature}");
            return;
        }

        if (TechManager.Instance != null && !TechManager.Instance.IsFeatureUnlocked(requiredFeature))
        {
            Debug.LogWarning($"[StructurePlacementManager] Feature '{requiredFeature}' not research or unlocked!");
            return;
        }

        isPlacing = true;
        currentPrefab = prefab;
        currentFeature = requiredFeature;
        currentYRotation = 0f; // Reset rotation for each new building

        hologram = Instantiate(prefab);
        if (hologram == null)
        {
            Debug.LogError($"[StructurePlacementManager] Failed to instantiate hologram for {prefab.name}");
            isPlacing = false;
            return;
        }

        Debug.Log($"[StructurePlacementManager] Started placement for {prefab.name}. Era visuals updating...");
        StructureNode node = hologram.GetComponent<StructureNode>();
        if (node != null)
        {
            node.UpdateEraVisuals();
            if (node.autoScaleToFit) node.AutoScaleToFitTiles();
        }

        // 2. Measure bottom offset AFTER visuals are active so we sit on ground
        _hologramBottomOffset = GetStructureBottomOffset(hologram);

        HologramUtil.MakeHologram(hologram, new Color(0f, 1f, 0f, 0.35f));
    }

    void FollowMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HexTile tile = hit.collider.GetComponent<HexTile>();
            if (tile != null)
            {
                hoveredTile = tile;

                StructureNode holoNode = hologram != null ? hologram.GetComponent<StructureNode>() : null;
                float extraLift = holoNode != null ? holoNode.verticalOffset : 0f;

                float surfaceY = GetTileSurfaceY(tile) + _hologramBottomOffset + extraLift;
                hologram.transform.position = new Vector3(tile.transform.position.x, surfaceY, tile.transform.position.z);

                bool possible = CanPlace(tile);
                HologramUtil.MakeHologram(hologram, possible
                    ? new Color(0f, 1f, 0f, 0.35f)
                    : new Color(1f, 0f, 0f, 0.35f));
            }
        }
    }

    private List<HexTile> GetTargetTiles(HexTile center)
    {
        List<HexTile> result = new List<HexTile> { center };
        if (currentPrefab == null) return result;
        
        StructureNode node = currentPrefab.GetComponent<StructureNode>();
        if (node == null || node.tilesOccupied <= 1) return result;

        List<HexTile> neighbors = GridManager.Instance.GetNeighbors(center);
        
        if (node.tilesOccupied == 2)
        {
            if (neighbors.Count > 0) result.Add(neighbors[0]);
        }
        else if (node.tilesOccupied >= 4 && node.tilesOccupied < 7)
        {
            for (int i = 0; i < Mathf.Min(3, neighbors.Count); i++)
                result.Add(neighbors[i]);
        }
        else if (node.tilesOccupied >= 7)
        {
            foreach (var n in neighbors) result.Add(n);
        }
        
        return result;
    }

    bool CanPlace(HexTile tile)
    {
        List<HexTile> targets = GetTargetTiles(tile);
        StructureNode prefabNode = currentPrefab.GetComponent<StructureNode>();
        if (prefabNode != null && targets.Count < prefabNode.tilesOccupied) return false;

        foreach (var t in targets)
        {
            // Explicitly block placement on water
            if (t.type == HexTile.TileType.Water) return false;
            if (t.IsOccupied()) return false;
        }

        PlayerData currentPlayer = TurnManager.Instance.currentPlayer;
        foreach (var t in targets)
        {
            // Adjacency/Influence check
            foreach (SignalNode hq in currentPlayer.ownedNodes)
            {
                if (hq == null) continue;
                float dist = GridManager.Instance.CubeDistance(hq.tile.cubeCoords, t.cubeCoords);
                if (dist <= hq.CurrentInfluenceRadius) return true;
            }

            foreach (HexTile neighbor in GridManager.Instance.GetNeighbors(t))
            {
                if (neighbor.placedNode != null && neighbor.placedNode.owner == currentPlayer) return true;
                if (neighbor.placedTower != null && neighbor.placedTower.owner == currentPlayer) return true;
                if (neighbor.placedWire != null && neighbor.placedWire.owner == currentPlayer) return true;
                if (neighbor.placedStructure != null && neighbor.placedStructure.owner == currentPlayer) return true;
            }
        }
        return false;
    }

    void PlaceStructure()
    {
        PlayerData owner = TurnManager.Instance.currentPlayer;
        List<HexTile> targets = GetTargetTiles(hoveredTile);

        StructureNode prefabNode = currentPrefab.GetComponent<StructureNode>();
        int cost = (prefabNode != null) ? prefabNode.baseGoldCost : 100;

        if (owner.resources < cost) return;
        owner.resources -= cost;

        float extraLift = (prefabNode != null) ? prefabNode.verticalOffset : 0f;
        float surfaceY = GetTileSurfaceY(hoveredTile) + _hologramBottomOffset + extraLift;

        GameObject realStructure = Instantiate(
            currentPrefab,
            new Vector3(hoveredTile.transform.position.x, surfaceY, hoveredTile.transform.position.z),
            Quaternion.Euler(0, currentYRotation, 0)
        );

        Debug.Log($"[StructurePlacement] {currentPrefab.name} placed with rotation {currentYRotation}.");

        StructureNode node = realStructure.GetComponent<StructureNode>();
        if (node != null)
        {
            node.Initialize(targets, owner);
            
            // Instantly update FOW so the new structure grants its vision immediately
            if (FieldOfViewManager.Instance != null && owner != null)
            {
                FieldOfViewManager.Instance.UpdateFogOfWar(owner);
            }
        }

        CancelPlacement();
    }

    void CancelPlacement()
    {
        if (hologram != null) Destroy(hologram);
        isPlacing = false;
        currentPrefab = null;
    }

    private float GetTileSurfaceY(HexTile tile)
    {
        if (tile == null) return 0f;
        BoxCollider box = tile.GetComponent<BoxCollider>();
        if (box == null) return tile.transform.position.y;
        float halfHeight = box.size.y * 0.5f * tile.transform.lossyScale.y;
        float centerY = box.center.y * tile.transform.lossyScale.y;
        return tile.transform.position.y + centerY + halfHeight;
    }

    private float GetStructureBottomOffset(GameObject obj)
    {
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return 0f;

        float minY = float.MaxValue;
        bool found = false;
        foreach (var r in rends)
        {
            if (r == null || !r.enabled || r.gameObject.name.Contains("RangeIndicator") || r.gameObject.name.Contains("Cylinder")) continue;
            minY = Mathf.Min(minY, r.bounds.min.y);
            found = true;
        }
        
        if (!found) return 0f;
        return obj.transform.position.y - minY;
    }
}