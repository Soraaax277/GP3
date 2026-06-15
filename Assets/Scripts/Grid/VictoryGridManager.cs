using UnityEngine;
using System.Collections.Generic;

public class VictoryGridManager : MonoBehaviour
{
    public static VictoryGridManager Instance;

    private static readonly Vector3Int[] CubeDirections =
    {
        new Vector3Int( 1, -1,  0),
        new Vector3Int( 1,  0, -1),
        new Vector3Int( 0,  1, -1),
        new Vector3Int(-1,  1,  0),
        new Vector3Int(-1,  0,  1),
        new Vector3Int( 0, -1,  1),
    };

    [Header("Grid Settings")]
    public int width = 300;
    public int height = 180;
    public float hexSize = 1f;

    [Header("Continent Generation Settings")]
    public float noiseScale = 0.015f;
    public float landThreshold = -0.1f;
    public float edgeFalloff = 1.6f;

    [Header("References")]
    public GameObject hexTilePrefab;
    public GameObject waterTilePrefab;

    [Header("Water Visual")]
    public Material waterMaterial;
    public float sandBedDepth = 0.18f;
    public float sandBedScale = 1.02f;

    [Header("Land Visual")]
    public Material grassMaterial;

    [Header("Victory Buildings")]
    [Tooltip("Add your futuristic buildings here")]
    public GameObject[] futuristicBuildings;
    
    [Tooltip("Add your BPO buildings here")]
    public GameObject[] bpoBuildings;

    [Tooltip("Percentage of land tiles to cover with BPO/Futuristic buildings")]
    [Range(0.05f, 1f)]
    public float buildingCoverage = 1.0f;

    [Tooltip("Multiplier to scale down/up the futuristic buildings")]
    public float futuristicScaleMultiplier = 1.8f;

    [Tooltip("Multiplier to scale down/up the BPO buildings")]
    public float bpoScaleMultiplier = 0.5f;

    [Tooltip("Chance (0 to 1) to spawn a duplicate BPO building AFTER the original 7 guarantees have been placed. 0.02 = 2% chance per tile.")]
    [Range(0f, 1f)]
    public float bpoSpawnChance = 0.02f;

    [Tooltip("Randomize rotation of buildings")]
    public bool randomizeRotation = true;

    [Tooltip("How far from the tile centre a building may be placed")]
    [Range(0f, 0.5f)]
    public float buildingPositionSpread = 0.45f;

    [Header("Camera")]
    [Tooltip("If true, automatically frames the camera around the generated grid")]
    public bool autoFrameCamera = true;

    // Generation Seeds
    public float mapOffsetX;
    public float mapOffsetY;
    private bool seedSet = false;

    private List<GameObject> allPrefabsCache = new List<GameObject>();
    private Queue<GameObject> guaranteedSpawns = new Queue<GameObject>();

