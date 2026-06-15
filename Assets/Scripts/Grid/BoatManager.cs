using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ── Data classes (mirror EraBuilding / EraBuildingSet in GridManager) ────────

/// <summary>
/// A single boat variant — prefab + per-model scale.
/// Mirrors EraBuilding in GridManager.
/// </summary>
[System.Serializable]
public class BoatEntry
{
    [Tooltip("The boat prefab to spawn. Model should face +Z (world forward).")]
    public GameObject prefab;

    [Tooltip("Scale applied to this specific model. Adjust per-model to match your art.")]
    public Vector3 scale = Vector3.one;
}

/// <summary>
/// All boat variants for one era. A random entry is picked each time a boat is spawned.
/// Mirrors EraBuildingSet in GridManager.
/// </summary>
[System.Serializable]
public class EraBoatSet
{
    [Tooltip("The era this set applies to.")]
    public TurnManager.GameEra era;

    [Tooltip("All boat variants for this era. One is chosen at random per spawn.")]
    public BoatEntry[] boats;
}

// =============================================================================

/// <summary>
/// Extracts the island coastline, builds a smoothed closed spline offset into
/// the sea, and drives a fleet of era-appropriate boats around it.
///
/// SETUP
///   1. Add this component to any GameObject in the scene.
///   2. Populate eraBoatSets — one entry per era, each with its boat variants.
///   3. In GridManager.RebuildMap() add:
///        BoatManager.Instance?.RebuildSpline();
///   4. Wherever TurnManager fires an era change, add:
///        BoatManager.Instance?.RefreshEraBoats(newEra);
/// </summary>
public class BoatManager : MonoBehaviour
{
    public static BoatManager Instance { get; private set; }

    // ── Era Boat Sets ─────────────────────────────────────────────────────────
    [Header("Era Boat Sets")]
    [Tooltip("One entry per era. Each entry holds the boat variants to randomly " +
             "pick from when the fleet spawns for that era. " +
             "Mirrors eraBuildingSets in GridManager.")]
    public EraBoatSet[] eraBoatSets;

    // ── Spawning ──────────────────────────────────────────────────────────────
    [Header("Spawning")]
    [Tooltip("Total number of boats in the fleet at any one time.")]
    [Range(1, 30)]
    public int boatCount = 6;

    // ── Perimeter ─────────────────────────────────────────────────────────────
    [Header("Perimeter")]
    [Tooltip("World units to push the boat path outward from the island edge into open water.")]
    public float seaOffset = 3f;

    [Tooltip("Chaikin smoothing passes on the raw hex perimeter. More = rounder coast path.")]
    [Range(0, 6)]
    public int smoothingPasses = 3;

    // ── Movement ──────────────────────────────────────────────────────────────
    [Header("Movement")]
    public float speedMin = 1.5f;
    public float speedMax = 3.0f;

    [Tooltip("Y world position boats travel at. Should sit just above the water surface.")]
    public float boatY = 0.65f;

    [Tooltip("Vertical bob amplitude in world units.")]
    public float bobAmplitude = 0.07f;

    [Tooltip("Bob cycles per second.")]
    public float bobFrequency = 0.35f;

    [Tooltip("Max roll angle in degrees during the bob cycle.")]
    public float bobRollDegrees = 3f;

    [Tooltip("Degrees per second the boat rotates to face its direction of travel.")]
    public float turnSpeed = 140f;

    // ── Lane & Separation ─────────────────────────────────────────────────────
    [Header("Lane & Separation")]
    [Tooltip("Max perpendicular distance a boat can be offset from the spline centre-line.\n"
           + "Boats are assigned a random offset in [-laneWidth, +laneWidth] so they spread\n"
           + "across parallel tracks instead of all riding the same line.")]
    public float laneWidth = 0.8f;

    [Tooltip("World-space radius within which a boat considers another boat 'too close'.\n"
           + "When a boat ahead is within this distance, the trailing boat slows down.")]
    public float separationDistance = 2.5f;

    [Tooltip("How much to slow down when inside another boat's separation bubble.\n"
           + "0 = full stop, 1 = no slowdown. Blends with current speed smoothly.")]
    [Range(0f, 1f)]
    public float separationSlowdown = 0.15f;

    // ── Internal state ────────────────────────────────────────────────────────
    private List<Vector3> _splinePoints = new List<Vector3>();
    private List<float>   _arcLengths  = new List<float>();
    private float         _totalLength;

