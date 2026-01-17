using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
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

        foreach (var kvp in tiles)
        {
            HexTile tile = kvp.Value;
            if (CubeDistance(centerTile.cubeCoords, tile.cubeCoords) <= range)
            {
                result.Add(tile);
            }
        }

        return result;
    }

}
