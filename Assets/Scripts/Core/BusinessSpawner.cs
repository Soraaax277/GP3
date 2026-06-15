using UnityEngine;
using System.Collections.Generic;

public class BusinessSpawner : MonoBehaviour
{
    public GameObject businessPrefab;

    [Header("Base Placement")]
    [Tooltip("Candidate tiles are sampled from this fraction of tiles that are furthest " +
             "from all already-placed bases. 0.15 = top 15% most-separated tiles, then " +
             "a random pick within that pool keeps games feeling different each run.")]
    [Range(0.05f, 0.5f)]
    public float separationPoolFraction = 0.15f;

    // World positions of every base placed so far this session.
    // Reset between games by calling ClearSpawnedBases().
    private readonly List<Vector3> _placedBasePositions = new List<Vector3>();

    /// <summary>Clears the placed-base registry. Call this when starting a new game.</summary>
    public void ClearSpawnedBases() => _placedBasePositions.Clear();

    public SignalNode SpawnInitialBusiness(PlayerData player)
    {
        if (GridManager.Instance == null || !GridManager.Instance.IsReady)
        {
            Debug.LogError("GridManager not ready!");
            return null;
        }
        if (businessPrefab == null)
        {
            Debug.LogError("Business prefab not assigned!");
            return null;
        }

        // Gather all unoccupied land tiles.
        List<HexTile> freeTiles = new List<HexTile>();
        foreach (var tile in GridManager.Instance.tiles.Values)
        {
            if (!tile.IsOccupied())
                freeTiles.Add(tile);
        }

        if (freeTiles.Count == 0)
        {
            Debug.LogError("No free tiles available to spawn a business!");
            return null;
        }

        HexTile chosenTile = PickSeparatedTile(freeTiles);
        return SpawnBusiness(chosenTile, player);
    }

    // Picks a tile that is as far as possible from all already-placed bases.
    // Strategy:
    //   1. Score every free tile by its minimum distance to any existing base.
    //      (First player has no bases yet, so every tile scores the same — we
    //      fall through to a random pick from the entire pool, keeping game 1
    //      feeling natural.)
    //   2. Sort descending so the most-separated tiles are at the front.
    //   3. Take the top separationPoolFraction of the list and pick randomly
    //      within that pool — this avoids always landing in the exact same
    //      corner every game while still guaranteeing meaningful separation.
    private HexTile PickSeparatedTile(List<HexTile> freeTiles)
    {
        if (_placedBasePositions.Count == 0)
        {
            // First player — no existing bases to separate from.
            // Pick randomly from the full set so the first base isn't always
            // in the same map corner.
            return freeTiles[Random.Range(0, freeTiles.Count)];
        }

        // Score each tile: minimum squared distance to any placed base.
        // Squared distance avoids Sqrt without affecting the sort order.
        List<(HexTile tile, float score)> scored = new List<(HexTile, float)>(freeTiles.Count);
        foreach (HexTile tile in freeTiles)
        {
            float minDistSq = float.MaxValue;
            Vector3 pos = tile.transform.position;
            foreach (Vector3 basePos in _placedBasePositions)
            {
                float dx = pos.x - basePos.x;
                float dz = pos.z - basePos.z;
                float dSq = dx * dx + dz * dz;
                if (dSq < minDistSq) minDistSq = dSq;
            }
            scored.Add((tile, minDistSq));
        }

        // Sort: highest score (furthest from existing bases) first.
        scored.Sort((a, b) => b.score.CompareTo(a.score));

        // Pick randomly within the top separationPoolFraction.
        int poolSize = Mathf.Max(1, Mathf.RoundToInt(scored.Count * separationPoolFraction));
        return scored[Random.Range(0, poolSize)].tile;
    }

    public SignalNode SpawnBusiness(HexTile tile, PlayerData player)
    {
        if (tile == null || businessPrefab == null) return null;

        Vector3 spawnPos = new Vector3(
            tile.transform.position.x,
            GetBusinessPlacementY(tile, businessPrefab),
            tile.transform.position.z
        );

        GameObject businessObj = Instantiate(businessPrefab, spawnPos, businessPrefab.transform.rotation);

        SignalNode node = businessObj.GetComponent<SignalNode>();
        if (node == null)
        {
            Debug.LogError("Business prefab missing SignalNode component!");
            Destroy(businessObj);
            return null;
        }

        node.Initialize(tile, player);

        // Register this base position so the next player's tile selection
        // maximises separation from it.
        _placedBasePositions.Add(tile.transform.position);

        return node;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Collider-based placement helpers
    //  Mirrors TowerPlacementManager so buildings sit flush on the tile surface
    //  regardless of the hex tile's world-space Y, scale, or collider offset.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the world-space Y of the top surface of the tile's BoxCollider.
    /// Falls back to the tile's pivot Y if no BoxCollider is found.
    /// </summary>
    private float GetTileSurfaceY(HexTile tile)
    {
        BoxCollider box = tile.GetComponent<BoxCollider>();
        if (box == null) return tile.transform.position.y;

        float halfHeight = box.size.y * 0.5f * tile.transform.lossyScale.y;
        float centerY    = box.center.y  * tile.transform.lossyScale.y;
        return tile.transform.position.y + centerY + halfHeight;
    }

    /// <summary>
    /// Temporarily instantiates the prefab to measure the distance from its
    /// pivot down to the bottom of its MeshCollider. This offset is added to
    /// the tile surface Y so the mesh base lands exactly on the tile surface.
    /// Falls back to 0 if the prefab has no MeshCollider.
    /// </summary>
    private float GetBusinessPlacementY(HexTile tile, GameObject prefab)
    {
        float surfaceY = GetTileSurfaceY(tile);

        GameObject temp = Instantiate(prefab);
        float bottomOffset = 0f;

        MeshCollider mc = temp.GetComponentInChildren<MeshCollider>();
        if (mc != null)
            bottomOffset = temp.transform.position.y - mc.bounds.min.y;

        Destroy(temp);
        return surfaceY + bottomOffset;
    }
}