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

        for (int q = 0; q < width; q++)
        {
            for (int r = 0; r < height; r++)
            {
                Vector3 worldPos = HexToWorld(q, r);
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

        IsReady = true;
        Debug.Log($"Generated {tiles.Count} hex tiles");
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
        float width = hexSize * 2f;
        float height = Mathf.Sqrt(3f) * hexSize;

        float x = width * (q + r * 0.5f);
        float z = height * r;

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
