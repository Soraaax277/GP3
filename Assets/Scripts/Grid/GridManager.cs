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
    public float swayAngle           = 3f;
    public float swayDuration        = 2.5f;
    public float lodFarDistance      = 40f;
    public float subtleSwayMultiplier = 0.08f;

    private float      _timeOffset;
    private Quaternion _baseRot;
    private Vector3    _baseScale;

    // Cache camera and throttle the expensive distance check to every 0.5 s
    private static Camera _cachedCam;
    private float  _intensity        = 1f;
    private float  _nextDistanceCheck = 0f;
    private const float DistCheckInterval = 0.5f;

    private void Start()
    {
        _baseRot    = transform.localRotation;
        _baseScale  = transform.localScale;
        _timeOffset = Random.Range(0f, 100f);

        if (_cachedCam == null) _cachedCam = Camera.main;
        // Stagger first check so all trees don't evaluate on the same frame
        _nextDistanceCheck = Time.time + Random.Range(0f, DistCheckInterval);
    }

    private void Update()
    {
        // Re-evaluate distance only every DistCheckInterval seconds
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

        // Skip transform writes entirely when intensity is negligible — saves CPU
        if (_intensity < 0.01f) return;

        float t    = (Time.time + _timeOffset) / swayDuration;
        float lean = Mathf.Sin(t * Mathf.PI * 2f)           * swayAngle * _intensity;
        float side = Mathf.Sin(t * Mathf.PI * 2f * 0.7f + 1f) * swayAngle * 0.5f * _intensity;

        transform.localRotation = _baseRot * Quaternion.Euler(lean, 0f, side);

        float scalePulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.02f * _intensity;
        transform.localScale = _baseScale * scalePulse;
    }
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

    [Header("Environmental Building Era Sets")]
    [Tooltip("One entry per era. Each entry holds an array of building variants "
             + "to randomly pick from when spawning on land tiles.")]
    public EraBuildingSet[] eraBuildingSets;

    [Header("Building LOD Settings")]
    [Tooltip("Screen-relative size (0..1) at which the building transitions from the " +
             "close-up LOD to the distant LOD. 0.03 = switches when the building fills " +
             "3% of screen height. Only used on prefabs with no LODGroup of their own.")]
    [Range(0.001f, 0.5f)]
    public float lod0ScreenSize = 0.03f;
    [Tooltip("Screen-relative size at which shadows are turned off (mid LOD).")]
    [Range(0.0001f, 0.1f)]
    public float lod1ScreenSize = 0.008f;
    [Tooltip("Screen-relative size below which the object is culled entirely. " +
             "Keeping this small (0.001) means it only vanishes when it's a single pixel. " +
             "This is the biggest performance lever — raise it to cull more aggressively.")]
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
    [Tooltip("Fraction of land tiles that start with buildings at the Industrial era. " +
             "0.35 = 35 %. Halved from the original 70 % to keep the map sparse early on.")]
    [Range(0.05f, 1f)]
    public float initialBuildingCoverage = 0.35f;

    [Tooltip("Extra fraction of land tiles that gain buildings each time the world era " +
             "advances. 0.10 = +10 % per era, so by Futuristic the map is at ~65 %.")]
    [Range(0f, 0.3f)]
    public float buildingCoveragePerEra = 0.10f;

    [Header("Nature Decoration")]
    [Tooltip("Drop any natural prop prefabs here — trees, rocks, bushes, etc. " +
             "Enable 'isSway' on tree entries for a gentle DOTween wind animation. " +
             "These ignore eras and are simply reshuffled when the era changes.")]
    public NatureProp[] naturePrefabs;

    [Tooltip("Fraction of land tiles that receive nature decorations. " +
             "Can overlap with building tiles — nature spawns independently.")]
    [Range(0f, 1f)]
    public float natureCoverage = 0.60f;

    [Tooltip("How many nature props are placed per decorated tile (min / max).")]
    public int natureCountMin = 1;
    public int natureCountMax = 5;

    [Tooltip("Base scale applied to all nature props. " +
             "Acts as the centre of the random scale range.")]
    public Vector3 natureBaseScale = Vector3.one;

    [Tooltip("How wildly nature props can vary in scale. " +
             "0.40 = ±40 % — much more unrestrained than buildings.")]
    [Range(0f, 1f)]
    public float natureScaleVariation = 0.40f;

    [Tooltip("How far a nature prop can stray from the tile centre, as a fraction of hexSize. " +
             "Higher than buildings — nature can spill toward tile edges.")]
    [Range(0f, 0.8f)]
    public float naturePositionSpread = 0.55f;

    [Tooltip("Allow nature props to tilt randomly on X and Z (±this many degrees). " +
             "Gives trees and rocks a natural leaning look.")]
    [Range(0f, 30f)]
    public float natureTiltRange = 8f;

    // ─────────────────────────────────────────────────────────────────────
    public Dictionary<Vector3Int, HexTile> tiles =
        new Dictionary<Vector3Int, HexTile>();

    // Shared sand material — created once, reused for all sand beds
    private Material _sandMaterial;

    // Tiles that must never receive env buildings or nature — HQ tile + its ring-1 neighbours.
    // Populated by RegisterHQTile(), persists for the lifetime of the map.
    private readonly HashSet<HexTile> _hqExclusionZone = new HashSet<HexTile>();

    // Call this immediately after placing each player HQ.
    // Marks the HQ tile and all six neighbours as permanently off-limits for
    // both environmental buildings and nature decorations, and destroys any
    // that were already spawned there.
    public void RegisterHQTile(HexTile hqTile)
    {
        if (hqTile == null) return;

        _hqExclusionZone.Add(hqTile);
        foreach (HexTile n in GetNeighbors(hqTile))
            _hqExclusionZone.Add(n);

        // Strip any env objects that may have landed there before the HQ was placed
        foreach (HexTile t in _hqExclusionZone)
            ClearEnvFromTile(t);
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
        // HQs placed by BusinessSpawner/GameManager are already registered on
        // their tiles (placedNode != null). WaitForEndOfFrame guarantees that.
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

        int landTilesWithStructures = Mathf.RoundToInt(allTiles.Count * initialBuildingCoverage);
        // Hard filter — allTiles was built before water conversion ran, so some
        // entries may now be water. Strip them out before picking spawn targets.
        allTiles.RemoveAll(t => t.type == HexTile.TileType.Water);
        allTiles.RemoveAll(t => IsHQZone(t));
        landTilesWithStructures = Mathf.RoundToInt(allTiles.Count * initialBuildingCoverage);

        for (int i = 0; i < landTilesWithStructures; i++)
        {
            if (allTiles.Count == 0) break;
            int index = Random.Range(0, allTiles.Count);
            SpawnStructures(allTiles[index]);
            allTiles.RemoveAt(index);
        }

        // NOTE: Nature decorations are NOT spawned here.
        // SpawnNatureDecorations() must be called AFTER all HQs/bases have been
        // placed so that IsBuildingBlocked() correctly skips those tiles.
    }

    // =====================================================================
    //  NATURE DECORATIONS
    // =====================================================================

    // Populates a random subset of land tiles with nature props (trees, rocks, etc.).
    // IMPORTANT: Call this AFTER all player HQs have been placed so that
    // IsBuildingBlocked() correctly excludes base tiles (placedNode is set by then).
    // Water tiles and tiles occupied by a player building/HQ are always skipped.
    public void SpawnNatureDecorations()
    {
        if (naturePrefabs == null || naturePrefabs.Length == 0) return;

        List<HexTile> landTiles = GetEligibleNatureTiles();
        int toDecorate = Mathf.RoundToInt(landTiles.Count * natureCoverage);

        for (int i = 0; i < toDecorate; i++)
        {
            if (landTiles.Count == 0) break;
            int     idx  = Random.Range(0, landTiles.Count);
            HexTile tile = landTiles[idx];
            landTiles.RemoveAt(idx);
            SpawnNatureOnTile(tile);
        }
    }

    // Returns all tiles that are safe for nature spawning:
    // must be land (not water) and must not be blocked by a player building or HQ.
    private List<HexTile> GetEligibleNatureTiles()
    {
        List<HexTile> result = new List<HexTile>();
        foreach (var tile in tiles.Values)
        {
            if (tile.type == HexTile.TileType.Water) continue;
            if (tile.IsBuildingBlocked())            continue;
            if (IsHQZone(tile))                      continue;
            result.Add(tile);
        }
        return result;
    }

    // Removes all Env_Nature children from every tile and re-places them at new
    // random positions. Called by RefreshEraBuildings so the world feels stirred
    // on an era transition without changing which prefabs are used.
    // Tiles that have become water or been claimed by a player since last spawn are skipped.
    public void ReshuffleNatureDecorations()
    {
        if (naturePrefabs == null || naturePrefabs.Length == 0) return;

        // Collect which tiles already had nature, destroy their props
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

        // Re-spawn — skip any tile that is now water, blocked, or inside an HQ zone
        foreach (HexTile tile in decoratedTiles)
        {
            if (tile.type == HexTile.TileType.Water) continue;
            if (tile.IsBuildingBlocked())            continue;
            if (IsHQZone(tile))                      continue;
            SpawnNatureOnTile(tile);
        }
    }

    // Spawns nature props on a single tile. Count is random within natureCountMin/Max.
    // Props are scattered freely across the tile surface with organic tilt and scale variation.
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
    // If the NatureProp entry has isSway enabled, a TreeSwayBehaviour is added.
    private void SpawnSingleNatureProp(HexTile tile, NatureProp entry,
                                        float spread, float tileSurfaceY)
    {
        float rx = Random.Range(-spread, spread);
        float ry = Random.Range(-spread, spread);

        GameObject obj = Instantiate(entry.prefab);
        obj.name = "Env_Nature";

        // ── Scale first — bounds depend on it ────────────────────────────────
        float sx = natureBaseScale.x * Random.Range(1f - natureScaleVariation, 1f + natureScaleVariation);
        float sy = natureBaseScale.y * Random.Range(1f - natureScaleVariation, 1f + natureScaleVariation);
        float sz = natureBaseScale.z * Random.Range(1f - natureScaleVariation, 1f + natureScaleVariation);
        obj.transform.localScale = new Vector3(sx, sy, sz);

        // ── Store intended tilt but DON'T apply it yet ────────────────────────
        // X/Z tilt shifts the bounding box min-Y, which inflates yOffset and makes
        // the prop float. We measure bounds upright (Y rotation only), then tilt.
        float yRot  = Random.Range(0f, 360f);
        float xTilt = Random.Range(-natureTiltRange, natureTiltRange);
        float zTilt = Random.Range(-natureTiltRange, natureTiltRange);

        // Only Y rotation for the bounds pass — no lean
        obj.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        obj.transform.position      = tile.transform.position;

        // ── Ground flush: measure bounds while upright ────────────────────────
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

        // ── Set final XZ scatter position with correct Y ──────────────────────
        obj.transform.position = new Vector3(
            tile.transform.position.x + rx,
            tile.transform.position.y + yOffset,
            tile.transform.position.z + ry);

        // ── NOW apply the X/Z tilt — purely cosmetic at this point ───────────
        obj.transform.localRotation = Quaternion.Euler(xTilt, yRot, zTilt);

        // ── Snapshot materials ────────────────────────────────────────────────
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        Material[][] savedMats = new Material[renderers.Length][];
        for (int r = 0; r < renderers.Length; r++)
            savedMats[r] = renderers[r].sharedMaterials;

        // ── Parent after final transform is set ──────────────────────────────
        obj.transform.SetParent(tile.transform, true);

        // ── Restore materials ─────────────────────────────────────────────────
        for (int r = 0; r < renderers.Length; r++)
            renderers[r].sharedMaterials = savedMats[r];

        // ── Disable colliders ─────────────────────────────────────────────────
        foreach (var col in obj.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // ── Sway animation (trees / foliage only) ────────────────────────────
        if (entry.isSway)
        {
            TreeSwayBehaviour sway = obj.AddComponent<TreeSwayBehaviour>();
            sway.swayAngle    = entry.swayAngle;
            sway.swayDuration = entry.swayDuration;
        }

        // ── LOD ───────────────────────────────────────────────────────────────
        SetupLOD(obj);
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

    // Returns a new material instance with a unique sand color tinted per-tile
    // using a deterministic hash of the tile's cube coordinates.
    // Range: warm tan → slightly greenish damp sand.
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
    //  STRUCTURE SPAWNING
    // =====================================================================

    // Returns the EraBuildingSet for the given era, or null if not configured.
    private EraBuildingSet GetEraBuildingSet(TurnManager.GameEra era)
    {
        if (eraBuildingSets == null) return null;
        foreach (var set in eraBuildingSets)
            if (set != null && set.era == era) return set;
        return null;
    }

    // Picks a random EraBuilding from a set, or null if the set is empty.
    private EraBuilding PickRandomBuilding(EraBuildingSet set)
    {
        if (set == null || set.buildings == null || set.buildings.Length == 0) return null;
        return set.buildings[Random.Range(0, set.buildings.Length)];
    }

    // =====================================================================
    //  BUILDING COUNT HELPERS
    // =====================================================================

    // Picks 1, 2, or 3 with a weighted distribution so the map feels varied:
    // ~30 % get 1 building, ~40 % get 2, ~30 % get 3.
    private int PickBuildingCount()
    {
        float r = Random.value;
        if (r < 0.30f) return 1;
        if (r < 0.70f) return 2;
        return 3;
    }

    // Returns <paramref name="count"/> XZ slot offsets evenly distributed around
    // the tile centre at <paramref name="radius"/>, each nudged by a small random
    // jitter so the pattern is organic rather than perfectly geometric.
    private Vector2[] BuildSlotOffsets(int count, float radius)
    {
        Vector2[] slots  = new Vector2[count];
        float     jitter = radius * 0.18f; // +-18 % keeps slots well separated

        if (count == 1)
        {
            // Single building: place near the centre with a small random nudge
            slots[0] = new Vector2(
                Random.Range(-jitter, jitter),
                Random.Range(-jitter, jitter));
        }
        else
        {
            // Evenly space N buildings around a circle; randomise the start angle
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

    // Initial structure spawn — always uses the Industrial era set.
    private void SpawnStructures(HexTile tile)
    {
        if (tile.type == HexTile.TileType.Water) return;
        if (IsHQZone(tile)) return;
        tile.hasStructure = true;
        EraBuildingSet set    = GetEraBuildingSet(TurnManager.GameEra.Industrial);
        int            count  = PickBuildingCount();
        float          radius = hexSize * buildingPositionSpread;
        Vector2[]      slots  = BuildSlotOffsets(count, radius);

        for (int i = 0; i < count; i++)
            SpawnSingleBuilding(tile, PickRandomBuilding(set), slots[i].x, slots[i].y);
    }

    // Swaps all environmental buildings on every tile to the prefab matching the given era.
    // Also expands building coverage by buildingCoveragePerEra for each era beyond Industrial,
    // so the world feels progressively more built-up as it advances.
    // Called by TurnManager whenever the world era advances.
    public void RefreshEraBuildings(TurnManager.GameEra era)
    {
        EraBuildingSet set = GetEraBuildingSet(era);

        // ── Step 1: Refresh buildings on tiles that already have structures ──
        foreach (var tile in tiles.Values)
        {
            if (tile.type == HexTile.TileType.Water) continue;
            if (IsHQZone(tile)) continue;
            if (!tile.hasStructure) continue;

            for (int i = tile.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = tile.transform.GetChild(i);
                if (child.name.Contains("Env_Structure"))
                    Destroy(child.gameObject);
            }

            int       count  = PickBuildingCount();
            float     radius = hexSize * buildingPositionSpread;
            Vector2[] slots  = BuildSlotOffsets(count, radius);

            for (int i = 0; i < count; i++)
                SpawnSingleBuilding(tile, PickRandomBuilding(set), slots[i].x, slots[i].y);
        }

        // ── Step 2: Populate additional tiles proportional to how far the era has advanced ──
        // Industrial = index 0 (no bonus), EarlyEighties = 1 (+10%), Retro = 2 (+20%), Futuristic = 3 (+30%)
        int eraIndex = (int)era;
        if (eraIndex <= 0) return;

        // Collect all LAND tiles that don't yet have structures
        List<HexTile> emptyLandTiles = new List<HexTile>();
        foreach (var tile in tiles.Values)
            if (tile.type != HexTile.TileType.Water && !tile.hasStructure && !IsHQZone(tile))
                emptyLandTiles.Add(tile);

        if (emptyLandTiles.Count == 0) return;

        // Target total coverage = initial + (eraIndex * perEra), relative to ALL land tiles
        int totalLandTiles = tiles.Count; // only land tiles are in this dict
        float targetCoverage = Mathf.Clamp01(initialBuildingCoverage + eraIndex * buildingCoveragePerEra);
        int targetCount      = Mathf.RoundToInt(totalLandTiles * targetCoverage);

        // How many tiles already have structures?
        int existingCount = totalLandTiles - emptyLandTiles.Count;
        int toAdd         = Mathf.Max(0, targetCount - existingCount);

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

        // ── Step 3: Reshuffle nature props to a fresh random layout ──────────
        ReshuffleNatureDecorations();
    }

    // Instantiates one building at the given XZ slot offset relative to the tile centre.
    // Reads renderer bounds for accurate ground-flush placement, preserves all materials.
    // entry.scale acts as a MULTIPLIER on top of the prefab's own baked scale.
    private void SpawnSingleBuilding(HexTile tile, EraBuilding entry, float rx, float ry)
    {
        if (entry != null && entry.prefab != null)
        {
            // Instantiate WITHOUT a parent so localScale == worldScale.
            // If we parented first, the tile's own world scale would compound
            // into localScale and produce a wildly different size.
            GameObject obj = Instantiate(entry.prefab);
            obj.name = "Env_Structure";
            // ── Apply scale variation (±buildingScaleVariation %) ─────────────
            float scaleMult = Random.Range(1f - buildingScaleVariation,
                                           1f + buildingScaleVariation);
            obj.transform.localScale    = entry.scale * scaleMult;

            // ── Apply random Y-axis rotation ──────────────────────────────────
            float yRot = randomizeRotation ? Random.Range(0f, 360f) : 0f;
            obj.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            obj.transform.position      = tile.transform.position;

            // ── Snapshot materials so nothing upstream can overwrite them ──────
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            Material[][] savedMats = new Material[renderers.Length][];
            for (int r = 0; r < renderers.Length; r++)
                savedMats[r] = renderers[r].sharedMaterials;

            // ── Get tile surface Y from its renderer top ─────────────────────
            // tile.transform.position.y is just the pivot — the actual visible
            // surface is at bounds.max.y of the tile's own renderer.
            float tileSurfaceY = tile.transform.position.y;
            Renderer tileRenderer = tile.GetComponent<Renderer>();
            if (tileRenderer != null) tileSurfaceY = tileRenderer.bounds.max.y;

            // ── Read building bounds for accurate Y placement ─────────────────
            // Only active MeshRenderers have valid bounds — inactive LOD children
            // report bounds at world origin and would corrupt the minY calculation.
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

            // yOffset = tileSurfaceY - minY  (minY measured with obj at tile pivot).
            // Adding yOffset to tile pivot shifts the building bottom exactly
            // to tileSurfaceY — using tileSurfaceY here instead would double-count
            // the tile height and make every building float above the surface.
            obj.transform.position = new Vector3(
                tile.transform.position.x + rx,
                tile.transform.position.y + yOffset,
                tile.transform.position.z + ry);

            // Re-parent AFTER position and scale are final.
            // worldPositionStays=true so the transform doesn't change on re-parent.
            obj.transform.SetParent(tile.transform, true);

            // ── Disable colliders — keep component so bounds stay readable ────
            foreach (var col in cols)
                col.enabled = false;

            // ── Restore materials ─────────────────────────────────────────────
            for (int r = 0; r < renderers.Length; r++)
                renderers[r].sharedMaterials = savedMats[r];

            // ── LOD setup ─────────────────────────────────────────────────────
            SetupLOD(obj);
        }
        else
        {
            // Fallback: plain cube when no prefab is configured
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
        }
    }

    // Adds a three-level LODGroup to objects that don't have one already.
    //   LOD 0 — full mesh, shadows on             (close up, above lod0ScreenSize)
    //   LOD 1 — full mesh, shadows OFF            (mid distance, above lod1ScreenSize)
    //   LOD 2 — full mesh, shadows off, no receive (far, above lodCullScreenSize)
    //   Culled below lodCullScreenSize
    // Turning shadows off at distance is the single biggest GPU saving on a dense map.
    private void SetupLOD(GameObject obj)
    {
        if (obj.GetComponentInChildren<LODGroup>(true) != null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        // LOD 0: close — full shadows
        foreach (var r in renderers)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows    = true;
        }
        LOD lod0 = new LOD(lod0ScreenSize, renderers);

        // LOD 1: mid — shadows off (stops shadow map writes for every distant object)
        foreach (var r in renderers)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows    = false;
        }
        LOD lod1 = new LOD(lod1ScreenSize, renderers);

        // LOD 2: far — same no-shadow mesh, very low screen coverage
        LOD lod2 = new LOD(lodCullScreenSize, renderers);

        // Cull: empty renderer array below lodCullScreenSize
        LOD lodCull = new LOD(0f, new Renderer[0]);

        // Restore shadows to On for LOD 0 behaviour next time this runs on another object
        // (renderers are shared refs — resetting keeps LOD 0 correct for the next SetupLOD call)
        foreach (var r in renderers)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows    = true;
        }

        LODGroup lodGroup = obj.AddComponent<LODGroup>();
        lodGroup.SetLODs(new LOD[] { lod0, lod1, lod2, lodCull });
        lodGroup.RecalculateBounds();
        lodGroup.fadeMode           = LODFadeMode.None; // CrossFade costs extra shader passes
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
    //  PUBLIC ACCESSORS 
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