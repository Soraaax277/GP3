using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Builds a water plane that fades to transparent where the island is.
/// No hard holes — uses vertex alpha to feather the transition smoothly.
///
/// Vertex color channels:
///   R = shore proximity  (1 near island edge, 0 far away) → shader draws shallow color
///   A = mask alpha       (0 inside island, feathers to 1 outside over featherWidth)
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterPlane : MonoBehaviour
{
    [Header("Size")]
    public bool  autoSizeFromGrid = true;
    public float gridPadding      = 220f;
    public float manualRadius     = 400f;

    [Header("Position")]
    public float waterY = 0.5f;

    [Header("Feather / Blend")]
    [Tooltip("Must match GridManager.hexSize.")]
    public float hexSize          = 1f;
    [Tooltip("World units over which the water fades in from the island edge. 2–4 looks natural.")]
    public float featherWidth     = 3f;
    [Tooltip("World units of the light shallow-water shore band beyond the feather.")]
    public float shoreWidth       = 5f;

    [Header("Material")]
    public Material waterMaterial;

    private const float Sqrt3 = 1.7320508f;

    // -------------------------------------------------------------------------

    private void Start()
    {
        StartCoroutine(BuildWhenReady());
    }

    private IEnumerator BuildWhenReady()
    {
        while (GridManager.Instance == null || !GridManager.Instance.IsReady)
            yield return null;
        Build();
    }

    private void Build()
    {
        var tilePos = new List<Vector2>();
        foreach (HexTile t in GridManager.Instance.GetAllTiles())
            tilePos.Add(new Vector2(t.transform.position.x, t.transform.position.z));

        if (tilePos.Count == 0) { Debug.LogWarning("[WaterPlane] No tiles found."); return; }

        float radius = manualRadius;
        if (autoSizeFromGrid)
        {
            float maxDist = 0f;
            foreach (var p in tilePos) if (p.magnitude > maxDist) maxDist = p.magnitude;
            radius = maxDist + gridPadding;
        }

        transform.position = new Vector3(0f, waterY, 0f);

        // Spatial hash
        float cellSize = hexSize * 2f;
        var buckets    = new Dictionary<Vector2Int, List<Vector2>>();
        foreach (var t in tilePos)
        {
            var cell = Bucket(t, cellSize);
            if (!buckets.ContainsKey(cell)) buckets[cell] = new List<Vector2>();
            buckets[cell].Add(t);
        }

        // Resolution — step = hexSize*0.5 gives smooth feather
        float step = hexSize * 0.5f;
        int   divs = Mathf.Clamp(Mathf.CeilToInt((radius * 2f) / step), 10, 900);
        step = (radius * 2f) / divs;

        // Hex circumradius
        float hexR = hexSize * (2f / Sqrt3);

        int vps    = divs + 1;
        var verts  = new Vector3[vps * vps];
        var uvs    = new Vector2[vps * vps];
        var colors = new Color[vps * vps];

        for (int z = 0; z <= divs; z++)
        for (int x = 0; x <= divs; x++)
        {
            int   idx = z * vps + x;
            float wx  = -radius + x * step;
            float wz  = -radius + z * step;

            verts[idx] = new Vector3(wx, 0f, wz);
            uvs[idx]   = new Vector2((float)x / divs, (float)z / divs);

            // Signed distance from nearest hex edge:
            //   negative = inside a tile
            //   positive = outside (in the water)
            float edgeDist = NearestHexEdgeDist(new Vector2(wx, wz), buckets, cellSize, hexR);

            // Alpha: 0 deep inside island, feathers to 1 over featherWidth
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeDist / Mathf.Max(featherWidth, 0.01f)));

            // Shore: bright band just outside the island edge
            float shore = 1f - Mathf.Clamp01(Mathf.Max(0f, edgeDist) / Mathf.Max(shoreWidth, 0.01f));

            colors[idx] = new Color(shore, 0f, 0f, alpha);
        }

        // All triangles — no holes, alpha does the blending
        var tris = new int[divs * divs * 6];
        int t2 = 0;
        for (int z = 0; z < divs; z++)
        for (int x = 0; x < divs; x++)
        {
            int bl = z * vps + x;
            tris[t2++] = bl;     tris[t2++] = bl + vps;     tris[t2++] = bl + vps + 1;
            tris[t2++] = bl;     tris[t2++] = bl + vps + 1; tris[t2++] = bl + 1;
        }

        var mesh = new Mesh();
        mesh.name        = "WaterPlane_Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices    = verts;
        mesh.uv          = uvs;
        mesh.colors      = colors;
        mesh.triangles   = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;

        var mr = GetComponent<MeshRenderer>();
        if (waterMaterial != null) mr.material = waterMaterial;
        else Debug.LogWarning("[WaterPlane] Assign Custom/URP/WorldWater material.");
        mr.sortingOrder = -1;

        Debug.Log($"[WaterPlane] Built feathered: divs={divs} tris={tris.Length/3}");
    }

    // Signed hex edge distance. Negative = inside, positive = outside.
    private float NearestHexEdgeDist(Vector2 p, Dictionary<Vector2Int, List<Vector2>> buckets,
                                     float cellSize, float R)
    {
        float minDist = float.MaxValue;
        var   center  = Bucket(p, cellSize);

        for (int dz = -2; dz <= 2; dz++)
        for (int dx = -2; dx <= 2; dx++)
        {
            var key = new Vector2Int(center.x + dx, center.y + dz);
            if (!buckets.TryGetValue(key, out var list)) continue;
            foreach (var t in list)
            {
                float adx = Mathf.Abs(p.x - t.x);
                float adz = Mathf.Abs(p.y - t.y);
                // Hex signed distance approximation
                float v1 = adx / (R * 0.8660254f) - 1f;
                float v2 = (adz + adx / Sqrt3) / R - 1f;
                float d  = Mathf.Max(v1, v2) * R;
                if (d < minDist) minDist = d;
            }
        }

        return minDist == float.MaxValue ? float.MaxValue : minDist;
    }

    private static Vector2Int Bucket(Vector2 p, float cellSize)
        => new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.y / cellSize));
}