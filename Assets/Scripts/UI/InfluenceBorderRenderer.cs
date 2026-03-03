using UnityEngine;
using System.Collections.Generic;

public class InfluenceBorderRenderer : MonoBehaviour
{
    public static InfluenceBorderRenderer Instance;

    [Header("Visual Settings")]
    public float borderHeightOffset = 0.55f;
    public float lineWidth = 0.28f; 
    public Material borderMaterial;
    public float topYPadding = 0.06f;

    [Header("Animation Settings")]
    public float growthSpeed = 4f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;

    private List<Vector3> verts = new List<Vector3>();
    private List<int> tris = new List<int>();
    private List<Color> colors = new List<Color>();

    // Track animation progress (0.0 to 1.0) for every potential perimeter edge
    // Key: string "x,y,z,dirIndex" (tile-relative edge identification)
    private Dictionary<string, float> edgeProgress = new Dictionary<string, float>();
    private HashSet<string> currentActivePerimeters = new HashSet<string>();
    
    // Track colors specifically for fading out
    private Dictionary<string, Color> edgeLastColors = new Dictionary<string, Color>();

    private bool _isFirstUpdate = true;
    private bool _hasInitialRun = false;

    private void Awake()
    {
        Instance = this;
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;

        if (borderMaterial == null)
            borderMaterial = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material = borderMaterial;
        
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
    }

    private void Update()
    {
        // One-time initial run after grid is ready to show starting territory (HQ)
        if (!_hasInitialRun && GridManager.Instance != null && GridManager.Instance.IsReady)
        {
            UpdateBorders();
            _hasInitialRun = true;
        }

        bool needsRebuild = false;
        List<string> keys = new List<string>(edgeProgress.Keys);

        foreach (var key in keys)
        {
            float target = currentActivePerimeters.Contains(key) ? 1f : 0f;
            
            if (!Mathf.Approximately(edgeProgress[key], target))
            {
                edgeProgress[key] = Mathf.MoveTowards(edgeProgress[key], target, Time.deltaTime * growthSpeed);
                needsRebuild = true;
                
                // Cleanup edges that finished fading out
                if (edgeProgress[key] <= 0f && target == 0f)
                {
                    edgeProgress.Remove(key);
                    edgeLastColors.Remove(key);
                }
            }
        }

        if (needsRebuild)
        {
            RebuildMesh();
        }
    }

    public void UpdateBorders()
    {
        if (GridManager.Instance == null || !GridManager.Instance.IsReady) return;

        // 1. IDENTIFY ALL PERIMETER EDGES
        currentActivePerimeters.Clear();

        Vector3Int[] directions = {
            new Vector3Int( 1, -1,  0), 
            new Vector3Int( 1,  0, -1), 
            new Vector3Int( 0,  1, -1), 
            new Vector3Int(-1,  1,  0), 
            new Vector3Int(-1,  0,  1), 
            new Vector3Int( 0, -1,  1)  
        };

        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
        {
            PlayerData owner = tile.GetOwner();
            if (owner == null) continue;

            // Fog Of War Filter
            if (owner.isAI && !tile.isVisible) continue;
            if (!tile.isExplored) continue;

            for (int i = 0; i < 6; i++)
            {
                HexTile neighbor = GridManager.Instance.GetTile(tile.cubeCoords + directions[i]);

                if (neighbor == null || neighbor.GetOwner() != owner)
                {
                    string edgeID = $"{tile.cubeCoords.x},{tile.cubeCoords.y},{tile.cubeCoords.z}_{i}";
                    currentActivePerimeters.Add(edgeID);
                    
                    // Initialize if first time seeing this edge
                    if (!edgeProgress.ContainsKey(edgeID))
                    {
                        // If it's the very first time building borders (game start), make them instant.
                        // If not, start at 0 to trigger the growth animation.
                        edgeProgress[edgeID] = _isFirstUpdate ? 1f : 0f;
                    }
                    edgeLastColors[edgeID] = owner.playerColor;
                }
            }
        }

        _isFirstUpdate = false;
        RebuildMesh();
    }

