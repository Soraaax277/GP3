using UnityEngine;
using System.Collections.Generic;

// A single building prefab entry with its own inspector-adjustable scale.
[System.Serializable]
public class EraBuilding
{
    [Tooltip("The building prefab to spawn. Its materials will never be overwritten.")]
    public GameObject prefab;
    [Tooltip("Scale applied to this specific building. Adjust per-model to match your art.")]
    public Vector3 scale = Vector3.one;
}

// One era's full set of buildings. A random entry is picked each time a building is spawned.
[System.Serializable]
public class EraBuildingSet
{
    public TurnManager.GameEra era;
    [Tooltip("All building variants for this era. One is chosen at random per spawn.")]
    public EraBuilding[] buildings;
}

// Pairs a GameEra with a single Material.
// Used for per-era city core concrete, urban fringe concrete, and road materials.
[System.Serializable]
public class EraMaterialSet
{
    public TurnManager.GameEra era;
    [Tooltip("Material applied when this era is active.")]
    public Material material;
}

// One entry in the nature prefab palette.
[System.Serializable]
public class NatureProp
{
    [Tooltip("The nature prefab to spawn (tree, rock, bush, etc.).")]
    public GameObject prefab;

    [Tooltip("When enabled this prop will gently sway using DOTween. " +
             "Intended for trees and tall foliage — disable for rocks, ruins, etc.")]
    public bool isSway = false;

    [Tooltip("Maximum lean angle (degrees) during the sway. 2–5 is subtle; 10+ is dramatic.")]
    [Range(0f, 20f)]
    public float swayAngle = 3f;

    [Tooltip("How many seconds one full sway cycle takes.")]
    [Range(0.5f, 10f)]
    public float swayDuration = 2.5f;
}

// Added at runtime to every sway-enabled nature prop.
// Uses DOTween to rock the transform gently on X and Z.
// A slight random phase offset per prop prevents all trees swaying in lockstep.
public class TreeSwayBehaviour : MonoBehaviour
{
    public float swayAngle            = 3f;
    public float swayDuration         = 2.5f;
    public float lodFarDistance       = 40f;
    public float subtleSwayMultiplier = 0.08f;

    private float      _timeOffset;
    private Quaternion _baseRot;
    private Vector3    _baseScale;

    private static Camera _cachedCam;
    private float  _intensity         = 1f;
    private float  _nextDistanceCheck = 0f;
    private const float DistCheckInterval = 0.5f;

    private void Start()
    {
        _baseRot    = transform.localRotation;
        _baseScale  = transform.localScale;
        _timeOffset = Random.Range(0f, 100f);

        if (_cachedCam == null) _cachedCam = Camera.main;
        _nextDistanceCheck = Time.time + Random.Range(0f, DistCheckInterval);
    }

    private void Update()
    {
        if (Time.time >= _nextDistanceCheck)
        {
            _nextDistanceCheck = Time.time + DistCheckInterval;
            if (_cachedCam == null) _cachedCam = Camera.main;

            if (_cachedCam != null)
            {
                float dist = Vector3.Distance(_cachedCam.transform.position, transform.position);
                _intensity = dist > lodFarDistance
                    ? subtleSwayMultiplier
                    : Mathf.Lerp(1f, subtleSwayMultiplier, dist / lodFarDistance);
            }
        }

        if (_intensity < 0.01f) return;

        float t    = (Time.time + _timeOffset) / swayDuration;
        float lean = Mathf.Sin(t * Mathf.PI * 2f)              * swayAngle * _intensity;
        float side = Mathf.Sin(t * Mathf.PI * 2f * 0.7f + 1f) * swayAngle * 0.5f * _intensity;

        transform.localRotation = _baseRot * Quaternion.Euler(lean, 0f, side);

        float scalePulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.02f * _intensity;
        transform.localScale = _baseScale * scalePulse;
    }
}

// Three-tier city zone:
//   coreTiles          — dense concrete urban center (TileType.City, high building density)
//   fringeTiles        — transitional urban ring    (TileType.City, medium building density,
//                         sparse nature allowed)
//   bleedTiles         — organic outer zone         (TileType.Land, nature-dominant,
//                         some concrete patches bleeding in)
//   bleedConcreteTiles — subset of bleedTiles that received a concrete material patch
//                         and are eligible for very sparse building placement.
[System.Serializable]
public class CityZone
{
    public HexTile centerTile;
    public int     currentRadius;

    public HashSet<HexTile> coreTiles          = new HashSet<HexTile>();
    public HashSet<HexTile> fringeTiles         = new HashSet<HexTile>();
    public HashSet<HexTile> bleedTiles          = new HashSet<HexTile>();
    public HashSet<HexTile> bleedConcreteTiles  = new HashSet<HexTile>();

