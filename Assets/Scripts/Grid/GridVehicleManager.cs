using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  One prefab entry in the vehicle palette.
// ─────────────────────────────────────────────────────────────────────────────
[System.Serializable]
public class VehicleEntry
{
    [Tooltip("The vehicle prefab to spawn. Pivot should be at the model's base centre.")]
    public GameObject prefab;

    [Tooltip("How many world-units per second this vehicle type moves.\n" +
             "Set to 0 to fall back to GridVehicleManager.defaultSpeed.")]
    [Range(0f, 20f)]
    public float speedOverride = 0f;
}

// ─────────────────────────────────────────────────────────────────────────────
//  Vehicle states
//
//  Moving  → lerping from one hex centre to the next along the fixed path.
//  Paused  → briefly idling at an intersection.
//  Waiting → next tile is reserved by another agent; polling every frame.
//  Fading  → reached destination; alpha fading to 0 before returning to pool.
//  Inactive→ in pool, deactivated, ready for reuse.
// ─────────────────────────────────────────────────────────────────────────────
internal enum VehicleState { Moving, Paused, Waiting, Fading, Inactive }

// ─────────────────────────────────────────────────────────────────────────────
//  Per-vehicle runtime state.
// ─────────────────────────────────────────────────────────────────────────────
internal class VehicleAgent
{
    public GameObject    obj;
    public HexTile       currentTile;
    public HexTile       destinationTile;
    public List<HexTile> path = new List<HexTile>();
    public int           pathIndex;
    public float         speed;
    public float         lerpT;
    public float         pauseTimer;
    public VehicleState  state = VehicleState.Inactive;

    // Cached once at the START of each tile-to-tile segment (not every frame).
    // Storing them here prevents any mid-lerp drift if tile positions were ever
    // to change (e.g. animated tiles), and removes the per-frame TileWorldPos call.
    public Vector3    fromPos;
    public Vector3    toPos;
    public Quaternion targetRotation;

    // The tile this agent has currently reserved (the one it is heading toward).
    // Null when the agent is not in transit or has just arrived.
    public HexTile reservedTile;

    // The tile this agent is physically sitting on (the origin of the current segment).
    // Kept in _reservedTiles so no other vehicle can enter it while we haven't fully left.
    // Released on arrival, at which point reservedTile is promoted to occupiedTile.
    public HexTile occupiedTile;

    // Two-lane slot: 0 = left lane, 1 = right lane.
    // Assigned at spawn and kept for the whole trip so the vehicle stays
    // consistently in one lane rather than drifting between hops.
    public int slot;

    // Slot claimed on the NEXT tile (mirrors the reservedTile concept).
    // -1 means no slot is currently reserved on the next tile.
    public int reservedSlot;

    // Fade-out
    public float      fadeTimer;
    public Renderer[] renderers;   // cached once at pool build time

    // Deadlock guard: tracks how long this agent has been continuously Waiting.
    // Reset whenever the agent starts moving. If it exceeds the manager's
    // waitTimeoutSeconds, the agent re-routes or fades out to break the jam.
    public float waitTimer;
}

// ─────────────────────────────────────────────────────────────────────────────
//  GridVehicleManager
//
//  Drop on any GameObject in the scene. Assign vehicle prefabs, hit Play.
//
//  Requires:  GridManager (same scene)
//             HexTile     (must have TileType.Road and isExplored)
// ─────────────────────────────────────────────────────────────────────────────
public class GridVehicleManager : MonoBehaviour
{
    // ── Vehicle palette ───────────────────────────────────────────────────
    [Header("Vehicle Prefabs")]
    [Tooltip("One or more vehicle types. One is chosen at random per pool slot.")]
    public VehicleEntry[] vehicleEntries;

    // ── Pool & Active Count ───────────────────────────────────────────────
    [Header("Pool & Count")]
    [Tooltip("Total vehicles pre-allocated at startup. Should be >= vehicleCount.\n" +
             "No Instantiate/Destroy calls happen at runtime.")]
    [Range(1, 150)]
    public int maxPoolSize = 60;

    [Tooltip("How many vehicles should be active and driving at any one time.")]
    [Range(1, 100)]
    public int vehicleCount = 25;