    private readonly List<BoatAgent> _agents = new List<BoatAgent>();

    // Tracks which era the fleet was last spawned for.
    private TurnManager.GameEra _currentEra = TurnManager.GameEra.Industrial;

    // Wake rendering — boat positions and forwards pushed to the water shader each frame.
    private const int MaxBoats = 16;
    private readonly Vector4[] _wakePositions = new Vector4[MaxBoats];
    private readonly Vector4[] _wakeForwards  = new Vector4[MaxBoats];
    private Material _waterMaterial;

    private static readonly int ShaderBoatPositions = Shader.PropertyToID("_BoatPositions");
    private static readonly int ShaderBoatForwards  = Shader.PropertyToID("_BoatForwards");
    private static readonly int ShaderBoatCount     = Shader.PropertyToID("_BoatCount");

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(BuildWhenReady());
    }

    private IEnumerator BuildWhenReady()
    {
        while (GridManager.Instance == null || !GridManager.Instance.IsReady)
            yield return null;

        // Grab the water plane's live material so we can push boat positions each frame.
        WaterPlane wp = FindObjectOfType<WaterPlane>();
        if (wp != null)
        {
            MeshRenderer mr = wp.GetComponent<MeshRenderer>();
            if (mr != null) _waterMaterial = mr.material;
        }
        if (_waterMaterial == null)
            Debug.LogWarning("[BoatManager] Could not find WaterPlane material — wake ripples won't render.");

        RebuildSpline();
    }

    private void Update()
    {
        PushWakeData();
    }

    // Collects world position + forward direction from every active BoatAgent
    // and pushes them into the water shader as global arrays each frame.
    private void PushWakeData()
    {
        if (_waterMaterial == null) return;

        int count = 0;
        for (int i = 0; i < _agents.Count && count < MaxBoats; i++)
        {
            BoatAgent a = _agents[i];
            if (a == null) continue;

            Vector3 pos = a.transform.position;
            Vector3 fwd = a.transform.forward;

            _wakePositions[count] = new Vector4(pos.x, pos.y, pos.z, 0f);
            _wakeForwards[count]  = new Vector4(fwd.x, fwd.y, fwd.z, 0f);
            count++;
        }

        _waterMaterial.SetVectorArray(ShaderBoatPositions, _wakePositions);
        _waterMaterial.SetVectorArray(ShaderBoatForwards,  _wakeForwards);
        _waterMaterial.SetInt(ShaderBoatCount, count);
    }

    // =========================================================================
    //  PUBLIC API
    // =========================================================================

    /// <summary>
    /// Rebuilds the coastline spline and respawns the fleet using the current era.
    /// Call from GridManager.RebuildMap() after map regeneration.
    /// </summary>
    public void RebuildSpline()
    {
        DespawnAll();
        _splinePoints.Clear();
        _arcLengths.Clear();
        _totalLength = 0f;

        List<Vector3> perimeter = ExtractOrderedPerimeter();

        if (perimeter.Count < 4)
        {
            Debug.LogWarning("[BoatManager] Too few coastal tiles to build a perimeter — no boats spawned.");
            return;
        }

        List<Vector3> smoothed = perimeter;
        for (int i = 0; i < smoothingPasses; i++)
            smoothed = ChaikinSmooth(smoothed);

        _splinePoints = smoothed;
        BuildArcLengthTable();
        SpawnBoats(_currentEra);

        Debug.Log($"[BoatManager] Spline built — {_splinePoints.Count} pts, " +
                  $"length={_totalLength:F1}m. Fleet: {_agents.Count} boats ({_currentEra}).");
    }

    /// <summary>
    /// Despawns the current fleet and respawns it using the boats assigned to the
    /// given era. Call this wherever TurnManager fires an era change, alongside
    /// GridManager.RefreshEraBuildings().
    /// </summary>
    public void RefreshEraBoats(TurnManager.GameEra era)
    {
        _currentEra = era;
        DespawnAll();

        if (_totalLength < 0.1f)
        {
            Debug.LogWarning("[BoatManager] Spline not ready — call RebuildSpline() first.");
            return;
        }

        SpawnBoats(era);
        Debug.Log($"[BoatManager] Era changed to {era}. Fleet respawned: {_agents.Count} boats.");
    }

    // Spline accessors used by BoatAgent.
    public float TotalLength => _totalLength;

    /// <summary>Interpolated world position at arc-length d (wraps automatically).</summary>
    public Vector3 SampleSpline(float d)
    {
        if (_splinePoints.Count == 0) return Vector3.zero;

        d = Mathf.Repeat(d, _totalLength);

        int lo = 0, hi = _arcLengths.Count - 2;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_arcLengths[mid + 1] < d) lo = mid + 1;
            else                           hi = mid;
        }

        float segLen = _arcLengths[lo + 1] - _arcLengths[lo];
        float t      = segLen > 0.0001f ? (d - _arcLengths[lo]) / segLen : 0f;
        int   n      = _splinePoints.Count;

        return Vector3.Lerp(_splinePoints[lo], _splinePoints[(lo + 1) % n], t);
    }

    /// <summary>Normalised forward tangent at arc-length d.</summary>
    public Vector3 TangentAt(float d)
    {
        const float eps = 0.05f;
        Vector3 ahead  = SampleSpline(d + eps);
        Vector3 behind = SampleSpline(d - eps);
        Vector3 delta  = ahead - behind;
        return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.forward;
    }

    // =========================================================================
    //  ERA BOAT SET HELPERS  (mirror GridManager.GetEraBuildingSet / PickRandomBuilding)
    // =========================================================================

    /// <summary>
    /// Returns the EraBoatSet matching the given era.
    /// Falls back to the first assigned entry if no exact match, then null.
    /// Mirrors GridManager.GetEraBuildingSet().
    /// </summary>
    private EraBoatSet GetEraBoatSet(TurnManager.GameEra era)
    {
        if (eraBoatSets == null) return null;

        foreach (var set in eraBoatSets)
            if (set != null && set.era == era && set.boats != null && set.boats.Length > 0)
                return set;

        // No exact era match — return first assigned entry as fallback
        foreach (var set in eraBoatSets)
            if (set?.boats != null && set.boats.Length > 0)
                return set;

        return null;
    }

    /// <summary>
    /// Returns a random BoatEntry from a set.
    /// Mirrors GridManager.PickRandomBuilding().
    /// </summary>
    private BoatEntry PickRandomBoat(EraBoatSet set)
    {
        if (set == null || set.boats == null || set.boats.Length == 0) return null;
        return set.boats[Random.Range(0, set.boats.Length)];
    }

    // =========================================================================
    //  PERIMETER EXTRACTION
    // =========================================================================

    private List<Vector3> ExtractOrderedPerimeter()
    {
        // ── Step 1: Collect coastal tiles ─────────────────────────────────────
        var coastal = new HashSet<HexTile>();
        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
        {
            if (tile.type == HexTile.TileType.Water) continue;

            List<HexTile> neighbours = GridManager.Instance.GetNeighbors(tile);

            bool isCoastal = neighbours.Count < 6; // map boundary
            if (!isCoastal)
                foreach (HexTile n in neighbours)
                    if (n.type == HexTile.TileType.Water) { isCoastal = true; break; }

            if (isCoastal) coastal.Add(tile);
        }

        if (coastal.Count == 0) return new List<Vector3>();

        // ── Step 2: Coastal-only adjacency graph ──────────────────────────────
        var adj = new Dictionary<HexTile, List<HexTile>>(coastal.Count);
        foreach (HexTile tile in coastal)
        {
            adj[tile] = new List<HexTile>();
            foreach (HexTile n in GridManager.Instance.GetNeighbors(tile))
                if (coastal.Contains(n)) adj[tile].Add(n);
        }

        // ── Step 3: Walk a continuous perimeter loop ──────────────────────────
        // Seed at the rightmost tile for determinism
        HexTile seed = null;
        float   maxX = float.MinValue;
        foreach (HexTile t in coastal)
        {
            float x = t.transform.position.x;
            if (x > maxX) { maxX = x; seed = t; }
        }

        var ordered  = new List<HexTile>();
        var visited  = new HashSet<HexTile>();
        HexTile current  = seed;
        HexTile previous = null;

        while (current != null && !visited.Contains(current))
        {
            ordered.Add(current);
            visited.Add(current);

            Vector2 heading = previous != null
                ? new Vector2(
                    current.transform.position.x - previous.transform.position.x,
                    current.transform.position.z - previous.transform.position.z).normalized
                : Vector2.right;

            HexTile bestNext  = null;
            float   bestScore = float.MinValue;

            foreach (HexTile n in adj[current])
            {
                if (visited.Contains(n) && n != seed) continue;

                Vector2 toN = new Vector2(
                    n.transform.position.x - current.transform.position.x,
                    n.transform.position.z - current.transform.position.z).normalized;

                float dot   = Vector2.Dot(heading, toN);
                float cross = heading.x * toN.y - heading.y * toN.x;
                float score = dot - Mathf.Max(0f, cross) * 0.5f;

                if (score > bestScore) { bestScore = score; bestNext = n; }
            }

            if (bestNext == seed && ordered.Count > 3) break;

            previous = current;
            current  = bestNext;
        }

        if (ordered.Count < 4) return new List<Vector3>();

        // ── Step 4: Island centroid for outward offset direction ──────────────
        Vector3 centroid = Vector3.zero;
        int     count    = 0;
        foreach (HexTile t in GridManager.Instance.GetAllTiles())
        {
            centroid += t.transform.position;
            count++;
        }
        if (count > 0) centroid /= count;
        centroid.y = 0f;

        // ── Step 5: Offset each coastal tile outward into the sea ─────────────
        var pts = new List<Vector3>(ordered.Count);
        foreach (HexTile t in ordered)
        {
            Vector3 pos     = new Vector3(t.transform.position.x, 0f, t.transform.position.z);
            Vector3 outward = pos - centroid;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f) outward = Vector3.right;
            outward.Normalize();

            pts.Add(new Vector3(
                pos.x + outward.x * seaOffset,
                boatY,
                pos.z + outward.z * seaOffset));
        }

        return pts;
    }

    // =========================================================================
    //  CHAIKIN SMOOTHING  (closed loop)
    // =========================================================================

    private static List<Vector3> ChaikinSmooth(List<Vector3> pts)
    {
        int n      = pts.Count;
        var result = new List<Vector3>(n * 2);
        for (int i = 0; i < n; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[(i + 1) % n];
            result.Add(Vector3.Lerp(a, b, 0.25f));
            result.Add(Vector3.Lerp(a, b, 0.75f));
        }
        return result;
    }

    // =========================================================================
    //  ARC-LENGTH TABLE
    // =========================================================================

    private void BuildArcLengthTable()
    {
        _arcLengths.Clear();
        _totalLength = 0f;
        _arcLengths.Add(0f);

        int n = _splinePoints.Count;
        for (int i = 0; i < n; i++)
        {
            float seg = Vector3.Distance(_splinePoints[i], _splinePoints[(i + 1) % n]);
            _totalLength += seg;
            _arcLengths.Add(_totalLength);
        }
    }

    // =========================================================================
    //  BOAT SPAWNING / DESPAWNING
    // =========================================================================

    private void SpawnBoats(TurnManager.GameEra era)
    {
        EraBoatSet set = GetEraBoatSet(era);

        if (set == null)
        {
            Debug.LogWarning($"[BoatManager] No EraBoatSet assigned for era {era} — no boats spawned.");
            return;
        }

        if (_totalLength < 0.1f) return;

        float spacing = _totalLength / boatCount;

        for (int i = 0; i < boatCount; i++)
        {
            BoatEntry entry = PickRandomBoat(set);

            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[BoatManager] Boat {i} ({era}): no valid prefab — skipping.");
                continue;
            }

            float startD = spacing * i + Random.Range(-spacing * 0.25f, spacing * 0.25f);
            startD = Mathf.Repeat(startD, _totalLength);

            // Give each boat a random perpendicular lane offset so they spread
            // across parallel tracks and don't all ride the same centre line.
            float laneOffset = Random.Range(-laneWidth, laneWidth);

            Vector3    spawnPos = SampleSpline(startD);
            GameObject obj      = Instantiate(entry.prefab, spawnPos, Quaternion.identity, transform);
            obj.name                 = $"Boat_{i} ({era})";
            obj.transform.localScale = entry.scale;

            BoatAgent agent = obj.AddComponent<BoatAgent>();
            agent.Init(
                manager:            this,
                startD:             startD,
                speed:              Random.Range(speedMin, speedMax),
                direction:          Random.value < 0.5f ? 1f : -1f,
                bobAmplitude:       bobAmplitude,
                bobFrequency:       bobFrequency + Random.Range(-0.04f, 0.04f),
                bobPhase:           Random.Range(0f, Mathf.PI * 2f),
                bobRollDegrees:     bobRollDegrees,
                turnSpeed:          turnSpeed,
                boatY:              boatY,
                laneOffset:         laneOffset,
                separationDistance: separationDistance,
                separationSlowdown: separationSlowdown,
                allAgents:          _agents
            );

            _agents.Add(agent);
        }
    }

    private void DespawnAll()
    {
        foreach (BoatAgent a in _agents)
            if (a != null && a.gameObject != null) Destroy(a.gameObject);
        _agents.Clear();
    }

    // =========================================================================
    //  EDITOR HELPERS
    // =========================================================================

    [ContextMenu("Rebuild Spline Now")]
    private void RebuildSplineNow()
    {
        if (GridManager.Instance != null && GridManager.Instance.IsReady)
            RebuildSpline();
        else
            Debug.LogWarning("[BoatManager] Grid not ready — cannot rebuild spline.");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_splinePoints == null || _splinePoints.Count < 2) return;

        Gizmos.color = Color.cyan;
        int n = _splinePoints.Count;
        for (int i = 0; i < n; i++)
            Gizmos.DrawLine(_splinePoints[i], _splinePoints[(i + 1) % n]);

        Gizmos.color = Color.yellow;
        foreach (var p in _splinePoints)
            Gizmos.DrawSphere(p, 0.1f);
    }