    // Returns true for any tile that is inside any of the three urban tiers.
    public bool Contains(HexTile t) =>
        coreTiles.Contains(t) || fringeTiles.Contains(t) || bleedTiles.Contains(t);
}


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
    public int   width   = 75;
    public int   height  = 45;
    public float hexSize = 1f;

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
    public float sandBedScale = 1.02f;

    // ── Grass / land shader / material ───────────────────────────────────
    [Header("Land Visual")]
    [Tooltip("Assign the Material using your grass shader here.\n" +
             "GridManager will apply it to every land tile automatically.")]
    public Material grassMaterial;

    // ── City / urban materials ────────────────────────────────────────────
    [Header("City Tile Visual — Per Era")]
    [Tooltip("Concrete / asphalt material for city CORE tiles, one entry per era.\n" +
             "Core concrete should read as dense, dark urban pavement.\n" +
             "Falls back to a procedural dark-gray if no matching era is found.")]
    public EraMaterialSet[] eraConcreteMaterials;

    [Tooltip("Material for urban FRINGE tiles and bleed-zone concrete patches, one entry per era.\n" +
             "Should be visually lighter / less saturated than core concrete.\n" +
             "Falls back to a procedural light-gray if no matching era is found.")]
    public EraMaterialSet[] eraFringeMaterials;

    [Header("Environmental Building Era Sets")]
    [Tooltip("One entry per era. Each entry holds an array of building variants "
             + "to randomly pick from when spawning on city tiles.")]
    public EraBuildingSet[] eraBuildingSets;

    [Header("Building LOD Settings")]
    [Tooltip("Screen-relative size at which the building transitions from close-up LOD to distant LOD.")]
    [Range(0.001f, 0.5f)]
    public float lod0ScreenSize = 0.03f;
    [Tooltip("Screen-relative size at which shadows are turned off (mid LOD).")]
    [Range(0.0001f, 0.1f)]
    public float lod1ScreenSize = 0.008f;
    [Tooltip("Screen-relative size below which the object is culled entirely.")]
    [Range(0.0001f, 0.05f)]
    public float lodCullScreenSize = 0.001f;

    [Header("Building Scatter Settings")]
    [Tooltip("How much each building's scale can deviate from its configured entry.scale. " +
             "0.02 = ±2 %, 0.20 = ±20 %. Applied uniformly on all three axes.")]
    [Range(0f, 0.5f)]
    public float buildingScaleVariation = 0.02f;

    [Tooltip("How far from the tile centre a building may be placed, expressed as a " +
             "fraction of hexSize. 0 = always centred, 0.4 = up to 40 % of hex radius away.")]
    [Range(0f, 0.5f)]
    public float buildingPositionSpread = 0.35f;

    [Tooltip("When enabled each building gets a random 0–360° rotation around the Y axis " +
             "so clusters never look copy-pasted.")]
    public bool randomizeRotation = true;

    [Header("Building Population")]
    [Tooltip("Fraction of city tiles that start with buildings at the Industrial era. " +
             "This rate applies to city tiles; bleed tiles use bleedBuildingCoverage.")]
    [Range(0.05f, 1f)]
    public float initialBuildingCoverage = 0.35f;

    [Tooltip("Extra fraction of city tiles that gain buildings each time the world era " +
             "advances. 0.10 = +10 % per era.")]
    [Range(0f, 0.3f)]
    public float buildingCoveragePerEra = 0.10f;

    [Header("Nature Decoration")]
    [Tooltip("Drop any natural prop prefabs here — trees, rocks, bushes, etc. " +
             "Enable 'isSway' on tree entries for a gentle DOTween wind animation. " +
             "These ignore eras and are simply reshuffled when the era changes.")]
    public NatureProp[] naturePrefabs;

    [Tooltip("Fraction of wilderness land tiles that receive nature decorations.")]
    [Range(0f, 1f)]
    public float natureCoverage = 0.60f;

    [Tooltip("How many nature props are placed per decorated tile (min / max).")]
    public int natureCountMin = 1;
    public int natureCountMax = 5;

    [Tooltip("Base scale applied to all nature props.")]
    public Vector3 natureBaseScale = Vector3.one;

    [Tooltip("How wildly nature props can vary in scale. 0.40 = ±40 %.")]
    [Range(0f, 1f)]
    public float natureScaleVariation = 0.40f;

    [Tooltip("How far a nature prop can stray from the tile centre, as a fraction of hexSize.")]
    [Range(0f, 0.8f)]
    public float naturePositionSpread = 0.55f;

    [Tooltip("Allow nature props to tilt randomly on X and Z (±this many degrees).")]
    [Range(0f, 30f)]
    public float natureTiltRange = 8f;

    // ── Water body settings ───────────────────────────────────────────────
    [Header("Water Body Settings")]
    [Tooltip("Number of water blob seeds to scatter across the map. Each one grows into a lake.")]
    public int waterBlobCount = 6;

    [Tooltip("Minimum number of tiles in each water blob (smallest lake).")]
    [Range(1, 20)]
    public int waterBlobMinSize = 3;

    [Tooltip("Maximum number of tiles in each water blob (largest lake).")]
    [Range(2, 40)]
    public int waterBlobMaxSize = 9;

    [Tooltip("How many times more likely a wilderness tile is to receive a water blob seed " +
             "compared to a city tile. 6 = lakes are 6× more likely outside cities.")]
    [Range(1f, 20f)]
    public float wildernessWaterBias = 6f;

    [Tooltip("Minimum hex distance between water blob seeds so lakes distribute across the map.")]
    [Range(3, 20)]
    public int waterBlobMinSeparation = 8;

    // ── City zone settings ────────────────────────────────────────────────
    [Header("City Zone Settings")]
    [Tooltip("Number of city zones to scatter across the continent.")]
    public int cityCount = 4;

    [Tooltip("Starting hex radius for each city zone at the Industrial era.")]
    [Range(2, 8)]
    public int cityStartRadius = 3;

    [Tooltip("Minimum hex distance required between any two city centers.")]
    [Range(6, 30)]
    public int cityMinSeparation = 14;

    [Tooltip("Building coverage inside the city core (inner tiles). High = dense city block feel.")]
    [Range(0f, 1f)]
    public float urbanCoreCoverage = 0.88f;

    [Tooltip("Building coverage in the city fringe (outer ring). Lower = suburban thinning.")]
    [Range(0f, 1f)]
    public float urbanFringeCoverage = 0.55f;

    [Tooltip("Nature coverage inside the city fringe — sparse greenery among buildings.")]
    [Range(0f, 0.3f)]
    public float urbanNatureCoverage = 0.05f;

    [Tooltip("How many hex rings each city zone grows per era advance.")]
    [Range(0, 3)]
    public int cityRadiusGrowthPerEra = 1;

    [Tooltip("Extra hex rings beyond the city fringe that form the organic urban bleed zone.\n" +
             "These tiles stay TileType.Land (nature can spawn) but a noise-selected subset " +
             "receives concrete patches and sparse buildings, creating a dissolving urban edge.")]
    [Range(0, 4)]
    public int cityBleedRings = 2;

    [Tooltip("Perlin noise scale used to shape the jagged bleed-zone boundary.\n" +
             "Higher = more fragmented, spiky city edges.")]
    [Range(0.05f, 0.5f)]
    public float bleedNoiseScale = 0.18f;

    [Tooltip("Tiles within the bleed ring range are included only when their Perlin noise " +
             "value exceeds this threshold. Lower = larger, more solid bleed area.")]
    [Range(0.2f, 0.8f)]
    public float bleedNoiseThreshold = 0.42f;

    [Tooltip("Building spawn probability on urban bleed tiles.")]
    [Range(0f, 0.5f)]
    public float bleedBuildingCoverage = 0.12f;

    // ── Urban size override ───────────────────────────────────────────────
    [Header("Urban Size Control")]
    [Tooltip("Global scale multiplier applied to every city's radius AND bleed rings.\n\n" +
             "1.0  = default sizes defined above.\n" +
             "2.0  = cities twice as wide (core + fringe + bleed all scale up).\n" +
             "0.5  = compact cities.\n\n" +
             "After changing this value, right-click the GridManager component header\n" +
             "and choose  'Regenerate Grid (Same Seed)'  to rebuild the map in-place.")]
    [Range(0.5f, 3f)]
    public float urbanSizeMultiplier = 1f;

    // ── Road visual ───────────────────────────────────────────────────────
    [Header("Road Visual — Per Era")]
    [Tooltip("Road material per era, one entry per era you want styled.\n" +
             "Falls back to a procedural warm-dark→cool-pale tarmac progression if empty.")]
    public EraMaterialSet[] eraRoadMaterials;

    // ── Road generation ───────────────────────────────────────────────────
    [Header("Road Generation Settings")]
    [Tooltip("How much roads meander between cities and inside them.\n" +
             "0 = dead straight BFS paths, 6 = gently winding organic routes.")]
    [Range(0f, 8f)]
    public float roadWobble = 3f;

    [Tooltip("Number of internal spoke roads radiating from each city center to random " +
             "points on the fringe ring. Each city gets a unique layout every generation.")]
    [Range(2, 6)]
    public int cityRoadSpokes = 4;

    [Tooltip("Extra inter-city road connections added on top of the minimum spanning tree.\n" +
             "0 = strict tree with no loops; 2 = two bonus roads creating alternate routes.")]
    [Range(0, 4)]
    public int bonusRoadConnections = 1;

    // ─────────────────────────────────────────────────────────────────────
    public Dictionary<Vector3Int, HexTile> tiles =
        new Dictionary<Vector3Int, HexTile>();

    // Shared sand material — created once, reused for all sand beds.
    private Material _sandMaterial;

    // Tiles that must never receive env buildings or nature — HQ tile + its ring-1 neighbours.
    private readonly HashSet<HexTile> _hqExclusionZone = new HashSet<HexTile>();

    // City zones built during AssignEnvironmentFeatures, used throughout generation.
    private List<CityZone>   _cityZones = new List<CityZone>();

    // Tiles whose surface material has been set to road — excluded from buildings and nature.
    private HashSet<HexTile> _roadTiles = new HashSet<HexTile>();

    // All tiles that received a concrete material (core + fringe + bleedConcrete).
    // Maintained by MarkCityTiles; used by UpdateConcreteBorders to compute edge masks.
    private HashSet<HexTile> _concreteTiles = new HashSet<HexTile>();

    // Road border rendering — reused to avoid per-frame allocations.
    private MaterialPropertyBlock _roadMPB;
    private static readonly int ShaderEdgeMask     = Shader.PropertyToID("_EdgeMask");
    private static readonly int ShaderTileCenterWS  = Shader.PropertyToID("_TileCenterWS");

    // Concrete border rendering — same property names, separate MPB instance.
    private MaterialPropertyBlock _concreteMPB;

    // Tracks which era is currently active so RefreshEraBuildings can re-apply materials.
    private TurnManager.GameEra _currentEra = TurnManager.GameEra.Industrial;

    // Call this immediately after placing each player HQ.
    public void RegisterHQTile(HexTile hqTile)
    {
        if (hqTile == null) return;

        _hqExclusionZone.Add(hqTile);
        foreach (HexTile n in GetNeighbors(hqTile))
            _hqExclusionZone.Add(n);

        foreach (HexTile t in _hqExclusionZone)
            ClearEnvFromTile(t);
    }

    // =====================================================================
    //  URBAN SIZE CONTROL — right-click the component header in the Inspector
    //  and choose one of these options to rebuild the map without touching any
    //  other script.
    // =====================================================================

    /// <summary>
    /// Destroys the current map and rebuilds it from scratch using the SAME
    /// random seed, so the continent shape stays identical while the new
    /// urbanSizeMultiplier takes full effect.
    /// Use this after adjusting urbanSizeMultiplier (or any other generation
    /// setting) while the game is running in the Editor.
    /// </summary>
    [ContextMenu("Regenerate Grid (Same Seed)")]
    public void RegenerateGridSameSeed()
    {
        // Preserve the current seed so continent shape is unchanged.
        // seedSet stays true → GenerateGrid will re-use mapOffsetX/Y.
        RebuildMap();
    }

    /// <summary>
    /// Same as above but picks a brand-new random seed, giving a completely
    /// fresh continent shape in addition to applying any inspector changes.
    /// </summary>
    [ContextMenu("Regenerate Grid (New Seed)")]
    public void RegenerateGridNewSeed()
    {
        seedSet = false;   // force GenerateGrid to roll a new seed
        RebuildMap();
    }

    // Shared teardown + rebuild routine.
    private void RebuildMap()
    {
        // Tear down all existing tile GameObjects.
        foreach (HexTile tile in tiles.Values)
            if (tile != null) Destroy(tile.gameObject);

        tiles.Clear();
        _cityZones.Clear();
        _roadTiles.Clear();
        _concreteTiles.Clear();
        _hqExclusionZone.Clear();
        _sandMaterial = null;
        IsReady       = false;

        GenerateGrid();

        // Re-run the deferred nature pass.
        StartCoroutine(SpawnNatureAfterFrame());

        // Rebuild the boat perimeter spline to match the new continent shape.
        BoatManager.Instance?.RebuildSpline();

        Debug.Log($"[GridManager] Grid rebuilt — urbanSizeMultiplier = {urbanSizeMultiplier:F2}");
    }

    // Destroys all Env_Structure and Env_Nature children on a tile.
    private void ClearEnvFromTile(HexTile tile)
    {
        if (tile == null) return;
        for (int i = tile.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = tile.transform.GetChild(i);
            if (child.name.Contains("Env_Structure") || child.name.Contains("Env_Nature"))
                Destroy(child.gameObject);
        }
        tile.hasStructure = false;
    }

    // Returns true when a tile is inside the HQ exclusion zone.
    private bool IsHQZone(HexTile tile) => _hqExclusionZone.Contains(tile);

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GenerateGrid();
        // Nature must spawn AFTER all other Start() methods have run so that
        // HQs placed by BusinessSpawner/GameManager are already registered.
        StartCoroutine(SpawnNatureAfterFrame());
    }

    private System.Collections.IEnumerator SpawnNatureAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        SpawnNatureDecorations();
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

        Random.InitState((int)(mapOffsetX * 10f + mapOffsetY * 100f));

        Vector3 worldCenter = HexToWorld(width / 2, height / 2);
        float   maxRadius   = (Mathf.Min(width, height) / 2f) * (hexSize * 2f);

        // STEP 1: INITIAL NOISE GENERATION
        for (int q = 0; q < width; q++)
        {
            for (int r = 0; r < height; r++)
            {
                Vector3 worldPos = HexToWorld(q, r);

                float distFromCenter = Vector3.Distance(worldPos, worldCenter);
                float normalizedDist = distFromCenter / maxRadius;
                float noiseValue     = Mathf.PerlinNoise(
                    (q + mapOffsetX) * noiseScale,
                    (r + mapOffsetY) * noiseScale);
                float finalLandValue = noiseValue - (normalizedDist * edgeFalloff);

                if (finalLandValue >= landThreshold)
                {
                    Vector3Int cubeCoords = AxialToCube(q, r);
                    GameObject tileObj = Instantiate(
                        hexTilePrefab,
                        worldPos - worldCenter,
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
        try
        {
            AssignEnvironmentFeatures();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GridManager] AssignEnvironmentFeatures failed — {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }

        // STEP 3: POST-PROCESSING (REMOVE DETACHED ISLANDS)
        RemoveDisconnectedIslands();

        // STEP 4: SAND BEDS — absolute last step after all tile types are locked.
        // Every tile is now confirmed Water / Land / City / Road.
        // SpawnSandBed has its own type guard so this is exception-safe.
        SpawnAllSandBeds();

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

        // ── STEP A: Generate city zones FIRST ────────────────────────────
        // Must run before water so blob seeding knows which tiles are urban.
        GenerateCityZones(allTiles);

        // ── STEP B: Spawn cohesive water bodies biased toward wilderness ──
        SpawnWaterBodies(allTiles);

        // ── Rebuild live tile list after water conversion ─────────────────
        allTiles = new List<HexTile>(tiles.Values);
        allTiles.RemoveAll(t => t.type == HexTile.TileType.Water);
        allTiles.RemoveAll(t => IsHQZone(t));

        // ── Re-populate city zones after water ────────────────────────────
        foreach (CityZone zone in _cityZones)
            PopulateZoneTiles(zone);

        // ── STEP B.5: Stamp city tile type + concrete material ────────────
        // Must run before roads so road tiles inside cities can override to Road.
        _currentEra = TurnManager.GameEra.Industrial;
        MarkCityTiles(_currentEra);

        // ── Structure material ────────────────────────────────────────────
        if (structureMaterial == null)
        {
            Shader structShader = Shader.Find("Universal Render Pipeline/Lit");
            if (structShader == null) structShader = Shader.Find("Sprites/Default");
            structureMaterial       = new Material(structShader);
            structureMaterial.color = new Color(0.8f, 0.8f, 0.85f);
        }

        // ── STEP C: Generate roads ────────────────────────────────────────
        GenerateRoads(TurnManager.GameEra.Industrial);

        // ── STEP D: Spawn buildings on city core and fringe tiles ─────────
        // Only TileType.City tiles host env buildings.
        // Road tiles are hard-blocked — nothing may spawn on them.
        foreach (HexTile tile in allTiles)
        {
            if (tile.type != HexTile.TileType.City) continue;
            if (IsRoadBlocked(tile))   continue;
            if (IsHQZone(tile))        continue;
            if (Random.value < GetUrbanCoverage(tile))
                SpawnStructures(tile);
        }

        // ── STEP E: Sparse buildings on bleed concrete patches ────────────
        // Bleed tiles stay TileType.Land but their concrete-patched subset
        // can host occasional buildings, making the urban edge feel inhabited.
        foreach (CityZone zone in _cityZones)
        {
            foreach (HexTile tile in zone.bleedConcreteTiles)
            {
                if (tile.type == HexTile.TileType.Water) continue;
                if (IsRoadBlocked(tile)) continue;
                if (IsHQZone(tile))      continue;
                if (tile.hasStructure)   continue;
                if (Random.value < bleedBuildingCoverage)
                    SpawnStructures(tile);
            }
        }

    }

    // =====================================================================
    //  WATER BODY SPAWNING
    // =====================================================================

    private void SpawnWaterBodies(List<HexTile> allTiles)
    {
        List<HexTile> wildernessTiles = new List<HexTile>();
        List<HexTile> cityTiles       = new List<HexTile>();

        foreach (HexTile t in allTiles)
        {
            if (IsInAnyCity(t)) cityTiles.Add(t);
            else                wildernessTiles.Add(t);
        }

        HashSet<HexTile>  converted  = new HashSet<HexTile>();
        List<Vector3Int>  seedCoords = new List<Vector3Int>();

        for (int blob = 0; blob < waterBlobCount; blob++)
        {
            HexTile seed = PickWaterSeed(wildernessTiles, cityTiles, converted, seedCoords);
            if (seed == null) break;

            seedCoords.Add(seed.cubeCoords);

            int           targetSize = Random.Range(waterBlobMinSize, waterBlobMaxSize + 1);
            List<HexTile> blobTiles  = GrowWaterBlob(seed, targetSize, converted);

            foreach (HexTile t in blobTiles)
            {
                converted.Add(t);
                ConvertTileToWater(t);
            }
        }

        Debug.Log($"[GridManager] Spawned {seedCoords.Count} water blobs.");
    }

    private HexTile PickWaterSeed(
        List<HexTile>    wilderness,
        List<HexTile>    city,
        HashSet<HexTile> excluded,
        List<Vector3Int> existingSeeds)
    {
        float wildWeight  = wilderness.Count * wildernessWaterBias;
        float cityWeight  = city.Count;
        float totalWeight = wildWeight + cityWeight;

        if (totalWeight <= 0f) return null;

        List<HexTile> primary;
        List<HexTile> fallback;
        if (Random.value * totalWeight < wildWeight)
        { primary = wilderness; fallback = city; }
        else
        { primary = city; fallback = wilderness; }

        return FindValidSeed(primary,  excluded, existingSeeds)
            ?? FindValidSeed(fallback, excluded, existingSeeds);
    }

    private HexTile FindValidSeed(
        List<HexTile>    candidates,
        HashSet<HexTile> excluded,
        List<Vector3Int> existingSeeds)
    {
        List<HexTile> shuffled = new List<HexTile>(candidates);
        ShuffleList(shuffled);

        foreach (HexTile candidate in shuffled)
        {
            if (excluded.Contains(candidate)) continue;
            if (IsInCityCore(candidate))      continue;

            bool tooClose = false;
            foreach (Vector3Int s in existingSeeds)
            {
                if (CubeDistance(candidate.cubeCoords, s) < waterBlobMinSeparation)
                { tooClose = true; break; }
            }
            if (tooClose) continue;

            return candidate;
        }
        return null;
    }

    private List<HexTile> GrowWaterBlob(
        HexTile          seed,
        int              targetSize,
        HashSet<HexTile> alreadyWater)
    {
        List<HexTile>    blob     = new List<HexTile>();
        Queue<HexTile>   frontier = new Queue<HexTile>();
        HashSet<HexTile> inBlob   = new HashSet<HexTile>();

        frontier.Enqueue(seed);
        inBlob.Add(seed);

        while (frontier.Count > 0 && blob.Count < targetSize)
        {
            HexTile current = frontier.Dequeue();
            blob.Add(current);

            List<HexTile> neighbors = GetNeighbors(current);
            ShuffleList(neighbors);

            foreach (HexTile n in neighbors)
            {
                if (inBlob.Contains(n))       continue;
                if (alreadyWater.Contains(n)) continue;
                if (IsInCityCore(n))          continue;
                inBlob.Add(n);
                frontier.Enqueue(n);
            }
        }

        return blob;
    }

    // Converts a single land tile to a water tile.
    // Sand beds are NOT spawned here — they are placed in a dedicated deferred pass
    // (SpawnAllSandBeds) after ALL tile types have been finalized. This guarantees
    // sand only ever appears under confirmed water tiles, never on land.
    private void ConvertTileToWater(HexTile tile)
    {
        if (tile == null) return;

        Vector3    pos    = tile.transform.position;
        Vector3Int coords = tile.cubeCoords;

        if (waterTilePrefab != null)
        {
            tiles.Remove(coords);
            Destroy(tile.gameObject);

            GameObject waterObj = Instantiate(
                waterTilePrefab,
                pos,
                hexTilePrefab.transform.rotation,
                transform);
            HexTile waterTile = waterObj.GetComponent<HexTile>();
            waterTile.Initialize(coords, HexTile.TileType.Water);
            tiles.Add(coords, waterTile);

            ApplyWaterMaterial(waterObj);
            // ── NO SpawnSandBed here — deferred to SpawnAllSandBeds() ──
        }
        else
        {
            tile.type = HexTile.TileType.Water;
            ApplyWaterMaterial(tile.gameObject);
            // ── NO SpawnSandBed here — deferred to SpawnAllSandBeds() ──
            tile.UpdateAppearance();
        }
    }

    // Returns true when the tile sits inside any city zone's core ring (not fringe or bleed).
    private bool IsInCityCore(HexTile tile)
    {
        foreach (CityZone zone in _cityZones)
            if (zone.coreTiles.Contains(tile)) return true;
        return false;
    }

    // Hard road lock — returns true when a tile is a road and must never receive
    // buildings, nature, or any other spawned content.
    // Checks BOTH the _roadTiles set (which persists across era rebuilds) AND
    // tile.type (which is stamped Road during GenerateRoads) so the guard works
    // whether called before or after road generation.
    private bool IsRoadBlocked(HexTile tile) =>
        tile.type == HexTile.TileType.Road || _roadTiles.Contains(tile);

    // =====================================================================
    //  DEFERRED SAND BED PASS
    // =====================================================================

    // Scans every tile currently in the map and places a sand bed directly
    // underneath each confirmed water tile.
    //
    // This runs ONCE at the very end of GenerateGrid, after ALL tile types
    // (water, grass, city, road) are fully finalized. It is the ONLY place
    // sand beds are ever created — ConvertTileToWater never spawns them inline.
    //
    // The method first destroys any pre-existing "SandBed" children on water
    // tiles (defensive cleanup in case of re-entry) before placing fresh ones,
    // guaranteeing exactly one sand bed per water tile and none on land.
    private void SpawnAllSandBeds()
    {
        // ── Pass 1: destroy any stale sand beds on water tiles ────────────
        foreach (HexTile tile in tiles.Values)
        {
            if (tile.type != HexTile.TileType.Water) continue;
            for (int i = tile.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = tile.transform.GetChild(i);
                if (child.name == "SandBed") Destroy(child.gameObject);
            }
        }

        // ── Pass 2: place one sand bed under every confirmed water tile ───
        // SpawnSandBed has its own Water type guard — this double-check is intentional.
        int sandCount = 0;
        foreach (HexTile tile in tiles.Values)
        {
            if (tile.type != HexTile.TileType.Water) continue;
            SpawnSandBed(tile);
            sandCount++;
        }
        Debug.Log($"[GridManager] Placed {sandCount} sand beds under water tiles.");
    }

    // =====================================================================
    //  CITY TILE TYPING
    // =====================================================================

    // Stamps every non-water, non-road core/fringe tile with TileType.City and applies
    // the era-correct concrete material.
    // Bleed concrete tiles receive the fringe material but stay TileType.Land.
    // Called with era = Industrial on first generation; called again on each era advance.
    private void MarkCityTiles(TurnManager.GameEra era)
    {
        // Rebuild the concrete tile set fresh each time (era change can shift tiles).
        _concreteTiles.Clear();

        foreach (CityZone zone in _cityZones)
        {
            // ── Core tiles: dense urban concrete ─────────────────────────
            foreach (HexTile t in zone.coreTiles)
            {
                if (t.type == HexTile.TileType.Water) continue;
                if (t.type == HexTile.TileType.Road)  continue;
                t.type = HexTile.TileType.City;
                ApplyCityMaterial(t.gameObject, isCore: true, era);
                _concreteTiles.Add(t);
            }

            // ── Fringe tiles: lighter urban concrete ──────────────────────
            foreach (HexTile t in zone.fringeTiles)
            {
                if (t.type == HexTile.TileType.Water) continue;
                if (t.type == HexTile.TileType.Road)  continue;
                t.type = HexTile.TileType.City;
                ApplyCityMaterial(t.gameObject, isCore: false, era);
                _concreteTiles.Add(t);
            }

            // ── Bleed concrete patches: fringe material, land type stays ──
            // These tiles remain TileType.Land so nature can still spawn on them.
            foreach (HexTile t in zone.bleedConcreteTiles)
            {
                if (t.type == HexTile.TileType.Water) continue;
                if (t.type == HexTile.TileType.Road)  continue;
                // Do NOT change TileType — bleed tiles remain Land
                ApplyCityMaterial(t.gameObject, isCore: false, era);
                _concreteTiles.Add(t);
            }
        }

        UpdateConcreteBorders();

        Debug.Log($"[GridManager] City tiles stamped for era {era}.");
    }

    // Applies the appropriate era-correct concrete material to a city tile.
    // isCore = true  → looks up eraConcreteMaterials  (dark, dense pavement)
    // isCore = false → looks up eraFringeMaterials     (lighter, transitional)
    // Falls back to a procedurally created gray if no era entry is assigned.
    private void ApplyCityMaterial(GameObject tileObj, bool isCore, TurnManager.GameEra era)
    {
        Renderer rend = tileObj.GetComponent<Renderer>();
        if (rend == null) return;

        Material mat = isCore
            ? GetConcreteMaterial(era)
            : GetFringeMaterial(era);

        if (mat != null)
        {
            rend.material = mat;
            return;
        }

        // Procedural fallback: core = dark urban concrete, fringe = lighter suburban
        Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                 ?? Shader.Find("Standard")
                 ?? Shader.Find("Sprites/Default");
        if (sh == null) return;

        Material fallback = new Material(sh);
        fallback.color = isCore
            ? new Color(0.52f, 0.52f, 0.54f, 1f)   // dark core concrete
            : new Color(0.66f, 0.64f, 0.60f, 1f);   // lighter suburban
        rend.material = fallback;
    }

    // Looks up the era-matched material from eraConcreteMaterials.
    // Falls back to the first assigned entry, then null (triggers procedural fallback).
    private Material GetConcreteMaterial(TurnManager.GameEra era)
    {
        if (eraConcreteMaterials != null)
        {
            foreach (var e in eraConcreteMaterials)
                if (e != null && e.era == era && e.material != null) return e.material;
            // No exact era match — return first assigned as fallback
            foreach (var e in eraConcreteMaterials)
                if (e?.material != null) return e.material;
        }
        return null;
    }

    // Looks up the era-matched material from eraFringeMaterials.
    private Material GetFringeMaterial(TurnManager.GameEra era)
    {
        if (eraFringeMaterials != null)
        {
            foreach (var e in eraFringeMaterials)
                if (e != null && e.era == era && e.material != null) return e.material;
            foreach (var e in eraFringeMaterials)
                if (e?.material != null) return e.material;
        }
        return null;
    }

    // =====================================================================
    //  NATURE DECORATIONS
    // =====================================================================

    // Spawns nature props across the map using a three-tier coverage system:
    //   Wilderness land    → natureCoverage        (dense)
    //   Bleed concrete     → urbanNatureCoverage   (sparse)
    //   City fringe        → urbanNatureCoverage   (sparse, greenery among buildings)
    //   City core / Road   → no nature
    public void SpawnNatureDecorations()
    {
        if (naturePrefabs == null || naturePrefabs.Length == 0) return;

        foreach (HexTile tile in tiles.Values)
        {
            if (tile.IsBuildingBlocked()) continue;
            if (IsHQZone(tile))           continue;
            if (IsRoadBlocked(tile))       continue;

            float coverage = GetNatureCoverage(tile);
            if (coverage > 0f && Random.value < coverage)
                SpawnNatureOnTile(tile);
        }
    }

    // Returns the nature spawn probability for a tile based on its tier.
    private float GetNatureCoverage(HexTile tile)
    {
        switch (tile.type)
        {
            case HexTile.TileType.Water:
            case HexTile.TileType.Road:
                return 0f;

            case HexTile.TileType.Land:
                // Bleed concrete patches get reduced urban coverage;
                // pure wilderness land gets full coverage.
                foreach (CityZone zone in _cityZones)
                    if (zone.bleedConcreteTiles.Contains(tile)) return urbanNatureCoverage;
                return natureCoverage;

            case HexTile.TileType.City:
                // Only the fringe ring gets sparse nature; the core stays clean concrete.
                foreach (CityZone zone in _cityZones)
                    if (zone.fringeTiles.Contains(tile)) return urbanNatureCoverage;
                return 0f;

            default:
                return 0f;
        }
    }

    // Removes all Env_Nature children from every tile and re-places them at new
    // random positions. Called by RefreshEraBuildings on era transition.
    public void ReshuffleNatureDecorations()
    {
        if (naturePrefabs == null || naturePrefabs.Length == 0) return;

        List<HexTile> decoratedTiles = new List<HexTile>();
        foreach (var tile in tiles.Values)
        {
            bool hadNature = false;
            for (int i = tile.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = tile.transform.GetChild(i);
                if (child.name.Contains("Env_Nature"))
                {
                    Destroy(child.gameObject);
                    hadNature = true;
                }
            }
            if (hadNature) decoratedTiles.Add(tile);
        }

        foreach (HexTile tile in decoratedTiles)
        {
            if (GetNatureCoverage(tile) <= 0f) continue;
            if (tile.IsBuildingBlocked())       continue;
            if (IsHQZone(tile))                continue;
            if (IsRoadBlocked(tile))            continue;
            SpawnNatureOnTile(tile);
        }
    }

    // Spawns nature props on a single tile.
    private void SpawnNatureOnTile(HexTile tile)
    {
        int count = Random.Range(
            Mathf.Max(1, natureCountMin),
            Mathf.Max(2, natureCountMax + 1));

        float spread       = hexSize * naturePositionSpread;
        float tileSurfaceY = tile.transform.position.y;
        Renderer tileRenderer = tile.GetComponent<Renderer>();
        if (tileRenderer != null) tileSurfaceY = tileRenderer.bounds.max.y;

        for (int i = 0; i < count; i++)
        {
            NatureProp entry = naturePrefabs[Random.Range(0, naturePrefabs.Length)];
            if (entry == null || entry.prefab == null) continue;
            SpawnSingleNatureProp(tile, entry, spread, tileSurfaceY);
        }
    }

    // Instantiates one nature prop, applies wild scale + full Y rotation + optional tilt,
    // grounds it flush with the tile surface, then parents it to the tile.
    private void SpawnSingleNatureProp(HexTile tile, NatureProp entry,
                                       float spread, float tileSurfaceY)
    {
        float rx = Random.Range(-spread, spread);
        float ry = Random.Range(-spread, spread);

        GameObject obj = Instantiate(entry.prefab);
        obj.name = "Env_Nature";

        float sx = natureBaseScale.x * Random.Range(1f - natureScaleVariation, 1f + natureScaleVariation);
        float sy = natureBaseScale.y * Random.Range(1f - natureScaleVariation, 1f + natureScaleVariation);
        float sz = natureBaseScale.z * Random.Range(1f - natureScaleVariation, 1f + natureScaleVariation);
        obj.transform.localScale = new Vector3(sx, sy, sz);

        float yRot  = Random.Range(0f, 360f);
        float xTilt = Random.Range(-natureTiltRange, natureTiltRange);
        float zTilt = Random.Range(-natureTiltRange, natureTiltRange);

        obj.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        obj.transform.position      = tile.transform.position;

        // ── Ground flush: measure bounds while upright ────────────────────
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

        obj.transform.localRotation = Quaternion.Euler(xTilt, yRot, zTilt);

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        Material[][] savedMats = new Material[renderers.Length][];
        for (int r = 0; r < renderers.Length; r++)
            savedMats[r] = renderers[r].sharedMaterials;

        obj.transform.SetParent(tile.transform, true);

        for (int r = 0; r < renderers.Length; r++)
            renderers[r].sharedMaterials = savedMats[r];

        foreach (var col in obj.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        if (entry.isSway)
        {
            TreeSwayBehaviour sway = obj.AddComponent<TreeSwayBehaviour>();
            sway.swayAngle    = entry.swayAngle;
            sway.swayDuration = entry.swayDuration;
        }

        SetupLOD(obj);

        if (!tile.isExplored)
            obj.SetActive(false);
    }

    // =====================================================================
    //  WATER MATERIAL APPLICATION
    // =====================================================================
    private void ApplyWaterMaterial(GameObject waterObj)
    {
        Renderer rend = waterObj.GetComponent<Renderer>();
        if (rend == null) return;

        if (waterMaterial != null)
        {
            rend.material = waterMaterial;
        }
        else
        {
            Debug.LogWarning("[GridManager] waterMaterial not assigned — using fallback blue.");
            Shader fb = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
            if (fb != null)
            {
                Material mat  = new Material(fb);
                mat.color     = new Color(0.1f, 0.3f, 0.78f, 0.85f);
                rend.material = mat;
            }
        }
    }

    // Applies the grass/land material to a land tile's Renderer.
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

    // Spawns a sand-colored hex tile as a child of a water tile, offset downward
    // by sandBedDepth so it appears as a visible seabed.
    //
    // HARD CONTRACT: sand beds are ONLY ever spawned here, and ONLY when the
    // tile is confirmed to be Water. They are never placed on land tiles.
    private void SpawnSandBed(HexTile waterTile)
    {
        // Safety guard — must be a water tile. Should always be true at call sites,
        // but this prevents any accidental call from landing on a land tile.
        if (waterTile == null || waterTile.type != HexTile.TileType.Water) return;
        if (hexTilePrefab == null) return;

        // ── World-space position: always sandBedDepth world-Y units below ────
        // CRITICAL: Do NOT use localPosition to offset the sand. Hex tile prefabs
        // are commonly exported from Blender with a -90° X rotation, which means
        // local Y points in world -Z (not world -Y). Any localPosition.y offset
        // therefore moves the sand horizontally, NOT downward — producing the
        // "sand appearing on the map" bug.
        //
        // Instead, compute the target world position first, pass it to Instantiate,
        // and let Unity resolve the localPosition automatically. This is correct
        // regardless of what rotation the prefab has.
        Vector3 sandWorldPos = waterTile.transform.position - new Vector3(0f, sandBedDepth, 0f);

        GameObject sandObj = Instantiate(
            hexTilePrefab,
            sandWorldPos,                  // world position — always directly below water tile
            waterTile.transform.rotation,  // match parent orientation
            waterTile.transform);          // parent: worldPositionStays=true by default

        sandObj.name = "SandBed";

        // DO NOT override localPosition — Unity already computed the correct value
        // from sandWorldPos above. Overriding it would reintroduce the wrong-axis bug.
        sandObj.transform.localRotation = Quaternion.identity;
        sandObj.transform.localScale    = Vector3.one * sandBedScale;

        // Remove HexTile script so the sand bed cannot be selected or acted upon.
        HexTile sandTileScript = sandObj.GetComponent<HexTile>();
        if (sandTileScript != null)
            Destroy(sandTileScript);

        // Remove all colliders — sand bed is purely visual.
        foreach (Collider col in sandObj.GetComponentsInChildren<Collider>())
            Destroy(col);

        // Destroy any child objects the prefab might carry (other tiles, props, etc.).
        for (int i = sandObj.transform.childCount - 1; i >= 0; i--)
            Destroy(sandObj.transform.GetChild(i).gameObject);

        // Apply a unique sand color variant derived from the water tile's coordinates.
        Renderer rend = sandObj.GetComponent<Renderer>();
        if (rend != null)
            rend.material = BuildSandVariant(waterTile.cubeCoords);
    }

    // =====================================================================
    //  SAND MATERIAL HELPERS
    // =====================================================================
    private void BuildSandMaterial()
    {
        if (_sandMaterial != null) return;

        Shader sandShader = Shader.Find("Custom/URP/SandBed")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
        _sandMaterial       = new Material(sandShader);
        _sandMaterial.color = SandColor(Vector3Int.zero, 0f);
    }

    private Material BuildSandVariant(Vector3Int coords)
    {
        Shader sandShader = Shader.Find("Custom/URP/SandBed")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

        float hash = Mathf.Abs(Mathf.Sin(coords.x * 127.1f + coords.z * 311.7f));
        hash       = hash - Mathf.Floor(hash);

        Material mat = new Material(sandShader);
        mat.color    = SandColor(coords, hash);

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
        Color baseSand  = new Color(0.80f, 0.70f, 0.48f, 1f);
        Color wetSand   = new Color(0.55f, 0.52f, 0.38f, 1f);
        Color lightSand = new Color(0.92f, 0.86f, 0.68f, 1f);

        if (variation > 0.85f) return Color.Lerp(baseSand, lightSand, (variation - 0.85f) / 0.15f);
        return Color.Lerp(wetSand, baseSand, variation);
    }

    // =====================================================================
    //  STRUCTURE SPAWNING HELPERS
    // =====================================================================

    private EraBuildingSet GetEraBuildingSet(TurnManager.GameEra era)
    {
        if (eraBuildingSets == null) return null;
        foreach (var set in eraBuildingSets)
            if (set != null && set.era == era) return set;
        return null;
    }

    private EraBuilding PickRandomBuilding(EraBuildingSet set)
    {
        if (set == null || set.buildings == null || set.buildings.Length == 0) return null;
        return set.buildings[Random.Range(0, set.buildings.Length)];
    }

    private int PickBuildingCount()
    {
        float r = Random.value;
        if (r < 0.30f) return 1;
        if (r < 0.70f) return 2;
        return 3;
    }

    private Vector2[] BuildSlotOffsets(int count, float radius)
    {
        Vector2[] slots  = new Vector2[count];
        float     jitter = radius * 0.18f;

        if (count == 1)
        {
            slots[0] = new Vector2(
                Random.Range(-jitter, jitter),
                Random.Range(-jitter, jitter));
        }
        else
        {
            float startAngle = Random.Range(0f, 360f);
            float step       = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angleDeg = startAngle + step * i;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                slots[i] = new Vector2(
                    Mathf.Cos(angleRad) * radius + Random.Range(-jitter, jitter),
                    Mathf.Sin(angleRad) * radius + Random.Range(-jitter, jitter));
            }
        }
        return slots;
    }

    // =====================================================================
    //  STRUCTURE SPAWNING
    // =====================================================================

    private void SpawnStructures(HexTile tile)
    {
        if (tile.type == HexTile.TileType.Water) return;
        if (IsRoadBlocked(tile)) return;   // hard road lock — nothing spawns on roads
        if (IsHQZone(tile)) return;
        tile.hasStructure = true;
        EraBuildingSet set    = GetEraBuildingSet(TurnManager.GameEra.Industrial);
        int            count  = PickBuildingCount();
        float          radius = hexSize * buildingPositionSpread;
        Vector2[]      slots  = BuildSlotOffsets(count, radius);

        for (int i = 0; i < count; i++)
            SpawnSingleBuilding(tile, PickRandomBuilding(set), slots[i].x, slots[i].y);
    }

    private void SpawnStructuresForEra(HexTile tile, EraBuildingSet set)
    {
        if (tile == null || set == null) return;
        if (tile.type == HexTile.TileType.Water) return;
        if (IsRoadBlocked(tile)) return;   // hard road lock — nothing spawns on roads
        if (IsHQZone(tile)) return;
        tile.hasStructure = true;
        int       count  = PickBuildingCount();
        float     radius = hexSize * buildingPositionSpread;
        Vector2[] slots  = BuildSlotOffsets(count, radius);
        for (int i = 0; i < count; i++)
            SpawnSingleBuilding(tile, PickRandomBuilding(set), slots[i].x, slots[i].y);
    }

    // Swaps all environmental buildings to the current era prefabs,
    // expands coverage, grows city zones, and rebuilds roads + materials.
    public void RefreshEraBuildings(TurnManager.GameEra era)
    {
        _currentEra = era;
        EraBuildingSet set      = GetEraBuildingSet(era);
        int            eraIndex = (int)era;

        // ══════════════════════════════════════════════════════════════════
        // CRITICAL ORDER:
        //   1. Update zone radii + tile populations (needed by road pathing)
        //   2. Stamp city materials
        //   3. Regenerate roads  ← _roadTiles must be FINAL before buildings
        //   4. Then place / refresh buildings (IsRoadBlocked is now accurate)
        //   5. Reshuffle nature
        // ══════════════════════════════════════════════════════════════════

        // ── Step 1: Update city zone radii and repopulate tile sets ───────
        // Snapshots capture the pre-growth footprint so Step 4 can distinguish
        // new tiles from tiles that already had buildings in the previous era.
        var expansionSnapshots =
            new Dictionary<CityZone, (HashSet<HexTile> urban, HashSet<HexTile> bleed)>();

        if (eraIndex > 0 && cityRadiusGrowthPerEra > 0)
        {
            foreach (CityZone zone in _cityZones)
            {
                var prevUrban = new HashSet<HexTile>(zone.coreTiles);
                prevUrban.UnionWith(zone.fringeTiles);
                var prevBleed = new HashSet<HexTile>(zone.bleedConcreteTiles);
                expansionSnapshots[zone] = (prevUrban, prevBleed);

                zone.currentRadius = Mathf.Max(1, Mathf.RoundToInt(
                    (cityStartRadius + eraIndex * cityRadiusGrowthPerEra) * urbanSizeMultiplier));
                PopulateZoneTiles(zone);
            }
        }

        // ── Step 2: Re-apply era concrete materials ────────────────────────
        MarkCityTiles(era);

        // ── Step 3: Rebuild roads — MUST happen before ANY building placement ──
        // GenerateRoads clears and repopulates _roadTiles from scratch.
        // Every building check below therefore sees the current, correct road set.
        GenerateRoads(era);

        // ── Step 4a: Refresh buildings on tiles that already have structures ──
        // Road tiles get their buildings stripped; others get era-swapped prefabs.
        foreach (var tile in tiles.Values)
        {
            if (tile.type == HexTile.TileType.Water) continue;
            if (IsHQZone(tile))      continue;
            if (!tile.hasStructure)  continue;

            // Road check with CURRENT _roadTiles (rebuilt in Step 3 above).
            if (IsRoadBlocked(tile))
            {
                for (int i = tile.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = tile.transform.GetChild(i);
                    if (child.name.Contains("Env_Structure")) Destroy(child.gameObject);
                }
                tile.hasStructure = false;
                continue;
            }

            for (int i = tile.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = tile.transform.GetChild(i);
                if (child.name.Contains("Env_Structure")) Destroy(child.gameObject);
            }

            int       count  = PickBuildingCount();
            float     radius = hexSize * buildingPositionSpread;
            Vector2[] slots  = BuildSlotOffsets(count, radius);
            for (int i = 0; i < count; i++)
                SpawnSingleBuilding(tile, PickRandomBuilding(set), slots[i].x, slots[i].y);
        }

        // ── Step 4b: Expand building coverage on existing city tiles ──────
        if (eraIndex > 0)
        {
            var emptyLandTiles = new List<HexTile>();
            foreach (var tile in tiles.Values)
                if (tile.type == HexTile.TileType.City && !tile.hasStructure
                    && !IsHQZone(tile) && !IsRoadBlocked(tile))
                    emptyLandTiles.Add(tile);

            if (emptyLandTiles.Count > 0)
            {
                int   totalTiles     = tiles.Count;
                float targetCoverage = Mathf.Clamp01(initialBuildingCoverage + eraIndex * buildingCoveragePerEra);
                int   targetCount    = Mathf.RoundToInt(totalTiles * targetCoverage);
                int   existingCount  = totalTiles - emptyLandTiles.Count;
                int   toAdd          = Mathf.Max(0, targetCount - existingCount);

                for (int i = 0; i < toAdd; i++)
                {
                    if (emptyLandTiles.Count == 0) break;
                    int     idx  = Random.Range(0, emptyLandTiles.Count);
                    HexTile tile = emptyLandTiles[idx];
                    emptyLandTiles.RemoveAt(idx);

                    tile.hasStructure = true;
                    int       count  = PickBuildingCount();
                    float     radius = hexSize * buildingPositionSpread;
                    Vector2[] slots  = BuildSlotOffsets(count, radius);
                    for (int j = 0; j < count; j++)
                        SpawnSingleBuilding(tile, PickRandomBuilding(set), slots[j].x, slots[j].y);
                }

                Debug.Log($"[GridManager] Era {era}: coverage target {targetCoverage * 100f:F0}% — added {toAdd} new structure tiles.");
            }
        }

        // ── Step 4c: Buildings on newly expanded city tiles ───────────────
        // Uses IsRoadBlocked which now reflects the roads rebuilt in Step 3.
        if (eraIndex > 0 && cityRadiusGrowthPerEra > 0)
        {
            foreach (CityZone zone in _cityZones)
            {
                if (!expansionSnapshots.TryGetValue(zone, out var snap)) continue;
                var (prevUrban, prevBleed) = snap;

                // New core tiles
                foreach (HexTile tile in zone.coreTiles)
                {
                    if (prevUrban.Contains(tile))            continue;
                    if (tile.type == HexTile.TileType.Water) continue;
                    if (tile.hasStructure)                   continue;
                    if (IsHQZone(tile))                      continue;
                    if (IsRoadBlocked(tile))                 continue;  // ← road lock
                    if (Random.value < urbanCoreCoverage)
                        SpawnStructuresForEra(tile, set);
                }

                // New fringe tiles
                foreach (HexTile tile in zone.fringeTiles)
                {
                    if (prevUrban.Contains(tile))            continue;
                    if (tile.type == HexTile.TileType.Water) continue;
                    if (tile.hasStructure)                   continue;
                    if (IsHQZone(tile))                      continue;
                    if (IsRoadBlocked(tile))                 continue;  // ← road lock
                    if (Random.value < urbanFringeCoverage)
                        SpawnStructuresForEra(tile, set);
                }

                // New bleed concrete patches
                foreach (HexTile tile in zone.bleedConcreteTiles)
                {
                    if (prevBleed.Contains(tile))             continue;
                    if (tile.type == HexTile.TileType.Water)  continue;
                    if (IsRoadBlocked(tile))                  continue;  // ← road lock
                    if (IsHQZone(tile))                       continue;
                    if (!tile.hasStructure && Random.value < bleedBuildingCoverage)
                        SpawnStructuresForEra(tile, set);
                }
            }
        }

        // ── Step 5: Reshuffle nature props to a fresh random layout ──────
        ReshuffleNatureDecorations();
    }

    // Instantiates one building at the given XZ slot offset relative to the tile centre.
    private void SpawnSingleBuilding(HexTile tile, EraBuilding entry, float rx, float ry)
    {
        if (entry != null && entry.prefab != null)
        {
            GameObject obj = Instantiate(entry.prefab);
            obj.name = "Env_Structure";

            float scaleMult = Random.Range(1f - buildingScaleVariation, 1f + buildingScaleVariation);
            obj.transform.localScale    = entry.scale * scaleMult;

            float yRot = randomizeRotation ? Random.Range(0f, 360f) : 0f;
            obj.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            obj.transform.position      = tile.transform.position;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            Material[][] savedMats = new Material[renderers.Length][];
            for (int r = 0; r < renderers.Length; r++)
                savedMats[r] = renderers[r].sharedMaterials;

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

            Collider[] cols = obj.GetComponentsInChildren<Collider>(true);

            obj.transform.position = new Vector3(
                tile.transform.position.x + rx,
                tile.transform.position.y + yOffset,
                tile.transform.position.z + ry);

            obj.transform.SetParent(tile.transform, true);

            foreach (var col in cols)
                col.enabled = false;

            for (int r = 0; r < renderers.Length; r++)
                renderers[r].sharedMaterials = savedMats[r];

            SetupLOD(obj);

            if (!tile.isExplored)
                obj.SetActive(false);
        }
        else
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Env_Structure";
            obj.transform.SetParent(tile.transform);

            float h = Random.Range(0.006f, 0.018f);
            float w = Random.Range(0.0025f, 0.0045f);
            obj.transform.localPosition = new Vector3(rx, ry, -h / 2f - 0.001f);
            obj.transform.localScale    = new Vector3(w, w, h);
            float yRotFallback = randomizeRotation ? Random.Range(0f, 360f) : 0f;
            obj.transform.localRotation = Quaternion.Euler(0f, 0f, yRotFallback);

            if (obj.TryGetComponent<Collider>(out Collider col))
                col.enabled = false;

            SetupLOD(obj);

            if (!tile.isExplored)
                obj.SetActive(false);
        }
    }

    // Adds a three-level LODGroup to objects that don't have one already.
    private void SetupLOD(GameObject obj)
    {
        if (obj.GetComponentInChildren<LODGroup>(true) != null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        foreach (var r in renderers)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows    = true;
        }
        LOD lod0 = new LOD(lod0ScreenSize, renderers);

        foreach (var r in renderers)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows    = false;
        }
        LOD lod1 = new LOD(lod1ScreenSize, renderers);

        LOD lod2    = new LOD(lodCullScreenSize, renderers);
        LOD lodCull = new LOD(0f, new Renderer[0]);

        foreach (var r in renderers)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows    = true;
        }

        LODGroup lodGroup = obj.AddComponent<LODGroup>();
        lodGroup.SetLODs(new LOD[] { lod0, lod1, lod2, lodCull });
        lodGroup.RecalculateBounds();
        lodGroup.fadeMode           = LODFadeMode.None;
        lodGroup.animateCrossFading = false;
    }

    // =====================================================================
    //  ISLAND REMOVAL
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
    //  CITY ZONES
    // =====================================================================

    private void GenerateCityZones(List<HexTile> landTiles)
    {
        _cityZones.Clear();

        List<HexTile> candidates = new List<HexTile>(landTiles);
        ShuffleList(candidates);

        List<HexTile> chosenCenters = new List<HexTile>();
        foreach (HexTile candidate in candidates)
        {
            if (_cityZones.Count >= cityCount) break;
            if (IsHQZone(candidate)) continue;

            bool tooClose = false;
            foreach (HexTile center in chosenCenters)
            {
                if (CubeDistance(candidate.cubeCoords, center.cubeCoords) < cityMinSeparation)
                { tooClose = true; break; }
            }
            if (tooClose) continue;

            CityZone zone = new CityZone
            {
                centerTile    = candidate,
                currentRadius = Mathf.Max(1, Mathf.RoundToInt(cityStartRadius * urbanSizeMultiplier))
            };
            PopulateZoneTiles(zone);
            _cityZones.Add(zone);
            chosenCenters.Add(candidate);
        }

        Debug.Log($"[GridManager] Placed {_cityZones.Count} city zones.");
    }

    // (Re)fills all three tiers of a zone from its currentRadius.
    //
    // Core   = rings 0 .. currentRadius-1   — always solid, dark concrete.
    //
    // Fringe = ring currentRadius           — NOISE FILTERED with a low threshold
    //          (~0.22) so ~78% of tiles survive, giving the city a naturally
    //          ragged outer boundary instead of a clean hexagonal ring.
    //          Passing tiles become TileType.City with full building eligibility.
    //
    // Bleed  = cityBleedRings rings BEYOND currentRadius — TileType.Land stays,
    //          but noise-filtered so the shape is ragged, not a clean hexagonal halo.
    //          Noise threshold tightens with distance so urban influence fades.
    //          ALL tiles that pass noise → bleedTiles AND bleedConcreteTiles.
    private void PopulateZoneTiles(CityZone zone)
    {
        zone.coreTiles.Clear();
        zone.fringeTiles.Clear();
        zone.bleedTiles.Clear();
        zone.bleedConcreteTiles.Clear();

        int coreRadius     = Mathf.Max(1, zone.currentRadius - 1);
        int scaledBleedRings = Mathf.Max(0, Mathf.RoundToInt(cityBleedRings * urbanSizeMultiplier));
        int bleedMaxRadius = zone.currentRadius + scaledBleedRings;

        foreach (HexTile t in GetTilesInRange(zone.centerTile, bleedMaxRadius))
        {
            if (t.type == HexTile.TileType.Water) continue;
            if (t.type == HexTile.TileType.Road)  continue;

            int dist = CubeDistance(t.cubeCoords, zone.centerTile.cubeCoords);

            // ── Solid city core ───────────────────────────────────────────
            if (dist <= coreRadius)
            {
                zone.coreTiles.Add(t);
                continue;
            }

            // ── Outermost city fringe ring — noise-filtered for organic edge ──
            // A low threshold (0.22) keeps ~78 % of tiles, breaking the ring
            // into a ragged boundary that naturally blends into the bleed zone.
            // Tiles that pass are still fully urban (TileType.City, buildings OK).
            if (dist == zone.currentRadius)
            {
                float nx    = t.transform.position.x * bleedNoiseScale + mapOffsetX * 0.01f;
                float nz    = t.transform.position.z * bleedNoiseScale + mapOffsetY * 0.01f;
                float noise = Mathf.PerlinNoise(nx, nz);
                if (noise < 0.22f) continue; // drop ~22% of fringe tiles → ragged city edge
                zone.fringeTiles.Add(t);
                continue;
            }

            // ── Urban bleed zone (beyond the city radius) ─────────────────
            // Perlin noise determines which tiles survive — producing a jagged,
            // organic border rather than a clean hexagonal ring.
            // The threshold increases with distance so the zone dissolves
            // progressively into nature instead of stopping abruptly.
            if (scaledBleedRings > 0 && dist <= bleedMaxRadius)
            {
                float bleedFraction = (float)(dist - zone.currentRadius) / scaledBleedRings; // 0..1
                float threshold     = Mathf.Lerp(bleedNoiseThreshold, 0.78f, bleedFraction);

                float nx    = t.transform.position.x * bleedNoiseScale + mapOffsetX * 0.01f;
                float nz    = t.transform.position.z * bleedNoiseScale + mapOffsetY * 0.01f;
                float noise = Mathf.PerlinNoise(nx, nz);

                if (noise < threshold) continue; // tile stays pure nature

                // Tile passed — it is urban bleed.
                // ALL passing bleed tiles get concrete material (bleedConcreteTiles).
                // The noise shape itself creates the broken, irregular edge.
                zone.bleedTiles.Add(t);
                zone.bleedConcreteTiles.Add(t);
            }
        }
    }

    // Returns a deterministic 0..1 float derived from cube coordinates.
    // Used wherever a stable per-tile probability is needed (no Random.value).
    private static float CoordHash(Vector3Int c)
    {
        float h = Mathf.Abs(Mathf.Sin(c.x * 127.1f + c.y * 74.7f + c.z * 311.7f));
        return h - Mathf.Floor(h);
    }

    // Returns the building coverage rate for a tile.
    private float GetUrbanCoverage(HexTile tile)
    {
        if (tile.type == HexTile.TileType.City)
        {
            foreach (CityZone zone in _cityZones)
            {
                if (zone.coreTiles.Contains(tile))   return urbanCoreCoverage;
                if (zone.fringeTiles.Contains(tile)) return urbanFringeCoverage;
            }
        }
        // Bleed concrete tiles (TileType.Land) use bleedBuildingCoverage.
        // This is checked externally in the bleed spawn pass; returning 0 here is intentional
        // so the general city building loop does not accidentally pick them up.
        return 0f;
    }

    private bool IsInAnyCity(HexTile tile)
    {
        foreach (CityZone zone in _cityZones)
            if (zone.Contains(tile)) return true;
        return false;
    }

    // =====================================================================
    //  ROADS
    // =====================================================================

    // Builds road tiles in two stages:
    //
    // 1. INTRA-CITY: For each city, pick cityRoadSpokes random hub tiles on the
    //    outermost fringe ring, then find a noise-wobbled path from the center
    //    to each hub. This gives every city a unique, organic internal road layout
    //    instead of the rigid 6-spoke asterisk.
    //
    // 2. INTER-CITY: Build a minimum spanning tree across all city centers (Kruskal's)
    //    so every city is reachable with the fewest roads. Then add bonusRoadConnections
    //    extra random edges to create loops and alternate routes. Each path uses
    //    FindWobblyLandPath so inter-city roads meander naturally.
    private void GenerateRoads(TurnManager.GameEra era)
    {
        _roadTiles.Clear();

        // ── Intra-city: random spoke roads to fringe ring ─────────────────
        foreach (CityZone zone in _cityZones)
        {
            if (zone.centerTile.type != HexTile.TileType.Water)
            {
                _roadTiles.Add(zone.centerTile);
                zone.centerTile.type = HexTile.TileType.Road;
            }

            // Collect all tiles on the outermost fringe ring
            List<HexTile> fringeRing = GetRingTiles(zone.centerTile, zone.currentRadius);
            ShuffleList(fringeRing);

            int spokes    = Mathf.Min(cityRoadSpokes, fringeRing.Count);
            int connected = 0;

            foreach (HexTile hub in fringeRing)
            {
                if (connected >= spokes) break;
                if (hub.type == HexTile.TileType.Water) continue;

                List<HexTile> path = FindWobblyLandPath(zone.centerTile, hub);
                if (path != null)
                {
                    foreach (HexTile t in path)
                    {
                        if (t.type == HexTile.TileType.Water) continue;
                        _roadTiles.Add(t);
                        t.type = HexTile.TileType.Road;
                    }
                    connected++;
                }
            }
        }

        // ── Inter-city: minimum spanning tree + bonus connections ─────────
        if (_cityZones.Count < 2)
        {
            ApplyRoadMaterials(era);
            return;
        }

        // Build edge list (all city pairs, sorted by hex distance)
        var edges = new List<(int i, int j, int dist)>();
        for (int i = 0; i < _cityZones.Count; i++)
            for (int j = i + 1; j < _cityZones.Count; j++)
            {
                int d = CubeDistance(
                    _cityZones[i].centerTile.cubeCoords,
                    _cityZones[j].centerTile.cubeCoords);
                edges.Add((i, j, d));
            }
        edges.Sort((a, b) => a.dist.CompareTo(b.dist));

        // Kruskal's MST: greedily add edges that connect a new city
        var connected2    = new HashSet<int> { 0 };
        var mstEdges      = new HashSet<(int, int)>();

        while (connected2.Count < _cityZones.Count)
        {
            bool progress = false;
            foreach (var (i, j, d) in edges)
            {
                bool iConn = connected2.Contains(i);
                bool jConn = connected2.Contains(j);
                if (iConn == jConn) continue; // skip if both connected or both isolated
                int newCity = iConn ? j : i;
                connected2.Add(newCity);
                mstEdges.Add((Mathf.Min(i, j), Mathf.Max(i, j)));
                progress = true;
                break;
            }
            if (!progress) break; // disconnected graph (shouldn't happen on one continent)
        }

        // Add bonus edges from unused city pairs for loops and alternate routes
        var unusedEdges = new List<(int i, int j, int dist)>();
        foreach (var e in edges)
        {
            var key = (Mathf.Min(e.i, e.j), Mathf.Max(e.i, e.j));
            if (!mstEdges.Contains(key)) unusedEdges.Add(e);
        }
        ShuffleList(unusedEdges);
        int bonusAdded = 0;
        foreach (var (i, j, d) in unusedEdges)
        {
            if (bonusAdded >= bonusRoadConnections) break;
            mstEdges.Add((Mathf.Min(i, j), Mathf.Max(i, j)));
            bonusAdded++;
        }

        // Path all selected city-pair edges
        foreach (var (i, j) in mstEdges)
        {
            List<HexTile> road = FindWobblyLandPath(
                _cityZones[i].centerTile,
                _cityZones[j].centerTile);

            if (road != null)
            {
                foreach (HexTile t in road)
                {
                    if (t.type == HexTile.TileType.Water) continue;
                    _roadTiles.Add(t);
                    t.type = HexTile.TileType.Road;
                }
            }
        }

        ApplyRoadMaterials(era);
    }

    // Returns all tiles that lie exactly on the given ring radius from center.
    // Uses the standard hex ring-walk (start at one corner, traverse 6 sides).
    private List<HexTile> GetRingTiles(HexTile center, int radius)
    {
        var ring = new List<HexTile>();
        if (radius == 0)
        {
            ring.Add(center);
            return ring;
        }

        // Start position: move 'radius' steps in direction 4 (–x, +z)
        Vector3Int cursor = center.cubeCoords;
        for (int k = 0; k < radius; k++)
            cursor += CubeDirections[4];

        // Walk the ring: 6 sides, each of length 'radius'
        for (int side = 0; side < 6; side++)
        {
            for (int step = 0; step < radius; step++)
            {
                if (tiles.TryGetValue(cursor, out HexTile t))
                    ring.Add(t);
                cursor += CubeDirections[side];
            }
        }

        return ring;
    }

    // Dijkstra pathfinding that avoids water and applies a per-tile noise cost
    // so paths meander organically rather than following the shortest straight line.
    // The noise is coordinate-derived and thus fully deterministic.
    private List<HexTile> FindWobblyLandPath(HexTile start, HexTile end)
    {
        if (start == end) return new List<HexTile> { start };

        var dist     = new Dictionary<HexTile, float>();
        var cameFrom = new Dictionary<HexTile, HexTile>();
        // Priority list — small enough maps make a sorted list acceptable here.
        var frontier = new List<(float cost, HexTile tile)>();

        dist[start]     = 0f;
        cameFrom[start] = null;
        frontier.Add((0f, start));

        while (frontier.Count > 0)
        {
            // Pop the lowest-cost entry
            frontier.Sort((a, b) => a.cost.CompareTo(b.cost));
            var (cost, current) = frontier[0];
            frontier.RemoveAt(0);

            if (current == end) break;

            // Skip stale entries
            if (dist.TryGetValue(current, out float bestSoFar) && cost > bestSoFar + 0.001f)
                continue;

            foreach (HexTile next in GetNeighbors(current))
            {
                if (next.type == HexTile.TileType.Water) continue;

                float tileCost = 1f + CoordHash(next.cubeCoords) * roadWobble;
                float newCost  = cost + tileCost;

                if (!dist.TryGetValue(next, out float prev) || newCost < prev)
                {
                    dist[next]     = newCost;
                    cameFrom[next] = current;
                    frontier.Add((newCost, next));
                }
            }
        }

        if (!cameFrom.ContainsKey(end)) return null;

        var path = new List<HexTile>();
        HexTile temp = end;
        while (temp != null) { path.Add(temp); temp = cameFrom[temp]; }
        path.Reverse();
        return path;
    }

    private void ApplyRoadMaterials(TurnManager.GameEra era)
    {
        Material mat = GetRoadMaterial(era);
        foreach (HexTile t in _roadTiles)
        {
            Renderer rend = t.GetComponent<Renderer>();
            if (rend != null && mat != null) rend.material = mat;
        }

        UpdateRoadBorders();
    }

    // Pushes a 6-bit edge mask into the HexRoad shader for every road tile via
    // MaterialPropertyBlock. Bit i = 1 means that edge borders a non-road tile
    // and the shader draws a white border on that side only.
    //
    // The shader uses object-space XZ (positionOS.xz passed through from vert),
    // so the tile centre is always the object-space origin — no _TileCenterWS
    // uniform is needed or set here.
    //
    // IMPORTANT: _EdgeMask must be declared at global scope in the shader (outside
    // CBUFFER_START(UnityPerMaterial)) or the SRP Batcher will own it and
    // MaterialPropertyBlock writes will be silently discarded.
    private void UpdateRoadBorders()
    {
        if (_roadMPB == null) _roadMPB = new MaterialPropertyBlock();

        foreach (HexTile tile in _roadTiles)
        {
            Renderer rend = tile.GetComponent<Renderer>();
            if (rend == null) continue;

            int mask = 0;
            for (int i = 0; i < CubeDirections.Length; i++)
            {
                Vector3Int nc       = tile.cubeCoords + CubeDirections[i];
                HexTile    neighbor = GetTile(nc);
                bool       isEdge   = neighbor == null ||
                                      neighbor.type != HexTile.TileType.Road;
                if (isEdge) mask |= (1 << i);
            }

            rend.GetPropertyBlock(_roadMPB);
            _roadMPB.SetFloat(ShaderEdgeMask, (float)mask);
            _roadMPB.SetVector(ShaderTileCenterWS, new Vector4(
                tile.transform.position.x, 0f,
                tile.transform.position.z, 0f));
            rend.SetPropertyBlock(_roadMPB);
        }
    }

    // Pushes a 6-bit edge mask into the HexConcrete shader for every concrete tile via
    // MaterialPropertyBlock. Bit i = 1 means that edge borders a non-concrete tile
    // (grass, water, road, or map edge) and the shader draws a border on that side only.
    //
    // "Concrete" means any tile in _concreteTiles — core, fringe, and bleed concrete
    // patches are all treated as one connected surface so the border traces the
    // outer silhouette of the entire urban zone.
    //
    // IMPORTANT: _EdgeMask and _TileCenterWS must be declared at global scope in the
    // shader (outside CBUFFER_START(UnityPerMaterial)) or the SRP Batcher will own
    // them and MaterialPropertyBlock writes will be silently discarded.
    private void UpdateConcreteBorders()
    {
        if (_concreteMPB == null) _concreteMPB = new MaterialPropertyBlock();

        foreach (HexTile tile in _concreteTiles)
        {
            Renderer rend = tile.GetComponent<Renderer>();
            if (rend == null) continue;

            int mask = 0;
            for (int i = 0; i < CubeDirections.Length; i++)
            {
                Vector3Int nc       = tile.cubeCoords + CubeDirections[i];
                HexTile    neighbor = GetTile(nc);
                // Border if neighbour is absent (map edge) or not part of the concrete zone.
                bool isEdge = neighbor == null || !_concreteTiles.Contains(neighbor);
                if (isEdge) mask |= (1 << i);
            }

            rend.GetPropertyBlock(_concreteMPB);
            _concreteMPB.SetFloat(ShaderEdgeMask, (float)mask);
            _concreteMPB.SetVector(ShaderTileCenterWS, new Vector4(
                tile.transform.position.x, 0f,
                tile.transform.position.z, 0f));
            rend.SetPropertyBlock(_concreteMPB);
        }
    }
    // Falls back to the first assigned entry, then to a procedural tarmac.
    private Material GetRoadMaterial(TurnManager.GameEra era)
    {
        if (eraRoadMaterials != null)
        {
            foreach (var e in eraRoadMaterials)
                if (e != null && e.era == era && e.material != null) return e.material;
            foreach (var e in eraRoadMaterials)
                if (e?.material != null) return e.material;
        }

        // Procedural fallback: warm dark tarmac (Industrial) → pale cool concrete (Futuristic)
        Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                 ?? Shader.Find("Standard")
                 ?? Shader.Find("Sprites/Default");
        if (sh == null) return null;

        Material fallback = new Material(sh);
        float t2 = Mathf.Clamp01((float)(int)era / 3f);
        fallback.color = Color.Lerp(
            new Color(0.35f, 0.33f, 0.30f),
            new Color(0.60f, 0.62f, 0.65f),
            t2);
        return fallback;
    }

    // =====================================================================
    //  UTILITY HELPERS
    // =====================================================================

    // In-place Fisher-Yates shuffle for any List<T>.
    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Simple BFS path that only avoids water (used as a fallback; road generation
    // uses FindWobblyLandPath instead).
    private List<HexTile> FindLandPath(HexTile start, HexTile end)
    {
        if (start == end) return new List<HexTile> { start };
        var frontier = new Queue<HexTile>();
        var cameFrom = new Dictionary<HexTile, HexTile>();
        frontier.Enqueue(start);
        cameFrom[start] = null;
        bool found = false;
        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();
            if (current == end) { found = true; break; }
            foreach (HexTile next in GetNeighbors(current))
                if (!cameFrom.ContainsKey(next) && next.type != HexTile.TileType.Water)
                { cameFrom[next] = current; frontier.Enqueue(next); }
        }
        if (!found) return null;
        var path = new List<HexTile>();
        HexTile temp = end;
        while (temp != null) { path.Add(temp); temp = cameFrom[temp]; }
        path.Reverse();
        return path;
    }

    // =====================================================================
    //  PUBLIC ACCESSORS
    // =====================================================================
    public IEnumerable<HexTile> GetAllTiles() => tiles.Values;

    // Exposes the road tile set to GridVehicleManager (and any other system
    // that needs to know where roads are without re-scanning all tiles).
    public IEnumerable<HexTile> GetRoadTiles() => _roadTiles;

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
        Queue<HexTile>               frontier = new Queue<HexTile>();
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