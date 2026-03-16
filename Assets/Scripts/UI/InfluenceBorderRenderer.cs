using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class InfluenceBorderRenderer : MonoBehaviour
{
    public static InfluenceBorderRenderer Instance;

    [Header("Visual Settings")]
    [SerializeField] public float borderHeightOffset = 1.2f; 
    [SerializeField] public float lineWidth = 0.5f; 
    [SerializeField] public Material borderMaterial;
    [SerializeField] public float topYPadding = 0.3f;

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

    // True until ProcessBorderUpdate has run at least once.
    // New edges during that first call start at progress=1 (instant, no grow animation).
    private bool _isFirstUpdate = true;
    private bool _forceRebuild = false;
    private float _warmupTimer = 2.0f; 

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
        
        // Force world origin and identity to match world-space vertex data
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnGameStatusChanged += UpdateBorders;
    }

    private IEnumerator Start()
    {
        // Wait for several frames AND some real time to ensure Unity engine 
        // internal mesh/physics bounds are fully updated and stable.
        yield return new WaitForSeconds(0.1f);
        yield return new WaitForEndOfFrame();
        
        UpdateBorders();
        _forceRebuild = true; 
    }

    private void OnDestroy()
    {
    }

    private void Update()
    {
        // Removed the initial run here to rely on TurnManager's explicit start-of-game call.
        // This prevents the border system from firing before influence is actually calculated.

        bool needsRebuild = false;
        List<string> keys = new List<string>(edgeProgress.Keys);

        foreach (var key in keys)
        {
            float target = currentActivePerimeters.Contains(key) ? 1f : 0f;
            
            if (!Mathf.Approximately(edgeProgress[key], target) || _forceRebuild)
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

        if (_warmupTimer > 0)
        {
            _warmupTimer -= Time.deltaTime;
            _forceRebuild = true; // Force frequent geometry updates during startup
        }

        if (needsRebuild || _forceRebuild)
        {
            RebuildMesh();
            _forceRebuild = false;
        }
    }

    public void UpdateBorders()
    {
        ProcessBorderUpdate();
    }

    private void ProcessBorderUpdate()
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

            // Fog Of War Filter: Enemy AI borders only show if visible.
            // Player borders always show if they have influence (prevents Turn 1 disappearance).
            if (owner.isAI && (!tile.isVisible || !tile.isExplored)) continue;

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
                        // On Turn 1 OR the very first time we see any tile, make them instant.
                        // This ensures the initial ring around the HQ is visible immediately.
                        bool isFirstTurn = (TurnManager.Instance != null && TurnManager.Instance.currentTurn <= 1);
                        edgeProgress[edgeID] = (isFirstTurn || _isFirstUpdate) ? 1f : 0f;
                    }
                    edgeLastColors[edgeID] = owner.playerColor;
                }
            }
        }

        // Mark that the first ProcessBorderUpdate has run, regardless of tile count.
        _isFirstUpdate = false;
        
        if (currentActivePerimeters.Count == 0 && edgeProgress.Count == 0)
        {
            ClearMesh();
            return;
        }

        _forceRebuild = true; 
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

            // Improved height detection using both BoxCollider (precise) and Renderer (fallback)
            float tileTopY = GetTileSurfaceY(tile) + topYPadding;

            Vector3 center = tile.transform.position;
            center.y = tileTopY;

            // Calculate Edge points dynamically based on actual neighbor distance
            Vector3 nDir;
            float nDist;
            HexTile neighbor = GridManager.Instance.GetTile(tile.cubeCoords + directions[dirIndex]);
            
            if (neighbor != null)
            {
                Vector3 diff = neighbor.transform.position - tile.transform.position;
                diff.y = 0;
                nDist = diff.magnitude;
                nDir = diff.normalized;
            }
            else
            {
                // Fallback for map edges
                float tW = GridManager.Instance.hexSize * 2f;
                float tH = Mathf.Sqrt(3f) * GridManager.Instance.hexSize;
                Vector3Int d = directions[dirIndex];
                Vector3 fallbackDir = new Vector3(tW * (d.x + d.z * 0.5f), 0, tH * d.z);
                nDist = fallbackDir.magnitude;
                nDir = fallbackDir.normalized;
            }

            // MATHEMATICAL HEX CORNERS - CENTERED RIBBON
            // We calculate the exact corners of the hex and then expand the ribbon 
            // both INWARD and OUTWARD from that line. This creates perfect miters.
            float R = nDist / 1.73205f; 
            float halfWidth = lineWidth * 0.5f;

            // Outer Vertices (Hanging slightly over the edge)
            Vector3 v1Outer = center + (Quaternion.Euler(0, -30, 0) * nDir) * (R + halfWidth);
            Vector3 v2Outer = center + (Quaternion.Euler(0, 30, 0) * nDir) * (R + halfWidth);
            
            // Inner Vertices (Receding into the tile)
            Vector3 v1Inner = center + (Quaternion.Euler(0, -30, 0) * nDir) * (R - halfWidth);
            Vector3 v2Inner = center + (Quaternion.Euler(0, 30, 0) * nDir) * (R - halfWidth);

            // Fade alpha based on progress
            Color finalColor = baseColor;
            finalColor.a = animProgress * 0.85f; 

            AddQuad(v1Inner, v1Outer, v2Inner, v2Outer, finalColor);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private float GetTileSurfaceY(HexTile tile)
    {
        if (tile == null) return borderHeightOffset;

        // Try BoxCollider first — bounds.max.y is the top of the collider in world space.
        // This is much more reliable than manual pivot + scale math at game start.
        BoxCollider box = tile.GetComponentInChildren<BoxCollider>();
        if (box != null && box.enabled)
        {
            // If the box is extremely thin or unset, fall back
            if (box.bounds.size.y > 0.01f)
                return box.bounds.max.y;
        }

        // Fallback to Renderer bounds (world space top)
        Renderer r = tile.GetComponentInChildren<Renderer>();
        if (r != null && r.bounds.size.y > 0.01f)
        {
            return r.bounds.max.y;
        }

        // Final fallback: use the tile center and add the offset.
        return tile.transform.position.y + borderHeightOffset;
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

        // Fix winding order to Clockwise (visible from above)
        tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 1);
        tris.Add(b + 2); tris.Add(b + 3); tris.Add(b + 1);
    }

    private void ClearMesh()
    {
        if (mesh != null) mesh.Clear();
        verts.Clear(); tris.Clear(); colors.Clear();
    }
}