#endif
}


// =============================================================================
//  BOAT AGENT
//  Added at runtime via AddComponent — never add this manually in the inspector.
// =============================================================================

/// <summary>
/// Moves a single boat along the BoatManager spline with hull-bob and gentle roll.
/// Initialised by BoatManager.SpawnBoats() — never add this component manually.
/// </summary>
public class BoatAgent : MonoBehaviour
{
    private BoatManager      _mgr;
    private List<BoatAgent>  _allAgents;

    private float _d;            // arc-length position along spline
    private float _speed;        // base speed assigned at spawn
    private float _currentSpeed; // runtime speed after avoidance dampening
    private float _dir;          // +1 or -1 along the spline

    private float _bobAmp;
    private float _bobFreq;
    private float _bobPhase;
    private float _bobRoll;
    private float _turnSpeed;
    private float _boatY;

    // _baseLaneOffset  : fixed perpendicular offset assigned at spawn — never changes.
    // _dynamicLaneOffset: live offset that drifts to steer away from nearby boats,
    //                     then smoothly returns to _baseLaneOffset when the coast is clear.
    private float _baseLaneOffset;
    private float _dynamicLaneOffset;

    // Max extra lateral drift the avoidance system is allowed to add on top of
    // the base lane offset (clamped to ±_maxLaneDrift from the base).
    private const float MaxLaneDrift    = 1.2f;
    // How fast (units/sec) the dynamic offset returns to the base when no boat is nearby.
    private const float LaneReturnSpeed = 0.8f;
    // How strongly nearby boats push the lane offset sideways (units/sec per unit of proximity).
    private const float LateralPushStrength = 2.5f;