    private void RebuildMesh()
    {
        ClearMesh();

        // Standard neighbor directions for Pointy Top
        Vector3Int[] directions = {
            new Vector3Int( 1, -1,  0), 
            new Vector3Int( 1,  0, -1), 
            new Vector3Int( 0,  1, -1), 
            new Vector3Int(-1,  1,  0), 
            new Vector3Int(-1,  0,  1), 
            new Vector3Int( 0, -1,  1)  
        };

        foreach (var entry in edgeProgress)
        {
            float animProgress = entry.Value;
            if (animProgress <= 0.001f) continue;

            // Parse tile and face from the key
            string[] parts = entry.Key.Split('_');
            string[] coords = parts[0].Split(',');
            int dirIndex = int.Parse(parts[1]);
            Vector3Int tilePos = new Vector3Int(int.Parse(coords[0]), int.Parse(coords[1]), int.Parse(coords[2]));

            HexTile tile = GridManager.Instance.GetTile(tilePos);
            if (tile == null) continue;

            Color baseColor = edgeLastColors[entry.Key];

            // Re-calc geometry for this specific edge
            float tileTopY = borderHeightOffset;
            if (tile.TryGetComponent<Renderer>(out Renderer r))
                tileTopY = r.bounds.max.y + topYPadding;

            Vector3 center = tile.transform.position;
            center.y = tileTopY;

            // Calculate Edge points (Same as our proven Midpoint Logic)
            Vector3 nDir;
            HexTile neighbor = GridManager.Instance.GetTile(tile.cubeCoords + directions[dirIndex]);
            if (neighbor != null)
                nDir = (neighbor.transform.position - tile.transform.position);
            else
            {
                Vector3Int d = directions[dirIndex];
                float tW = GridManager.Instance.hexSize * 2f;
                float tH = Mathf.Sqrt(3f) * GridManager.Instance.hexSize;
                nDir = new Vector3(tW * (d.x + d.z * 0.5f), 0, tH * d.z);
            }
            nDir.y = 0; nDir = nDir.normalized;
            Vector3 eDir = new Vector3(-nDir.z, 0, nDir.x);
            Vector3 edgeMid = center + nDir * GridManager.Instance.hexSize;
            float hLen = GridManager.Instance.hexSize * 0.57735f;

            // ANIMATION WRAP: Shrink length and width
            // This makes the removal look like a clean dissolve/retreat
            Vector3 v1Outer = edgeMid - eDir * (hLen * animProgress);
            Vector3 v2Outer = edgeMid + eDir * (hLen * animProgress);
            
            float currentWidth = lineWidth * animProgress;
            Vector3 v1Inner = v1Outer - nDir * currentWidth;
            Vector3 v2Inner = v2Outer - nDir * currentWidth;

            // Fade alpha based on progress too
            Color finalColor = baseColor;
            finalColor.a = animProgress;

            AddQuad(v1Inner, v1Outer, v2Inner, v2Outer, finalColor);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void AddQuad(Vector3 v1Inner, Vector3 v1Outer, Vector3 v2Inner, Vector3 v2Outer, Color color)
    {
        int b = verts.Count;
        verts.Add(v1Inner); verts.Add(v1Outer); verts.Add(v2Inner); verts.Add(v2Outer);

        // Alpha Glow: Outer edge is bright, inner is soft
        // Multiply player-set alpha for the dissolve effect
        colors.Add(new Color(color.r, color.g, color.b, color.a * 0.3f)); 
        colors.Add(new Color(color.r, color.g, color.b, color.a * 1.0f)); 
        colors.Add(new Color(color.r, color.g, color.b, color.a * 0.3f)); 
        colors.Add(new Color(color.r, color.g, color.b, color.a * 1.0f)); 

        tris.Add(b + 0); tris.Add(b + 1); tris.Add(b + 2);
        tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
    }

    private void ClearMesh()
    {
        if (mesh != null) mesh.Clear();
        verts.Clear(); tris.Clear(); colors.Clear();
    }
}
