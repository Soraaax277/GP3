using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    private static readonly Vector3Int[] CubeDirections =
    {
        new Vector3Int( 1, -1,  0),
        new Vector3Int( 1,  0, -1),
        new Vector3Int( 0,  1, -1),
        new Vector3Int(-1,  1,  0),
        new Vector3Int(-1,  0,  1),
        new Vector3Int( 0, -1,  1),
    };

    public bool IsReady { get; private set; }
    public static GridManager Instance;

    [Header("Grid Settings")]
    public int   width     = 75;
    public int   height    = 45;
    public float hexSize   = 1f;

    [Header("Continent Generation Settings")]
    [Tooltip("How zoomed in the noise is. Lower values = larger, smoother landmasses.")]
    public float noiseScale    = 0.04f;
    [Tooltip("The base value required to spawn land. Lower = more land overall.")]
    public float landThreshold = -0.1f;
    [Tooltip("How strongly the edges of the map turn to water. Lower = land pushes closer to edges.")]
    public float edgeFalloff   = 0.8f;

    [Header("References")]
    public GameObject hexTilePrefab;
    public GameObject waterTilePrefab;

    // ── Water shader / material ──────────────────────────────────────────
    [Header("Water Visual")]
    [Tooltip("Assign the Material using the WaterUnlit shader here.\n" +
             "GridManager will apply it to every water tile automatically.")]
    public Material waterMaterial;

    [Tooltip("How far below the water surface the sand bed tile is placed.")]
    public float sandBedDepth = 0.18f;

    [Tooltip("Sand bed scale relative to the water tile (1 = identical size).")]
    public float sandBedScale = 1.02f; // slightly wider so no gaps at edges

    // ── Grass / land shader / material ───────────────────────────────────
    [Header("Land Visual")]
    [Tooltip("Assign the Material using your grass shader here.\n" +
             "GridManager will apply it to every land tile automatically.")]
    public Material grassMaterial;

    [Header("Environmental Building Era Prefabs")]
    [Tooltip("Prefab for the buildings randomly placed on the map during Industrial era.")]
    public GameObject buildingIndustrialPrefab;
    [Tooltip("Prefab for the Early 80s era map buildings.")]
    public GameObject buildingEarly80sPrefab;
    [Tooltip("Prefab for the Retro era map buildings.")]
    public GameObject buildingRetroPrefab;
    [Tooltip("Prefab for the Futuristic era map buildings.")]
    public GameObject buildingFuturisticPrefab;

    // ─────────────────────────────────────────────────────────────────────
    public Dictionary<Vector3Int, HexTile> tiles =
        new Dictionary<Vector3Int, HexTile>();

    // Shared sand material — created once, reused for all sand beds
    private Material _sandMaterial;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GenerateGrid();
    }

    [Header("Generation Seeds (Saved)")]
    public float mapOffsetX;
    public float mapOffsetY;
    private bool seedSet = false;

    // =====================================================================
    //  GRID GENERATION
    // =====================================================================
    public void SeedMap(float x, float y)
    {
        mapOffsetX = x;
        mapOffsetY = y;
        seedSet = true;
    }

    void GenerateGrid()
    {
        tiles.Clear();

        if (!seedSet)
        {
            mapOffsetX = Random.Range(-10000f, 10000f);
            mapOffsetY = Random.Range(-10000f, 10000f);
            seedSet = true;
        }

        // Fix: Make all Random.Range calls in environment generation deterministic
        Random.InitState((int)(mapOffsetX * 10f + mapOffsetY * 100f));


        Vector3 worldCenter = HexToWorld(width / 2, height / 2);
        float   maxRadius   = (Mathf.Min(width, height) / 2f) * (hexSize * 2f);

        // STEP 1: INITIAL NOISE GENERATION
        for (int q = 0; q < width; q++)
        {
            for (int r = 0; r < height; r++)
            {
                Vector3 worldPos = HexToWorld(q, r);

                float distFromCenter  = Vector3.Distance(worldPos, worldCenter);
                float normalizedDist  = distFromCenter / maxRadius;
                float noiseValue      = Mathf.PerlinNoise(
                    (q + mapOffsetX) * noiseScale,
                    (r + mapOffsetY) * noiseScale);
                float finalLandValue  = noiseValue - (normalizedDist * edgeFalloff);

                if (finalLandValue >= landThreshold)
                {
                    Vector3Int cubeCoords = AxialToCube(q, r);
                    GameObject tileObj = Instantiate(
                        hexTilePrefab,
                        worldPos,
                        hexTilePrefab.transform.rotation,
                        transform);
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

        IsReady = true;
        Debug.Log($"Generated {tiles.Count} hex tiles forming a single contiguous continent.");
    }

    // =====================================================================
    //  ENVIRONMENT FEATURES
    // =====================================================================
    private Material structureMaterial;

    private void AssignEnvironmentFeatures()
    {
        List<HexTile> allTiles = new List<HexTile>(tiles.Values);

        // ── Build shared sand material once ───────────────────────────────
        BuildSandMaterial();

        int waterCount = Mathf.RoundToInt(allTiles.Count * 0.10f);
        for (int i = 0; i < waterCount; i++)
        {
            if (allTiles.Count == 0) break;
            int     index = Random.Range(0, allTiles.Count);
            HexTile tile  = allTiles[index];

            Vector3    pos    = tile.transform.position;
            Vector3Int coords = tile.cubeCoords;
            allTiles.RemoveAt(index);

            if (waterTilePrefab != null)
            {
                tiles.Remove(coords);
                Destroy(tile.gameObject);

                GameObject waterObj  = Instantiate(
                    waterTilePrefab,
                    pos,
                    hexTilePrefab.transform.rotation,
                    transform);
                HexTile waterTile = waterObj.GetComponent<HexTile>();
                waterTile.Initialize(coords, HexTile.TileType.Water);
                tiles.Add(coords, waterTile);

                // ── Apply water shader material ───────────────────────────
                ApplyWaterMaterial(waterObj);

                // ── Spawn sand bed below this water tile ──────────────────
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

        // ── Structure material ─────────────────────────────────────────────
        if (structureMaterial == null)
        {
            Shader structShader = Shader.Find("Universal Render Pipeline/Lit");
            if (structShader == null) structShader = Shader.Find("Sprites/Default");
            structureMaterial       = new Material(structShader);
            structureMaterial.color = new Color(0.8f, 0.8f, 0.85f);
        }

        int landTilesWithStructures = Mathf.RoundToInt(allTiles.Count * 0.70f);
        for (int i = 0; i < landTilesWithStructures; i++)
        {
            if (allTiles.Count == 0) break;
            int index = Random.Range(0, allTiles.Count);
            SpawnStructures(allTiles[index]);
            allTiles.RemoveAt(index);
        }
    }

    // =====================================================================
    //  WATER MATERIAL APPLICATION
    // =====================================================================
    // Applies the WaterUnlit material to the Renderer on the water tile
    // GameObject. Falls back to a plain blue material if none is assigned.
    private void ApplyWaterMaterial(GameObject waterObj)
    {
        Renderer rend = waterObj.GetComponent<Renderer>();
        if (rend == null) return;

        if (waterMaterial != null)
        {
            // Instance the material so each tile can have independent depth
            // values without interfering with others.
            rend.material = waterMaterial;
        }
        else
        {
            // Fallback: simple blue so the game isn't broken without the shader
            Debug.LogWarning("[GridManager] waterMaterial not assigned — using fallback blue.");
            Shader fb = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
            if (fb != null)
            {
                Material mat   = new Material(fb);
                mat.color      = new Color(0.1f, 0.3f, 0.78f, 0.85f);
                rend.material  = mat;
            }
        }
    }

    // Applies the grass/land material to a land tile's Renderer.
    // Falls back to a plain green material if none is assigned in the Inspector.
    private void ApplyGrassMaterial(GameObject landObj)
    {
        Renderer rend = landObj.GetComponent<Renderer>();
        if (rend == null) return;

        if (grassMaterial != null)
        {
            rend.material = grassMaterial;
        }
        else
        {
            // Fallback: plain green so the game isn't broken without the shader
            Debug.LogWarning("[GridManager] grassMaterial not assigned — using fallback green.");
            Shader fb = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
            if (fb != null)
            {
                Material mat  = new Material(fb);
                mat.color     = new Color(0.28f, 0.45f, 0.18f, 1f);
                rend.material = mat;
            }
        }
    }

    // =====================================================================
    //  SAND BED
    // =====================================================================
    // Spawns a copy of the hex tile prefab directly below the given water tile,
    // coloured like wet sand to simulate a shallow seabed visible through the
    // water shader's depth-based transparency.
    //
    // The sand bed is parented to the water tile so it moves with it if anything
    // ever repositions the tile, and it is tagged "SandBed" for easy lookup.
    //
    // Visual design notes:
    //   - Offset downward by sandBedDepth (Inspector-tunable, default 0.18 units)
    ///   - Slightly wider than the water tile (sandBedScale, default 1.02) so
    //     no thin gaps appear at the water surface edge
    //   - Two-tone color: base sandy brown with a subtle darker variation seeded
    //     per-tile via the tile's cube coords, giving organic variation across
    //     the seabed without any texture asset
    private void SpawnSandBed(HexTile waterTile)
    {
        if (hexTilePrefab == null) return;

        // Position: same XZ as the water tile, pushed down by sandBedDepth
        Vector3 waterPos = waterTile.transform.position;
        Vector3 sandPos  = new Vector3(0f, 0f, 0.01f);

        GameObject sandObj = Instantiate(
            hexTilePrefab,
            waterTile.transform.position, // world pos set via localPosition below
            waterTile.transform.rotation,
            waterTile.transform); // parented under the water tile

        sandObj.name = "SandBed";
        sandObj.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        sandObj.transform.localRotation = Quaternion.identity;

        // Scale slightly larger to avoid edge gaps at the water surface
        sandObj.transform.localScale = Vector3.one * sandBedScale;

        // ── Remove the HexTile script — sand beds are purely visual ────────
        HexTile sandTileScript = sandObj.GetComponent<HexTile>();
        if (sandTileScript != null)
            Destroy(sandTileScript);

        // Remove any colliders — only the water tile above should receive raycasts
        foreach (Collider col in sandObj.GetComponentsInChildren<Collider>())
            Destroy(col);

        // ── Apply per-tile sand color variation ────────────────────────────
        Renderer rend = sandObj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = BuildSandVariant(waterTile.cubeCoords);
        }

        // ── Disable any child Env_Structure objects that might have been
        //    copied from the prefab — sand bed should be flat ───────────────
        for (int i = sandObj.transform.childCount - 1; i >= 0; i--)
            Destroy(sandObj.transform.GetChild(i).gameObject);
    }

    // =====================================================================
    //  SAND MATERIAL HELPERS
    // =====================================================================
    private void BuildSandMaterial()
    {
        if (_sandMaterial != null) return;

        Shader sandShader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default");
        _sandMaterial       = new Material(sandShader);
        _sandMaterial.color = SandColor(Vector3Int.zero, 0f);
    }

    /// <summary>
    /// Returns a new material instance with a unique sand color tinted per-tile
    /// using a deterministic hash of the tile's cube coordinates.
    /// Range: warm tan → slightly greenish damp sand.
    /// </summary>
    private Material BuildSandVariant(Vector3Int coords)
    {
        Shader sandShader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default");

        // Hash the coords to a 0..1 variation value — deterministic per tile
        float hash = Mathf.Abs(Mathf.Sin(coords.x * 127.1f + coords.z * 311.7f));
        hash       = hash - Mathf.Floor(hash); // frac

        Material mat = new Material(sandShader);
        mat.color    = SandColor(coords, hash);

        // Make the sand bed slightly emissive so it's visible through the
        // semi-transparent water without needing a light source under the tile
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            Color emissive = SandColor(coords, hash) * 0.18f;
            mat.SetColor("_EmissionColor", emissive);
        }

        return mat;
    }

    private static Color SandColor(Vector3Int coords, float variation)
    {
        // Base: warm sandy tan
        Color baseSand  = new Color(0.80f, 0.70f, 0.48f, 1f);
        // Wet variant: cooler, darker, slightly greenish
        Color wetSand   = new Color(0.55f, 0.52f, 0.38f, 1f);
        // Occasional bright highlight patch (shells / quartz)
        Color lightSand = new Color(0.92f, 0.86f, 0.68f, 1f);

        if (variation > 0.85f) return Color.Lerp(baseSand, lightSand, (variation - 0.85f) / 0.15f);
        return Color.Lerp(wetSand, baseSand, variation);
    }

    // =====================================================================
    //  STRUCTURE SPAWNING (unchanged)
    // =====================================================================
    private void SpawnStructures(HexTile tile)
    {
        tile.hasStructure = true;
        int count = Random.Range(3, 6);

        // Pick which era prefab to use for the initial spawn
        GameObject eraBuilding = buildingIndustrialPrefab;

        for (int i = 0; i < count; i++)
        {
            GameObject obj;
            if (eraBuilding != null)
            {
                obj = Instantiate(eraBuilding, tile.transform);
                obj.name = "Env_Structure";

                float rx = Random.Range(-0.0035f, 0.0035f);
                float ry = Random.Range(-0.0035f, 0.0035f);
                obj.transform.localPosition = new Vector3(rx, ry, 0f);
                obj.transform.localRotation = Quaternion.identity;
                // Remove colliders so units can walk through
                foreach (var col in obj.GetComponentsInChildren<Collider>())
                    Destroy(col);
            }
            else
            {
                // Fallback: plain cube
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "Env_Structure";
                obj.transform.SetParent(tile.transform);

                float rx = Random.Range(-0.0035f, 0.0035f);
                float ry = Random.Range(-0.0035f, 0.0035f);
                float h  = Random.Range(0.006f, 0.018f);
                float w  = Random.Range(0.0025f, 0.0045f);

                obj.transform.localPosition = new Vector3(rx, ry, -h / 2f - 0.001f);
                obj.transform.localScale    = new Vector3(w, w, h);
                obj.transform.localRotation = Quaternion.identity;
                obj.GetComponent<Renderer>().sharedMaterial = grassMaterial;

                if (obj.TryGetComponent<Collider>(out Collider col))
                    Destroy(col);
            }
        }
    }

    /// <summary>
    /// Swaps all environmental buildings on every tile to the prefab matching the given era.
    /// Call this whenever TurnManager's global ear changes.
    /// </summary>
    public void RefreshEraBuildings(TurnManager.GameEra era)
    {
        GameObject prefab = buildingIndustrialPrefab;
        if (era == TurnManager.GameEra.EarlyEighties) prefab = buildingEarly80sPrefab;
        else if (era == TurnManager.GameEra.Retro)    prefab = buildingRetroPrefab;
        else if (era == TurnManager.GameEra.Futuristic) prefab = buildingFuturisticPrefab;

        foreach (var tile in tiles.Values)
        {
            if (!tile.hasStructure) continue;

            // Remove existing Env_Structure children
            for (int i = tile.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = tile.transform.GetChild(i);
                if (child.name.Contains("Env_Structure"))
                    Destroy(child.gameObject);
            }

            // Re-spawn with new prefab
            SpawnStructuresEra(tile, prefab);
        }
    }

    private void SpawnStructuresEra(HexTile tile, GameObject prefab)
    {
        int count = Random.Range(3, 6);
        for (int i = 0; i < count; i++)
        {
            GameObject obj;
            if (prefab != null)
            {
                obj = Instantiate(prefab, tile.transform);
                obj.name = "Env_Structure";
                float rx = Random.Range(-0.0035f, 0.0035f);
                float ry = Random.Range(-0.0035f, 0.0035f);
                obj.transform.localPosition = new Vector3(rx, ry, 0f);
                obj.transform.localRotation = Quaternion.identity;
                foreach (var col in obj.GetComponentsInChildren<Collider>())
                    Destroy(col);
            }
            else
            {
                // Fallback: plain cube
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "Env_Structure";
                obj.transform.SetParent(tile.transform);
                float rx = Random.Range(-0.0035f, 0.0035f);
                float ry = Random.Range(-0.0035f, 0.0035f);
                float h  = Random.Range(0.006f, 0.018f);
                float w  = Random.Range(0.0025f, 0.0045f);
                obj.transform.localPosition = new Vector3(rx, ry, -h / 2f - 0.001f);
                obj.transform.localScale    = new Vector3(w, w, h);
                obj.transform.localRotation = Quaternion.identity;
                obj.GetComponent<Renderer>().sharedMaterial = grassMaterial;
                if (obj.TryGetComponent<Collider>(out Collider col))
                    Destroy(col);
            }
        }
    }

    // =====================================================================
    //  ISLAND REMOVAL (unchanged)
    // =====================================================================
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
                foreach (HexTile t in allLandmasses[i])
                {
                    tiles.Remove(t.cubeCoords);
                    Destroy(t.gameObject);
                }
        }
    }

    private HashSet<HexTile> GetConnectedRegion(HexTile startTile)
    {
        HashSet<HexTile> region   = new HashSet<HexTile>();
        Queue<HexTile>   frontier = new Queue<HexTile>();
        frontier.Enqueue(startTile);
        region.Add(startTile);
        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();
            foreach (HexTile n in GetNeighbors(current))
                if (!region.Contains(n)) { region.Add(n); frontier.Enqueue(n); }
        }
        return region;
    }

    // =====================================================================
    //  PUBLIC ACCESSORS (unchanged)
    // =====================================================================
    public IEnumerable<HexTile> GetAllTiles() => tiles.Values;

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

    Vector3 HexToWorld(int q, int r)
    {
        float tw = hexSize * 2f;
        float th = Mathf.Sqrt(3f) * hexSize;
        return new Vector3(tw * (q + r * 0.5f), 0f, th * r);
    }

    Vector3Int AxialToCube(int q, int r)
    {
        int x = q, z = r, y = -x - z;
        return new Vector3Int(x, y, z);
    }

    public HexTile GetTile(Vector3Int coords)
    {
        tiles.TryGetValue(coords, out HexTile tile);
        return tile;
    }

    public HexTile GetTile(int x, int y)
    {
        foreach (var tile in tiles.Values)
            if (tile.cubeCoords.x == x && tile.cubeCoords.y == y) return tile;
        return null;
    }

    public int CubeDistance(Vector3Int a, Vector3Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), Mathf.Abs(a.z - b.z));

    public List<HexTile> GetTilesInRange(HexTile centerTile, int range)
    {
        List<HexTile> result = new List<HexTile>();
        for (int dx = -range; dx <= range; dx++)
            for (int dy = Mathf.Max(-range, -dx - range); dy <= Mathf.Min(range, -dx + range); dy++)
            {
                int dz = -dx - dy;
                Vector3Int c = centerTile.cubeCoords + new Vector3Int(dx, dy, dz);
                if (tiles.TryGetValue(c, out HexTile t)) result.Add(t);
            }
        return result;
    }

    public List<HexTile> GetTilesByCount(HexTile start, int count)
    {
        List<HexTile>    result   = new List<HexTile>();
        Queue<HexTile>   frontier = new Queue<HexTile>();
        HashSet<HexTile> visited  = new HashSet<HexTile>();
        frontier.Enqueue(start);
        visited.Add(start);
        while (frontier.Count > 0 && result.Count < count)
        {
            HexTile current = frontier.Dequeue();
            result.Add(current);
            foreach (HexTile n in GetNeighbors(current))
            {
                if (visited.Contains(n)) continue;
                visited.Add(n);
                frontier.Enqueue(n);
            }
        }
        return result;
    }

    public List<HexTile> FindPath(HexTile start, HexTile end)
    {
        if (start == end) return new List<HexTile> { start };
        Queue<HexTile>              frontier = new Queue<HexTile>();
        Dictionary<HexTile, HexTile> cameFrom = new Dictionary<HexTile, HexTile>();
        frontier.Enqueue(start);
        cameFrom[start] = null;
        bool found = false;
        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();
            if (current == end) { found = true; break; }
            foreach (HexTile next in GetNeighbors(current))
                if (!cameFrom.ContainsKey(next) && (!next.IsBuildingBlocked() || next == end))
                { cameFrom[next] = current; frontier.Enqueue(next); }
        }
        if (!found) return null;
        List<HexTile> path = new List<HexTile>();
        HexTile temp = end;
        while (temp != null) { path.Add(temp); temp = cameFrom[temp]; }
        path.Reverse();
        return path;
    }
}