    public Dictionary<Vector3Int, HexTile> tiles = new Dictionary<Vector3Int, HexTile>();
    private Material _sandMaterial;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GenerateGrid();
    }

    public void SeedMap(float x, float y)
    {
        mapOffsetX = x;
        mapOffsetY = y;
        seedSet = true;
    }

    private void GenerateGrid()
    {
        tiles.Clear();

        if (!seedSet)
        {
            mapOffsetX = Random.Range(-10000f, 10000f);
            mapOffsetY = Random.Range(-10000f, 10000f);
            seedSet = true;
        }

        Random.InitState((int)(mapOffsetX * 10f + mapOffsetY * 100f));

        Vector3 worldCenter = HexToWorld(width / 2, height / 2);
        float maxRadius = (Mathf.Min(width, height) / 2f) * (hexSize * 2f);

        // STEP 1: INITIAL NOISE GENERATION
        for (int q = 0; q < width; q++)
        {
            for (int r = 0; r < height; r++)
            {
                Vector3 worldPos = HexToWorld(q, r);

                float distFromCenter = Vector3.Distance(worldPos, worldCenter);
                float normalizedDist = distFromCenter / maxRadius;
                float noiseValue = Mathf.PerlinNoise((q + mapOffsetX) * noiseScale, (r + mapOffsetY) * noiseScale);
                float finalLandValue = noiseValue - (normalizedDist * edgeFalloff);

                if (finalLandValue >= landThreshold)
                {
                    Vector3Int cubeCoords = AxialToCube(q, r);
                    GameObject tileObj = Instantiate(hexTilePrefab, worldPos - worldCenter, hexTilePrefab.transform.rotation, transform);
                    HexTile tile = tileObj.GetComponent<HexTile>();
                    tile.Initialize(cubeCoords);
                    tiles.Add(cubeCoords, tile);
                    ApplyGrassMaterial(tileObj);
                }
            }
        }

        // STEP 2: ASSIGN WATER AND STRUCTURES
        AssignEnvironmentFeatures();

        // STEP 3: POST-PROCESSING (REMOVE DETACHED ISLANDS)
        RemoveDisconnectedIslands();

        // STEP 4: FRAME CAMERA
        if (autoFrameCamera)
            FrameCameraToGrid(worldCenter, maxRadius);

        Debug.Log($"Generated {tiles.Count} hex tiles for Victory Grid.");
    }

    private void AssignEnvironmentFeatures()
    {
        List<HexTile> allTiles = new List<HexTile>(tiles.Values);
        BuildSandMaterial();

        int waterCount = Mathf.RoundToInt(allTiles.Count * 0.10f);
        for (int i = 0; i < waterCount; i++)
        {
            if (allTiles.Count == 0) break;
            int index = Random.Range(0, allTiles.Count);
            HexTile tile = allTiles[index];

            Vector3 pos = tile.transform.position;
            Vector3Int coords = tile.cubeCoords;
            allTiles.RemoveAt(index);

            if (waterTilePrefab != null)
            {
                tiles.Remove(coords);
                Destroy(tile.gameObject);

                GameObject waterObj = Instantiate(waterTilePrefab, pos, hexTilePrefab.transform.rotation, transform);
                HexTile waterTile = waterObj.GetComponent<HexTile>();
                waterTile.Initialize(coords, HexTile.TileType.Water);
                tiles.Add(coords, waterTile);

                ApplyWaterMaterial(waterObj);
                SpawnSandBed(waterTile);
            }
            else
            {
                tile.type = HexTile.TileType.Water;
                ApplyWaterMaterial(tile.gameObject);
                SpawnSandBed(tile);
                tile.UpdateAppearance();
            }
        }

        // Prepare pool of custom buildings
        BuildPrefabPools();

        // Spawn Custom Buildings
        int landTilesWithStructures = Mathf.RoundToInt(allTiles.Count * buildingCoverage);

        for (int i = 0; i < landTilesWithStructures; i++)
        {
            if (allTiles.Count == 0) break;
            int index = Random.Range(0, allTiles.Count);
            SpawnVictoryBuilding(allTiles[index]);
            allTiles.RemoveAt(index);
        }
    }

    private void BuildPrefabPools()
    {
        allPrefabsCache.Clear();
        guaranteedSpawns.Clear();

        if (futuristicBuildings != null) allPrefabsCache.AddRange(futuristicBuildings);
        if (bpoBuildings != null) allPrefabsCache.AddRange(bpoBuildings);

        // Remove nulls
        allPrefabsCache.RemoveAll(p => p == null);

        if (allPrefabsCache.Count == 0) return;

        // Shuffle the cache and enqueue every single unique prefab into the guaranteed queue
        List<GameObject> shuffleList = new List<GameObject>(allPrefabsCache);
        for (int i = 0; i < shuffleList.Count; i++)
        {
            GameObject temp = shuffleList[i];
            int randomIndex = Random.Range(i, shuffleList.Count);
            shuffleList[i] = shuffleList[randomIndex];
            shuffleList[randomIndex] = temp;
        }
        foreach (var p in shuffleList) guaranteedSpawns.Enqueue(p);
    }

    private GameObject GetNextPrefab()
    {
        if (futuristicBuildings == null && bpoBuildings == null) return null;

        // If we still have some unspawned prefabs to guarantee, pop one
        if (guaranteedSpawns.Count > 0)
            return guaranteedSpawns.Dequeue();

        // ── After Guarantees, roll for BPO vs Futuristic ──
        bool canSpawnBpo = (bpoBuildings != null && bpoBuildings.Length > 0);
        bool canSpawnFuturistic = (futuristicBuildings != null && futuristicBuildings.Length > 0);

        if (canSpawnBpo && canSpawnFuturistic)
        {
            if (Random.value <= bpoSpawnChance)
                return bpoBuildings[Random.Range(0, bpoBuildings.Length)];
            else
                return futuristicBuildings[Random.Range(0, futuristicBuildings.Length)];
        }
        else if (canSpawnFuturistic)
        {
            return futuristicBuildings[Random.Range(0, futuristicBuildings.Length)];
        }
        else if (canSpawnBpo)
        {
            return bpoBuildings[Random.Range(0, bpoBuildings.Length)];
        }

        return null;
    }

    private int PickBuildingCount()
    {
        // Cyberpunk Density: mostly 3, 4, or 5 buildings per hex tile!
        float r = Random.value;
        if (r < 0.15f) return 2;
        if (r < 0.45f) return 3;
        if (r < 0.80f) return 4;
        return 5;
    }

    private Vector2[] BuildSlotOffsets(int count, float radius)
    {
        Vector2[] slots = new Vector2[count];
        float jitter = radius * 0.18f;

        if (count == 1)
        {
            slots[0] = new Vector2(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter));
        }
        else
        {
            float startAngle = Random.Range(0f, 360f);
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angleDeg = startAngle + step * i;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                slots[i] = new Vector2(
                    Mathf.Cos(angleRad) * radius + Random.Range(-jitter, jitter),
                    Mathf.Sin(angleRad) * radius + Random.Range(-jitter, jitter)
                );
            }
        }
        return slots;
    }

    private bool IsBPOBuilding(GameObject prefab)
    {
        if (bpoBuildings == null) return false;
        foreach (var p in bpoBuildings)
            if (p == prefab) return true;
        return false;
    }

    private void SpawnVictoryBuilding(HexTile tile)
    {
        if (tile.type == HexTile.TileType.Water) return;
        if (allPrefabsCache.Count == 0) return;

        tile.hasStructure = true;

        GameObject firstPrefab = GetNextPrefab();
        if (firstPrefab == null) return;

        bool isBPO = IsBPOBuilding(firstPrefab);

        if (isBPO)
        {
            // BPO buildings are standalone. Spawn exactly 1, perfectly centered.
            SpawnSinglePrefab(tile, firstPrefab, 0f, 0f, true);
        }
        else
        {
            // Futuristic buildings are environmental. Spawn 1 to 3 in a cluster.
            int count = PickBuildingCount();
            float radius = hexSize * buildingPositionSpread;
            Vector2[] slots = BuildSlotOffsets(count, radius);

            // Spawn the guaranteed one first
            SpawnSinglePrefab(tile, firstPrefab, slots[0].x, slots[0].y, false);

            // Spawn the rest randomly from the futuristic pool only (if any exist)
            for (int i = 1; i < count; i++)
            {
                GameObject nextFuturistic = futuristicBuildings != null && futuristicBuildings.Length > 0 
                    ? futuristicBuildings[Random.Range(0, futuristicBuildings.Length)] 
                    : null;
                    
                if (nextFuturistic != null)
                {
                    SpawnSinglePrefab(tile, nextFuturistic, slots[i].x, slots[i].y, false);
                }
            }
        }
    }

    private void SpawnSinglePrefab(HexTile tile, GameObject prefabToSpawn, float rx, float ry, bool isBPO)
    {
        GameObject obj = Instantiate(prefabToSpawn);
        obj.name = isBPO ? "Victory_BPO" : "Victory_Futuristic";
        
        // Apply individual scale reduction
        float mult = isBPO ? bpoScaleMultiplier : futuristicScaleMultiplier;
        obj.transform.localScale = obj.transform.localScale * mult;
        
        // Add extreme vertical variation to futuristic skyscrapers to get a dynamic cyberpunk skyline
        if (!isBPO)
        {
            float verticalStretch = Random.Range(0.8f, 2.8f); // Some slightly shorter, some nearly 3x as tall!
            obj.transform.localScale = new Vector3(
                obj.transform.localScale.x,
                obj.transform.localScale.y * verticalStretch,
                obj.transform.localScale.z
            );
        }
        
        float yRot = randomizeRotation ? Random.Range(0f, 360f) : 0f;
        obj.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        obj.transform.position = tile.transform.position;

        float tileSurfaceY = tile.transform.position.y;
        Renderer tileRenderer = tile.GetComponent<Renderer>();
        if (tileRenderer != null) tileSurfaceY = tileRenderer.bounds.max.y;

        float yOffset = 0f;
        MeshRenderer[] meshRenderers = obj.GetComponentsInChildren<MeshRenderer>(false);
        if (meshRenderers.Length > 0)
        {
            float minY = float.MaxValue;
            foreach (var mr in meshRenderers)
                if (mr.enabled && mr.bounds.min.y < minY) minY = mr.bounds.min.y;

            if (minY < float.MaxValue)
                yOffset = tileSurfaceY - minY;
        }

        obj.transform.position = new Vector3(
            tile.transform.position.x + rx,
            tile.transform.position.y + yOffset,
            tile.transform.position.z + ry);

        obj.transform.SetParent(tile.transform, true);
    }

    // --- Water & Sand Helpers ---
    private void ApplyWaterMaterial(GameObject waterObj)
    {
        Renderer rend = waterObj.GetComponent<Renderer>();
        if (rend != null) rend.material = waterMaterial;
    }

    private void ApplyGrassMaterial(GameObject landObj)
    {
        Renderer rend = landObj.GetComponent<Renderer>();
        if (rend != null) rend.material = grassMaterial;
    }

    private void SpawnSandBed(HexTile waterTile)
    {
        if (hexTilePrefab == null) return;
        GameObject sandObj = Instantiate(hexTilePrefab, waterTile.transform.position, waterTile.transform.rotation, waterTile.transform);
        sandObj.name = "SandBed";
        sandObj.transform.localPosition = new Vector3(0f, -sandBedDepth, 0f);
        sandObj.transform.localScale = Vector3.one * sandBedScale;

        HexTile sandTileScript = sandObj.GetComponent<HexTile>();
        if (sandTileScript != null) Destroy(sandTileScript);
        foreach (Collider col in sandObj.GetComponentsInChildren<Collider>()) Destroy(col);

        Renderer rend = sandObj.GetComponent<Renderer>();
        if (rend != null) rend.material = BuildSandVariant(waterTile.cubeCoords);
    }

    private void BuildSandMaterial()
    {
        if (_sandMaterial != null) return;
        Shader sandShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _sandMaterial = new Material(sandShader) { color = SandColor(Vector3Int.zero, 0f) };
    }

    private Material BuildSandVariant(Vector3Int coords)
    {
        Shader sandShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        float hash = Mathf.Abs(Mathf.Sin(coords.x * 127.1f + coords.z * 311.7f));
        hash = hash - Mathf.Floor(hash);
        return new Material(sandShader) { color = SandColor(coords, hash) };
    }

    private static Color SandColor(Vector3Int coords, float variation)
    {
        Color baseSand = new Color(0.80f, 0.70f, 0.48f, 1f);
        Color wetSand = new Color(0.55f, 0.52f, 0.38f, 1f);
        Color lightSand = new Color(0.92f, 0.86f, 0.68f, 1f);
        if (variation > 0.85f) return Color.Lerp(baseSand, lightSand, (variation - 0.85f) / 0.15f);
        return Color.Lerp(wetSand, baseSand, variation);
    }

    // --- Math & Cleanup Helpers ---
    private void RemoveDisconnectedIslands()
    {
        List<HashSet<HexTile>> allLandmasses = new List<HashSet<HexTile>>();
        HashSet<HexTile> unvisited = new HashSet<HexTile>(tiles.Values);

        while (unvisited.Count > 0)
        {
            var en = unvisited.GetEnumerator();
            en.MoveNext();
            HashSet<HexTile> lm = GetConnectedRegion(en.Current);
            allLandmasses.Add(lm);
            unvisited.ExceptWith(lm);
        }

        if (allLandmasses.Count > 1)
        {
            allLandmasses.Sort((a, b) => b.Count.CompareTo(a.Count));
            for (int i = 1; i < allLandmasses.Count; i++)
            {
                foreach (HexTile t in allLandmasses[i])
                {
                    tiles.Remove(t.cubeCoords);
                    Destroy(t.gameObject);
                }
            }
        }
    }

    private HashSet<HexTile> GetConnectedRegion(HexTile startTile)
    {
        HashSet<HexTile> region = new HashSet<HexTile>();
        Queue<HexTile> frontier = new Queue<HexTile>();
        frontier.Enqueue(startTile);
        region.Add(startTile);

        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();
            foreach (HexTile n in GetNeighbors(current))
            {
                if (!region.Contains(n))
                {
                    region.Add(n);
                    frontier.Enqueue(n);
                }
            }
        }
        return region;
    }

    public List<HexTile> GetNeighbors(HexTile tile)
    {
        List<HexTile> neighbors = new List<HexTile>();
        foreach (Vector3Int dir in CubeDirections)
        {
            Vector3Int nc = tile.cubeCoords + dir;
            if (tiles.TryGetValue(nc, out HexTile n))
                neighbors.Add(n);
        }
        return neighbors;
    }

    private void FrameCameraToGrid(Vector3 worldCenter, float maxRadius)
    {
        if (Camera.main != null)
        {
            // Position camera above and pulled back so it looks down at the centre of the grid
            // The distance depends on how wide the max radius of the grid is.
            float camHeight = maxRadius * 1.25f;
            float camBack = maxRadius * 0.85f;
            
            Camera.main.transform.position = worldCenter + new Vector3(0, camHeight, -camBack);
            Camera.main.transform.LookAt(worldCenter);
        }
        else
        {
            Debug.LogWarning("[VictoryGridManager] Auto-Frame failed: No Camera tagged as MainCamera found.");
        }
    }

    private Vector3 HexToWorld(int q, int r)
    {
        float tw = hexSize * 2f;
        float th = Mathf.Sqrt(3f) * hexSize;
        return new Vector3(tw * (q + r * 0.5f), 0f, th * r);
    }

    private Vector3Int AxialToCube(int q, int r)
    {
        int x = q, z = r, y = -x - z;
        return new Vector3Int(x, y, z);
    }
}