    [Tooltip("How often (seconds) the spawner coroutine checks and tops up active vehicles.")]
    [Range(0.05f, 5f)]
    public float spawnIntervalSeconds = 0.3f;

    [Tooltip("When enabled, the spawner loop starts automatically once the grid is ready.")]
    public bool spawnOnStart = true;

    // ── Movement ──────────────────────────────────────────────────────────
    [Header("Movement")]
    [Tooltip("Default travel speed in world-units/second. Per-prefab overrides in VehicleEntry.")]
    [Range(0.5f, 20f)]
    public float defaultSpeed = 3f;

    [Tooltip("Controls how quickly vehicles settle onto the heading of the next hex.\n" +
             "Acts as an exponential decay rate — higher values snap faster but still ease smoothly.\n" +
             "Typical sweet spot: 4–8 for organic feel, 12+ for snappy turns.")]
    [Range(1f, 20f)]
    public float rotationSpeed = 6f;

    [Tooltip("Height above the tile surface. Tune so wheels sit flush.")]
    public float vehicleHeightOffset = 0.05f;

    [Tooltip("How far left/right (world units) each lane is offset from the tile centre.\n" +
             "Slot 0 shifts left, slot 1 shifts right, relative to the direction of travel.\n" +
             "Tune until wheels sit inside the road markings on your tile art.")]
    [Range(0f, 2f)]
    public float laneOffset = 0.3f;

    // ── Route ─────────────────────────────────────────────────────────────
    [Header("Route Behaviour")]
    [Tooltip("Minimum path length (tiles). Trips shorter than this are rejected and retried.")]
    [Range(2, 20)]
    public int minRouteLength = 4;

    [Tooltip("Maximum path length (tiles). Caps BFS search depth.")]
    [Range(5, 200)]
    public int maxRouteLength = 80;

    [Tooltip("Vehicles briefly stop at road tiles with 3+ neighbours (junctions).")]
    public bool pauseAtIntersections = true;

    [Range(0f, 5f)]
    public float intersectionPauseDuration = 0.4f;

    [Range(0f, 2f)]
    public float pauseJitter = 0.2f;

    [Tooltip("How long (seconds) a vehicle may stay blocked before it tries to re-route.\n" +
             "Prevents permanent intersection deadlocks. Set higher for denser cities.")]
    [Range(0.5f, 10f)]
    public float waitTimeoutSeconds = 2f;

    // ── Lifespan / Fade ───────────────────────────────────────────────────
    [Header("Lifespan & Fade")]
    [Tooltip("Seconds to fade the vehicle out after reaching its destination.\n" +
             "NOTE: your vehicle materials must have an alpha channel / transparency\n" +
             "enabled (URP Surface Type = Transparent) for the fade to show.\n" +
             "If they don't, the vehicle will simply snap off — which is also fine.")]
    [Range(0.1f, 3f)]
    public float fadeDuration = 0.6f;

    // ─────────────────────────────────────────────────────────────────────
    //  Internal state
    // ─────────────────────────────────────────────────────────────────────
    private List<VehicleAgent>               _pool          = new List<VehicleAgent>();
    private List<VehicleAgent>               _active        = new List<VehicleAgent>();
    private List<HexTile>                    _roadTiles     = new List<HexTile>();
    // Maps each reserved tile to the number of slots currently occupied (1 or 2).
    // A tile is considered "full" (impassable) only when its count reaches 2.
    private Dictionary<HexTile, int> _reservedTiles = new Dictionary<HexTile, int>();

    // Tracks which slot index (0 or 1) is taken when only ONE vehicle is on a tile.
    // Used by PickSlot to hand the opposite lane to a second vehicle.
    // Entry is removed once a second vehicle claims the tile (both slots full).
    private Dictionary<HexTile, int> _slotOccupancy = new Dictionary<HexTile, int>();
    private Dictionary<HexTile, List<HexTile>> _roadNeighbors =
        new Dictionary<HexTile, List<HexTile>>();

    // =====================================================================
    //  LIFECYCLE
    // =====================================================================

    private void Start()
    {
        if (spawnOnStart)
            StartCoroutine(WaitForGridThenSpawn());
    }