    private float _separationDist;
    private float _separationSlowdown;

    public void Init(BoatManager      manager,
                     float            startD,
                     float            speed,
                     float            direction,
                     float            bobAmplitude,
                     float            bobFrequency,
                     float            bobPhase,
                     float            bobRollDegrees,
                     float            turnSpeed,
                     float            boatY,
                     float            laneOffset,
                     float            separationDistance,
                     float            separationSlowdown,
                     List<BoatAgent>  allAgents)
    {
        _mgr                = manager;
        _d                  = startD;
        _speed              = speed;
        _currentSpeed       = speed;
        _dir                = direction;
        _bobAmp             = bobAmplitude;
        _bobFreq            = bobFrequency;
        _bobPhase           = bobPhase;
        _bobRoll            = bobRollDegrees;
        _turnSpeed          = turnSpeed;
        _boatY              = boatY;
        _baseLaneOffset     = laneOffset;
        _dynamicLaneOffset  = laneOffset;
        _separationDist     = separationDistance;
        _separationSlowdown = separationSlowdown;
        _allAgents          = allAgents;
    }

    private void Update()
    {
        if (_mgr == null || _mgr.TotalLength < 0.1f) return;

        // Cache frequently used values
        Vector3 myPos   = transform.position;
        Vector3 tangent = _mgr.TangentAt(_d) * _dir;
        // Perpendicular in XZ plane — positive points "outward" relative to travel direction
        Vector3 perp    = new Vector3(-tangent.z, 0f, tangent.x);

        // ── 1. Avoidance pass ─────────────────────────────────────────────────
        // For every nearby boat we accumulate two responses:
        //   • Forward component  → slow down  (only for boats ahead of us)
        //   • Lateral component  → steer away (for all nearby boats regardless of direction)
        //
        // This means boats behind us don't make us slow down, but they DO push us
        // sideways if we're drifting into them — preventing side-on clipping.

        float targetSpeed    = _speed;
        float lateralPush    = 0f;   // accumulated signed lateral push this frame
        bool  anyBoatNearby  = false;

        if (_allAgents != null)
        {
            float sepSqr = _separationDist * _separationDist;

            foreach (BoatAgent other in _allAgents)
            {
                if (other == null || other == this) continue;

                Vector3 toOther  = other.transform.position - myPos;
                toOther.y        = 0f;                           // work in XZ only
                float distSqr    = toOther.sqrMagnitude;

                if (distSqr >= sepSqr || distSqr < 0.0001f) continue;

                float dist       = Mathf.Sqrt(distSqr);
                // 0 = at bubble edge, 1 = fully overlapping
                float proximity  = 1f - dist / _separationDist;
                anyBoatNearby    = true;

                // ── Forward component: only slow for boats ahead of us ────────
                float forwardDot = Vector3.Dot(toOther.normalized, tangent);
                if (forwardDot > 0.1f)
                {
                    // The more directly ahead and the closer, the more we slow
                    float slowAmount = proximity * forwardDot;
                    float candidate  = Mathf.Lerp(_speed, _speed * _separationSlowdown, slowAmount);
                    if (candidate < targetSpeed) targetSpeed = candidate;
                }

                // ── Lateral component: steer away from ALL nearby boats ───────
                // Project the vector-to-other onto our perpendicular axis.
                // Positive lateralDot means the other boat is to our right (outward).
                // We push ourselves in the opposite direction (inward / leftward).
                float lateralDot = Vector3.Dot(toOther.normalized, perp);
                // Push magnitude scales with proximity; direction is away from the other boat
                lateralPush -= lateralDot * proximity * LateralPushStrength;
            }
        }

        // ── 2. Apply lateral push to dynamic lane offset ──────────────────────
        if (anyBoatNearby)
        {
            // Nudge the dynamic offset this frame based on accumulated push
            _dynamicLaneOffset += lateralPush * Time.deltaTime;

            // Clamp so boats don't drift infinitely far from their assigned lane
            float minOffset = _baseLaneOffset - MaxLaneDrift;
            float maxOffset = _baseLaneOffset + MaxLaneDrift;
            _dynamicLaneOffset = Mathf.Clamp(_dynamicLaneOffset, minOffset, maxOffset);
        }
        else
        {
            // No boats nearby — drift back to the assigned base lane
            _dynamicLaneOffset = Mathf.MoveTowards(
                _dynamicLaneOffset, _baseLaneOffset, LaneReturnSpeed * Time.deltaTime);
        }

        // ── 3. Smooth speed transition ────────────────────────────────────────
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * 5f);

        // ── 4. Advance along spline ───────────────────────────────────────────
        _d = Mathf.Repeat(_d + _currentSpeed * _dir * Time.deltaTime, _mgr.TotalLength);

        // Recompute tangent and perp at the new position
        tangent = _mgr.TangentAt(_d) * _dir;
        perp    = new Vector3(-tangent.z, 0f, tangent.x);

        // ── 5. Build world position from spline + dynamic lane offset ─────────
        Vector3 splinePos = _mgr.SampleSpline(_d);
        Vector3 pos       = splinePos + perp * _dynamicLaneOffset;

        float bobT = Time.time * _bobFreq * Mathf.PI * 2f + _bobPhase;
        pos.y = _boatY + Mathf.Sin(bobT) * _bobAmp;

        transform.position = pos;

        // ── 6. Rotation: face direction of travel + gentle hull roll ──────────
        if (tangent.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(tangent, Vector3.up);
            float roll           = Mathf.Sin(bobT + Mathf.PI * 0.5f) * _bobRoll;
            targetRot           *= Quaternion.Euler(0f, 0f, roll);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, _turnSpeed * Time.deltaTime);
        }
    }
}