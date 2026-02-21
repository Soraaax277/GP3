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
    public int width = 75;
    public int height = 45;
    public float hexSize = 1f;

    [Header("Continent Generation Settings")]
    [Tooltip("How zoomed in the noise is. Lower values = larger, smoother landmasses.")]
    public float noiseScale = 0.04f; 
    [Tooltip("The base value required to spawn land. Lower = more land overall.")]
    public float landThreshold = -0.1f; 
    [Tooltip("How strongly the edges of the map turn to water. Lower = land pushes closer to edges.")]
    public float edgeFalloff = 0.8f;

    [Header("References")]
    public GameObject hexTilePrefab;

    public Dictionary<Vector3Int, HexTile> tiles =
        new Dictionary<Vector3Int, HexTile>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        tiles.Clear();

        // Determine the center of the grid in world space for the falloff calculation
        Vector3 worldCenter = HexToWorld(width / 2, height / 2);
        
        // The maximum radius based on the grid dimensions
        float maxRadius = (Mathf.Min(width, height) / 2f) * (hexSize * 2f);
        
        // Generate random offsets so the continent is different every time you play
        float randomOffsetX = Random.Range(-10000f, 10000f);
        float randomOffsetY = Random.Range(-10000f, 10000f);

        // New world generation approach:
        // STEP 1: INITIAL NOISE GENERATION
        for (int q = 0; q < width; q++)
        {
            for (int r = 0; r < height; r++)
            {
                Vector3 worldPos = HexToWorld(q, r);
                
                float distFromCenter = Vector3.Distance(worldPos, worldCenter);
                float normalizedDist = distFromCenter / maxRadius;

                float noiseValue = Mathf.PerlinNoise((q + randomOffsetX) * noiseScale, (r + randomOffsetY) * noiseScale);
                float finalLandValue = noiseValue - (normalizedDist * edgeFalloff);

                if (finalLandValue >= landThreshold)
                {
                    Vector3Int cubeCoords = AxialToCube(q, r);

                    GameObject tileObj = Instantiate(
                        hexTilePrefab,
                        worldPos,
                        hexTilePrefab.transform.rotation,
                        transform
                    );

                    HexTile tile = tileObj.GetComponent<HexTile>();
                    tile.Initialize(cubeCoords);

                    tiles.Add(cubeCoords, tile);
                }
            }
        }

        // STEP 2: POST-PROCESSING (REMOVE DETACHED ISLANDS)
        RemoveDisconnectedIslands();

        IsReady = true;
        Debug.Log($"Generated {tiles.Count} hex tiles forming a single contiguous continent.");
    }

    private void RemoveDisconnectedIslands()
    {
        List<HashSet<HexTile>> allLandmasses = new List<HashSet<HexTile>>();
        HashSet<HexTile> unvisitedTiles = new HashSet<HexTile>(tiles.Values);

        // 1. Group all tiles into distinct landmasses
        while (unvisitedTiles.Count > 0)
        {
            // Grab any unvisited tile to start a new flood fill
            var enumerator = unvisitedTiles.GetEnumerator();
            enumerator.MoveNext();
            HexTile startTile = enumerator.Current;

            // Get all connected tiles for this specific island
            HashSet<HexTile> currentLandmass = GetConnectedRegion(startTile);
            allLandmasses.Add(currentLandmass);

            // Remove these tiles from the unvisited pool so we don't check them again
            unvisitedTiles.ExceptWith(currentLandmass);
        }

        // 2. If we have more than one island, we need to prune the smaller ones
        if (allLandmasses.Count > 1)
        {
            // Sort the list of landmasses by size, largest first
            allLandmasses.Sort((a, b) => b.Count.CompareTo(a.Count));

            // The first one [0] is the main continent. Destroy all others.
            for (int i = 1; i < allLandmasses.Count; i++)
            {
                foreach (HexTile islandTile in allLandmasses[i])
                {
                    tiles.Remove(islandTile.cubeCoords);
                    Destroy(islandTile.gameObject);
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

            foreach (HexTile neighbor in GetNeighbors(current))
            {
                if (!region.Contains(neighbor))
                {
                    region.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }
        }

        return region;
    }

    public IEnumerable<HexTile> GetAllTiles()
    {
        return tiles.Values;
    }

    public List<HexTile> GetNeighbors(HexTile tile)
    {
        List<HexTile> neighbors = new List<HexTile>();

        foreach (Vector3Int dir in CubeDirections)
        {
            Vector3Int neighborCoords = tile.cubeCoords + dir;

            if (tiles.TryGetValue(neighborCoords, out HexTile neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }
    
    Vector3 HexToWorld(int q, int r)
    {
        float tileWidth = hexSize * 2f;
        float tileHeight = Mathf.Sqrt(3f) * hexSize;

        float x = tileWidth * (q + r * 0.5f);
        float z = tileHeight * r;

        return new Vector3(x, 0f, z);
    }

    Vector3Int AxialToCube(int q, int r)
    {
        int x = q;
        int z = r;
        int y = -x - z;
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
        {
            if (tile.cubeCoords.x == x && tile.cubeCoords.y == y)
                return tile;
        }
        return null;
    }

    public int CubeDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Max(
            Mathf.Abs(a.x - b.x),
            Mathf.Abs(a.y - b.y),
            Mathf.Abs(a.z - b.z)
        );
    }
    
    public List<HexTile> GetTilesInRange(HexTile centerTile, int range)
    {
        List<HexTile> result = new List<HexTile>();

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = Mathf.Max(-range, -dx - range); dy <= Mathf.Min(range, -dx + range); dy++)
            {
                int dz = -dx - dy;
                Vector3Int coords = centerTile.cubeCoords + new Vector3Int(dx, dy, dz);

                if (tiles.TryGetValue(coords, out HexTile tile))
                    result.Add(tile);
            }
        }

        return result;
    }

    public List<HexTile> GetTilesByCount(HexTile start, int count)
    {
        List<HexTile> result = new List<HexTile>();
        Queue<HexTile> frontier = new Queue<HexTile>();
        HashSet<HexTile> visited = new HashSet<HexTile>();

        frontier.Enqueue(start);
        visited.Add(start);

        while (frontier.Count > 0 && result.Count < count)
        {
            HexTile current = frontier.Dequeue();
            result.Add(current);

            foreach (HexTile neighbor in GetNeighbors(current))
            {
                if (visited.Contains(neighbor)) continue;
                visited.Add(neighbor);
                frontier.Enqueue(neighbor);
            }
        }

        return result;
    }

    public List<HexTile> FindPath(HexTile start, HexTile end)
    {
        if (start == end) return new List<HexTile> { start };

        Queue<HexTile> frontier = new Queue<HexTile>();
        frontier.Enqueue(start);

        Dictionary<HexTile, HexTile> cameFrom = new Dictionary<HexTile, HexTile>();
        cameFrom[start] = null;

        bool found = false;
        while (frontier.Count > 0)
        {
            HexTile current = frontier.Dequeue();

            if (current == end)
            {
                found = true;
                break;
            }

            foreach (HexTile next in GetNeighbors(current))
            {
                if (!cameFrom.ContainsKey(next) && (!next.IsOccupied() || next == end))
                {
                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }
        }

        if (!found) return null;

        List<HexTile> path = new List<HexTile>();
        HexTile temp = end;
        while (temp != null)
        {
            path.Add(temp);
            temp = cameFrom[temp];
        }
        path.Reverse();
        return path;
    }
}