    private IEnumerator WaitForGridThenSpawn()
    {
        while (GridManager.Instance == null || !GridManager.Instance.IsReady)
            yield return null;

        yield return new WaitForEndOfFrame();   // let nature props finish too

        CollectRoadData();
        BuildPool();
        StartCoroutine(SpawnerLoop());
    }

    private void Update()
    {
        // Tick all active agents. Iterate in reverse so we can safely remove
        // agents that have returned to the pool this frame.
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            VehicleAgent agent = _active[i];
            TickAgent(agent);

            if (agent.state == VehicleState.Inactive)
            {
                _active.RemoveAt(i);
                _pool.Add(agent);
            }
        }
    }

    // =====================================================================
    //  POOL CONSTRUCTION (runs once after grid is ready)
    // =====================================================================

    private void BuildPool()
    {
        if (vehicleEntries == null || vehicleEntries.Length == 0)
        {
            Debug.LogWarning("[GridVehicleManager] No vehicle entries assigned in the Inspector.");
            return;
        }

        for (int i = 0; i < maxPoolSize; i++)
        {
            VehicleEntry entry = vehicleEntries[Random.Range(0, vehicleEntries.Length)];
            if (entry?.prefab == null) continue;

            GameObject obj = Instantiate(entry.prefab, transform);
            obj.name = $"Vehicle_{i}";
            obj.SetActive(false);

            // Vehicles are purely visual — no physics collisions.
            foreach (Collider col in obj.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            float spd = entry.speedOverride > 0f ? entry.speedOverride : defaultSpeed;

            VehicleAgent agent = new VehicleAgent
            {
                obj       = obj,
                speed     = spd,
                renderers = obj.GetComponentsInChildren<Renderer>(true),
                state     = VehicleState.Inactive,
            };

            _pool.Add(agent);
        }

        Debug.Log($"[GridVehicleManager] Pool built: {_pool.Count} vehicles ready.");
    }

    // =====================================================================
    //  SPAWNER LOOP
    // =====================================================================

    private IEnumerator SpawnerLoop()
    {
        while (true)
        {
            int deficit = vehicleCount - _active.Count;
            for (int i = 0; i < deficit; i++)
                TrySpawnOne();

            yield return new WaitForSeconds(spawnIntervalSeconds);
        }
    }

    // Pulls one agent from the pool, picks a random origin→destination path,
    // and kicks it off. Silently skips if the pool is empty or no valid path
    // is found within a small number of attempts.
    private void TrySpawnOne()
    {
        if (_pool.Count == 0 || _roadTiles.Count < 2) return;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            HexTile origin = _roadTiles[Random.Range(0, _roadTiles.Count)];
            HexTile dest   = _roadTiles[Random.Range(0, _roadTiles.Count)];
            if (origin == dest) continue;

            List<HexTile> path = FindRoadPath(origin, dest, maxRouteLength);
            if (path == null || path.Count < minRouteLength) continue;

            // ── Grab from pool ─────────────────────────────────────────────
            VehicleAgent agent = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);

            // Reset alpha in case last trip ended mid-fade.
            SetAgentAlpha(agent, 1f);

            agent.currentTile     = origin;
            agent.destinationTile = dest;
            agent.path            = path;
            agent.pathIndex       = 1;        // index 0 is the tile we start on
            agent.lerpT           = 0f;
            agent.pauseTimer      = 0f;
            agent.fadeTimer       = 0f;
            agent.reservedTile    = null;
            agent.occupiedTile    = null;
            agent.targetRotation  = Quaternion.identity;
            agent.waitTimer       = 0f;

            agent.obj.transform.position = TileWorldPos(origin);
            agent.obj.transform.rotation = Quaternion.identity;
            agent.obj.SetActive(true);

            // Occupy the spawn tile so another vehicle can't drive into it.
            // If it's already full (both slots taken), put the agent back and try again.
            if (IsTileFull(origin))
            {
                agent.obj.SetActive(false);
                agent.state = VehicleState.Inactive;
                _pool.Add(agent);
                continue;
            }
            agent.slot         = PickSlot(origin);
            agent.reservedSlot = -1;
            OccupyTile(agent, origin);

            // Try to claim the first tile we're heading toward.
            HexTile firstNext = path[1];
            if (IsTileFull(firstNext))
            {
                agent.state = VehicleState.Waiting;
            }
            else
            {
                ClaimNextTile(agent, firstNext);
                BeginSegment(agent);   // sets state = Moving + caches fromPos/toPos
            }

            _active.Add(agent);
            return;
        }
    }

    // =====================================================================
    //  AGENT TICK  (called every frame per active agent)
    // =====================================================================

    private void TickAgent(VehicleAgent agent)
    {
        if (agent.obj == null) return;

        // Match tile explored state — hide vehicles on unexplored tiles.
        // (Skip check when Inactive; that state is handled by the Update loop.)
        if (agent.state != VehicleState.Inactive && agent.state != VehicleState.Fading)
        {
            bool visible = agent.currentTile != null && agent.currentTile.isExplored;
            if (agent.obj.activeSelf != visible)
                agent.obj.SetActive(visible);
        }

        switch (agent.state)
        {
            case VehicleState.Moving:  TickMoving(agent);  break;
            case VehicleState.Paused:  TickPause(agent);   break;
            case VehicleState.Waiting: TickWaiting(agent); break;
            case VehicleState.Fading:  TickFading(agent);  break;
        }
    }

    // ── Intersection idle ─────────────────────────────────────────────────
    private void TickPause(VehicleAgent agent)
    {
        agent.pauseTimer -= Time.deltaTime;
        if (agent.pauseTimer > 0f) return;

        // Pause finished — try to move on.
        if (agent.pathIndex >= agent.path.Count) { BeginFade(agent); return; }

        HexTile nextTile = agent.path[agent.pathIndex];
        if (IsTileFull(nextTile))
        {
            agent.state = VehicleState.Waiting;
        }
        else
        {
            ClaimNextTile(agent, nextTile);
            BeginSegment(agent);
        }
    }

    // ── Waiting for a blocked tile to free up ─────────────────────────────
    private void TickWaiting(VehicleAgent agent)
    {
        if (agent.pathIndex >= agent.path.Count) { BeginFade(agent); return; }

        HexTile nextTile = agent.path[agent.pathIndex];

        // Poll: is the tile free yet?
        if (!IsTileFull(nextTile))
        {
            agent.waitTimer = 0f;
            ClaimNextTile(agent, nextTile);
            BeginSegment(agent);   // sets state = Moving
            return;
        }

        // Deadlock guard: if we have been stuck too long, try an alternate route.
        // This breaks circular reservation chains at busy intersections.
        agent.waitTimer += Time.deltaTime;
        if (agent.waitTimer < waitTimeoutSeconds) return;

        agent.waitTimer = 0f;

        // Attempt to reroute from the current tile to a new random destination.
        if (_roadTiles.Count >= 2)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                HexTile newDest = _roadTiles[Random.Range(0, _roadTiles.Count)];
                if (newDest == agent.currentTile) continue;

                List<HexTile> newPath = FindRoadPath(agent.currentTile, newDest, maxRouteLength);
                if (newPath == null || newPath.Count < 2) continue;

                // Only adopt the path if the very next tile on it is actually free.
                HexTile firstStep = newPath[1];
                if (IsTileFull(firstStep)) continue;

                agent.path            = newPath;
                agent.pathIndex       = 1;
                agent.destinationTile = newDest;
                agent.waitTimer       = 0f;

                ClaimNextTile(agent, firstStep);
                BeginSegment(agent);
                return;
            }
        }

        // No alternate route found — fade out so the spot is freed and the
        // vehicle can respawn fresh on the next spawner interval.
        BeginFade(agent);
    }

    // ── Lerp along the path ───────────────────────────────────────────────
    private void TickMoving(VehicleAgent agent)
    {
        if (agent.pathIndex >= agent.path.Count) { BeginFade(agent); return; }

        // fromPos / toPos are stable — cached at BeginSegment, not re-derived here.
        float dist = Vector3.Distance(agent.fromPos, agent.toPos);
        if (dist < 0.001f) dist = 0.001f;

        agent.lerpT += (agent.speed / dist) * Time.deltaTime;
        agent.lerpT  = Mathf.Clamp01(agent.lerpT);

        // Linear lerp across each tile hop. The rotation Slerp below already
        // gives each segment an organic feel; adding SmoothStep on top causes
        // visible decelerate/accelerate micro-stutters at every hex boundary.
        agent.obj.transform.position = Vector3.Lerp(agent.fromPos, agent.toPos, agent.lerpT);

        // Exponential Slerp: fast initial snap that organically settles on the target heading.
        // rotationSpeed now controls the decay rate rather than raw degrees/sec.
        agent.obj.transform.rotation = Quaternion.Slerp(
            agent.obj.transform.rotation,
            agent.targetRotation,
            1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));

        // ── Arrived at the next tile ───────────────────────────────────────
        if (agent.lerpT < 1f) return;

        HexTile arrivedTile = agent.path[agent.pathIndex];

        // Update agent position record.
        agent.currentTile = arrivedTile;
        agent.pathIndex++;
        agent.lerpT = 0f;

        // Dual-tile handoff: release the tile we departed (occupiedTile),
        // and promote the tile we just arrived at (reservedTile → occupiedTile).
        // This keeps the arrived tile locked until the next segment begins,
        // preventing another vehicle from entering it before we depart.
        if (agent.occupiedTile != null)
        {
            DecrementTile(agent.occupiedTile, agent.slot);
            agent.occupiedTile = null;
        }
        if (agent.reservedTile == arrivedTile)
        {
            // Promote: the slot we reserved on arrivedTile becomes our occupied slot.
            // The count in _reservedTiles stays the same — we just change ownership.
            agent.slot         = agent.reservedSlot;
            agent.occupiedTile = arrivedTile;
            agent.reservedTile = null;
            agent.reservedSlot = -1;
        }

        // ── Reached final destination? ─────────────────────────────────────
        if (agent.pathIndex >= agent.path.Count)
        {
            BeginFade(agent);
            return;
        }

        // ── Intersection pause ─────────────────────────────────────────────
        if (pauseAtIntersections && IsIntersection(agent.currentTile))
        {
            float jitter      = Random.Range(-pauseJitter, pauseJitter);
            agent.pauseTimer  = Mathf.Max(0f, intersectionPauseDuration + jitter);
            agent.state       = VehicleState.Paused;
            return;
        }

        // ── Claim next tile and begin the next segment ─────────────────────
        HexTile nextTile = agent.path[agent.pathIndex];
        if (IsTileFull(nextTile))
        {
            agent.state = VehicleState.Waiting;
            return;
        }

        ClaimNextTile(agent, nextTile);
        BeginSegment(agent);
    }

    // ── Fade-out toward destination ───────────────────────────────────────
    private void TickFading(VehicleAgent agent)
    {
        agent.fadeTimer += Time.deltaTime;
        float alpha = Mathf.Clamp01(1f - agent.fadeTimer / fadeDuration);
        SetAgentAlpha(agent, alpha);

        if (agent.fadeTimer >= fadeDuration)
            ReturnToPool(agent);
    }

    // =====================================================================
    //  SEGMENT & STATE HELPERS
    // =====================================================================

    // Call at the start of each new tile hop.
    // Caches the two world-space endpoints so TickMoving never re-derives them.
    // Also pre-computes the target heading so TickMoving only needs to Slerp toward it.
    // The occupiedTile is updated here: the tile we were sitting on becomes the new
    // occupiedTile, and any previously held occupiedTile is released.
    private void BeginSegment(VehicleAgent agent)
    {
        Vector3 rawTo   = TileWorldPos(agent.path[agent.pathIndex]);
        agent.lerpT     = 0f;
        agent.waitTimer = 0f;
        agent.state     = VehicleState.Moving;

        // Direction is derived from tile centres (not offset positions) so it is
        // always accurate regardless of which lane we happen to be in.
        Vector3 rawFrom = TileWorldPos(agent.currentTile);
        Vector3 dir     = rawTo - rawFrom;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            agent.targetRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // fromPos = the car's ACTUAL world position right now.
        // Using the live transform instead of re-deriving from the tile centre
        // ensures there is zero positional jump when the road turns and the
        // perpendicular offset vector changes direction between segments.
        agent.fromPos = agent.obj.transform.position;

        // toPos = offset tile centre of the destination.
        // sign: slot 0 → left (-1), slot 1 → right (+1) relative to travel dir.
        if (laneOffset > 0f && dir.sqrMagnitude > 0.0001f)
        {
            float sign     = agent.slot == 0 ? -1f : 1f;
            Vector3 right  = Vector3.Cross(Vector3.up, dir.normalized);
            agent.toPos    = rawTo + right * (sign * laneOffset);
        }
        else
        {
            agent.toPos = rawTo;
        }
    }

    private void BeginFade(VehicleAgent agent)
    {
        // Release both tile reservations before fading.
        if (agent.occupiedTile != null)
        {
            DecrementTile(agent.occupiedTile, agent.slot);
            agent.occupiedTile = null;
        }
        if (agent.reservedTile != null)
        {
            DecrementTile(agent.reservedTile, agent.reservedSlot);
            agent.reservedTile = null;
        }
        agent.fadeTimer = 0f;
        agent.state     = VehicleState.Fading;
    }

    // Deactivates the vehicle and marks it ready for reuse.
    // Actual removal from _active + insertion into _pool happens in Update.
    private void ReturnToPool(VehicleAgent agent)
    {
        if (agent.occupiedTile != null)
        {
            DecrementTile(agent.occupiedTile, agent.slot);
            agent.occupiedTile = null;
        }
        if (agent.reservedTile != null)
        {
            DecrementTile(agent.reservedTile, agent.reservedSlot);
            agent.reservedTile = null;
        }
        agent.obj.SetActive(false);
        agent.state = VehicleState.Inactive;
    }

    // ── Tile reservation ──────────────────────────────────────────────────
    // Each tile supports up to 2 simultaneous occupants (one per lane slot).
    // IsTileFull returns true only when both slots are taken.
    private bool IsTileFull(HexTile tile)
    {
        return _reservedTiles.TryGetValue(tile, out int n) && n >= 2;
    }

    // PickSlot: returns the first free slot index on a tile (0 or 1).
    // Call only after confirming the tile is NOT full.
    private int PickSlot(HexTile tile)
    {
        if (!_reservedTiles.TryGetValue(tile, out int n) || n == 0) return 0;
        // One slot is taken — figure out which and return the other.
        // We track which slots are taken per tile so we can hand the opposite one.
        return _slotOccupancy.TryGetValue(tile, out int takenSlot) ? 1 - takenSlot : 1;
    }

    // Each agent holds UP TO TWO reservations at a time:
    //   occupiedTile  — the tile it is physically on (released on departure).
    //   reservedTile  — the tile it is heading toward (released on arrival,
    //                   then promoted to occupiedTile for the next segment).
    //
    // OccupyTile: mark the current tile as occupied (called once at spawn and
    //             implicitly maintained via the arrival hand-off in TickMoving).
    private void OccupyTile(VehicleAgent agent, HexTile tile)
    {
        agent.occupiedTile = tile;
        IncrementTile(tile, agent.slot);
    }

    // ClaimNextTile: reserve the tile we are about to move into.
    // The previous reservedTile (if any) is released — it means we never
    // managed to depart toward it and are now targeting a different tile.
    private void ClaimNextTile(VehicleAgent agent, HexTile tile)
    {
        if (agent.reservedTile != null)
            DecrementTile(agent.reservedTile, agent.reservedSlot);

        int slot = PickSlot(tile);
        agent.reservedTile = tile;
        agent.reservedSlot = slot;
        IncrementTile(tile, slot);
    }

    // ── Low-level slot accounting ─────────────────────────────────────────
    private void IncrementTile(HexTile tile, int slot)
    {
        _reservedTiles.TryGetValue(tile, out int n);
        _reservedTiles[tile] = n + 1;
        if (n == 0)
            _slotOccupancy[tile] = slot;   // first occupant — record which lane
        else
            _slotOccupancy.Remove(tile);   // both lanes now taken — no longer meaningful
    }

    private void DecrementTile(HexTile tile, int slot)
    {
        if (!_reservedTiles.TryGetValue(tile, out int n)) return;
        if (n <= 1)
        {
            _reservedTiles.Remove(tile);
            _slotOccupancy.Remove(tile);
        }
        else
        {
            _reservedTiles[tile] = n - 1;
            _slotOccupancy[tile] = slot == 0 ? 1 : 0;  // the OTHER occupant is still there
        }
    }

    // =====================================================================
    //  ROAD-ONLY BFS PATHFINDER
    // =====================================================================

    private List<HexTile> FindRoadPath(HexTile start, HexTile end, int maxTiles)
    {
        if (start == end) return null;

        var cameFrom = new Dictionary<HexTile, HexTile>();
        var frontier  = new Queue<HexTile>();

        cameFrom[start] = null;
        frontier.Enqueue(start);

        int  visited = 0;
        bool found   = false;

        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();
            visited++;

            if (current == end) { found = true; break; }
            if (visited > maxTiles) break;

            if (!_roadNeighbors.TryGetValue(current, out List<HexTile> nbrs)) continue;

            foreach (HexTile next in nbrs)
            {
                if (cameFrom.ContainsKey(next)) continue;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!found) return null;

        var path = new List<HexTile>();
        HexTile t = end;
        while (t != null) { path.Add(t); t = cameFrom[t]; }
        path.Reverse();
        return path;
    }

    // =====================================================================
    //  GRID DATA COLLECTION
    // =====================================================================

    private void CollectRoadData()
    {
        _roadTiles.Clear();
        _roadNeighbors.Clear();

        foreach (HexTile t in GridManager.Instance.GetRoadTiles())
            _roadTiles.Add(t);

        foreach (HexTile t in _roadTiles)
        {
            List<HexTile> roadNbrs = new List<HexTile>();
            foreach (HexTile n in GridManager.Instance.GetNeighbors(t))
            {
                if (n.type == HexTile.TileType.Road)
                    roadNbrs.Add(n);
            }
            _roadNeighbors[t] = roadNbrs;
        }

        Debug.Log($"[GridVehicleManager] Cached {_roadTiles.Count} road tiles.");
    }

    // =====================================================================
    //  PUBLIC API
    // =====================================================================

    /// <summary>
    /// Call after a grid regeneration to refresh road data and stop/respawn all vehicles.
    /// </summary>
    public void RefreshAfterGridRegeneration()
    {
        // Clear reservations and return all active agents to pool.
        foreach (VehicleAgent a in _active)
        {
            // Individual decrements are skipped here — both dicts are fully
            // cleared below, so direct nulling is sufficient.
            a.occupiedTile = null;
            a.reservedTile = null;
            a.reservedSlot = -1;
            a.obj.SetActive(false);
            a.state = VehicleState.Inactive;
            _pool.Add(a);
        }
        _active.Clear();
        _reservedTiles.Clear();
        _slotOccupancy.Clear();

        CollectRoadData();
        // The SpawnerLoop coroutine is still running — it will top up vehicles on its
        // next interval tick automatically.
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    private Vector3 TileWorldPos(HexTile tile)
    {
        Vector3 p = tile.transform.position;
        p.y += vehicleHeightOffset;
        return p;
    }

    private bool IsIntersection(HexTile tile)
    {
        if (!_roadNeighbors.TryGetValue(tile, out List<HexTile> nbrs)) return false;
        return nbrs.Count >= 3;
    }

    // Attempts to set the alpha on all renderer materials.
    // Supports both the built-in pipeline (_Color) and URP (_BaseColor).
    // If your materials are opaque-only, the vehicle will simply snap off
    // at the end of the fade — set fadeDuration very low in that case.
    private void SetAgentAlpha(VehicleAgent agent, float alpha)
    {
        if (agent.renderers == null) return;
        foreach (Renderer r in agent.renderers)
        {
            if (r == null) continue;
            foreach (Material mat in r.materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color; c.a = alpha; mat.color = c;
                }
                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor"); c.a = alpha;
                    mat.SetColor("_BaseColor", c);
                }
            }
        }
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}