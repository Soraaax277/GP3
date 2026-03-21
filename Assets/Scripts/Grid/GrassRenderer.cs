using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GrassRenderer : MonoBehaviour
{
    public static GrassRenderer Instance;

    [Header("References")]
    public Material grassMaterial;

    [Header("Density")]
    public int bladesMin = 30;
    public int bladesMax = 34;

    [Header("Blade Size")]
    public float bladeWidth  = 0.35f;
    public float bladeHeight = 0.45f;

    [Header("Scatter")]
    [Tooltip("Match GridManager.hexSize.")]
    public float hexSize       = 1f;
    public float scatterRadius = 0.75f;
    public float yOffset       = 0.05f;

    [Header("LOD Distances")]
    public float lodFullDist    = 20f;
    public float lodHalfDist    = 45f;
    public float lodQuarterDist = 75f;
    public float lodCullDist    = 110f;
    [Tooltip("Camera must move this far before LOD levels are recalculated. Prevents per-frame flicker.")]
    public float lodUpdateStep  = 4f;

    // LOD levels per blade — cached, only updated when camera moves lodUpdateStep
    private enum LodLevel { Full, Half, Quarter, Culled }

    private struct BladeData
    {
        public Matrix4x4 matrix;
        public HexTile   tile;
        public Vector3   worldPos;
        public int       bladeIndex;
        public LodLevel  lod;        // cached — not recalculated every frame
    }

    private List<BladeData> _allBlades   = new List<BladeData>();
    private List<Matrix4x4> _visibleMats = new List<Matrix4x4>(1024);
    private Mesh            _bladeMesh;
    private const int       BatchSize    = 1023;

    // Last camera position when we did a LOD update
    private Vector3 _lastLodCamPos = new Vector3(float.MaxValue, 0, float.MaxValue);

    private void Awake() { Instance = this; }

    private void Start()
    {
        _bladeMesh = BuildTaperedBladeMesh(bladeWidth, bladeHeight);
        StartCoroutine(InitWhenReady());
    }

    private IEnumerator InitWhenReady()
    {
        while (GridManager.Instance == null || !GridManager.Instance.IsReady)
            yield return null;
        GenerateBlades();
    }

    private void GenerateBlades()
    {
        _allBlades.Clear();

        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
        {
            if (tile.type != HexTile.TileType.Land) continue;

            Vector3 tilePos = tile.transform.position;
            int     count   = bladesMin + Mathf.Abs(Hash(tilePos.x * 7.3f, tilePos.z * 13.1f)) % (bladesMax - bladesMin + 1);
            float   maxR    = hexSize * scatterRadius;

            for (int i = 0; i < count; i++)
            {
                float seedX = tilePos.x * 127.1f + i * 311.7f;
                float seedZ = tilePos.z * 269.5f + i * 183.3f;
                float ox    = (Frac(Mathf.Sin(seedX) * 43758.5f) * 2f - 1f) * maxR;
                float oz    = (Frac(Mathf.Sin(seedZ) * 43758.5f) * 2f - 1f) * maxR;

                if (ox * ox + oz * oz > maxR * maxR) { ox *= 0.6f; oz *= 0.6f; }

                Vector3    pos       = new Vector3(tilePos.x + ox, tilePos.y + yOffset, tilePos.z + oz);
                float      rotSeed   = Frac(Mathf.Sin(seedX * 0.3f + seedZ * 0.7f) * 53421.3f);
                float      scaleSeed = Frac(Mathf.Sin(seedX * 1.3f + seedZ * 2.1f) * 21341.7f);

                _allBlades.Add(new BladeData
                {
                    matrix     = Matrix4x4.TRS(pos, Quaternion.Euler(0f, rotSeed * 360f, 0f), Vector3.one * Mathf.Lerp(0.75f, 1.3f, scaleSeed)),
                    tile       = tile,
                    worldPos   = pos,
                    bladeIndex = i,
                    lod        = LodLevel.Full   // will be set on first UpdateLOD
                });
            }
        }

        Debug.Log($"[GrassRenderer] {_allBlades.Count} blades generated.");
    }

    // ------------------------------------------------------------------
    // Recalculates per-blade LOD levels.
    // Called only when camera has moved more than lodUpdateStep — NOT every frame.
    // Hysteresis: use slightly wider "out" distances to prevent boundary flicker.
    // ------------------------------------------------------------------
    private void UpdateLOD(Vector3 camPos)
    {
        // Hysteresis margins — must move 15% PAST the boundary before switching
        float hysteresis   = 1.15f;
        float fullSq       = lodFullDist    * lodFullDist;
        float halfSq       = lodHalfDist    * lodHalfDist;
        float quarterSq    = lodQuarterDist * lodQuarterDist;
        float cullSq       = lodCullDist    * lodCullDist;

        // Widen the "downgrade" threshold, narrow the "upgrade" threshold
        float halfOutSq    = (lodHalfDist    * hysteresis) * (lodHalfDist    * hysteresis);
        float quarterOutSq = (lodQuarterDist * hysteresis) * (lodQuarterDist * hysteresis);
        float cullOutSq    = (lodCullDist    * hysteresis) * (lodCullDist    * hysteresis);

        for (int i = 0; i < _allBlades.Count; i++)
        {
            BladeData b    = _allBlades[i];
            float dx       = b.worldPos.x - camPos.x;
            float dz       = b.worldPos.z - camPos.z;
            float distSq   = dx * dx + dz * dz;

            LodLevel newLod = b.lod; // keep current unless crossing hysteresis band

            switch (b.lod)
            {
                case LodLevel.Full:
                    if      (distSq > halfOutSq)    newLod = LodLevel.Half;
                    else if (distSq > quarterOutSq) newLod = LodLevel.Quarter;
                    else if (distSq > cullOutSq)    newLod = LodLevel.Culled;
                    break;

                case LodLevel.Half:
                    if      (distSq <= fullSq)      newLod = LodLevel.Full;
                    else if (distSq > quarterOutSq) newLod = LodLevel.Quarter;
                    else if (distSq > cullOutSq)    newLod = LodLevel.Culled;
                    break;

                case LodLevel.Quarter:
                    if      (distSq <= fullSq)      newLod = LodLevel.Full;
                    else if (distSq <= halfSq)      newLod = LodLevel.Half;
                    else if (distSq > cullOutSq)    newLod = LodLevel.Culled;
                    break;

                case LodLevel.Culled:
                    if      (distSq <= fullSq)      newLod = LodLevel.Full;
                    else if (distSq <= halfSq)      newLod = LodLevel.Half;
                    else if (distSq <= quarterSq)   newLod = LodLevel.Quarter;
                    break;
            }

            b.lod        = newLod;
            _allBlades[i] = b;
        }

        _lastLodCamPos = camPos;
    }

    private void Update()
    {
        if (_bladeMesh == null || grassMaterial == null || _allBlades.Count == 0) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;

        // Only recalculate LOD when camera has moved lodUpdateStep world units
        float movedSq = (camPos - _lastLodCamPos).sqrMagnitude;
        if (movedSq >= lodUpdateStep * lodUpdateStep)
            UpdateLOD(camPos);

        // Build visible matrix list from cached LOD levels — no distance math here
        _visibleMats.Clear();
        foreach (var blade in _allBlades)
        {
            if (blade.tile == null || !blade.tile.isExplored) continue;

            switch (blade.lod)
            {
                case LodLevel.Culled:  continue;
                case LodLevel.Half:    if (blade.bladeIndex % 2 != 0) continue; break;
                case LodLevel.Quarter: if (blade.bladeIndex % 4 != 0) continue; break;
                case LodLevel.Full:    break;
            }

            _visibleMats.Add(blade.matrix);
        }

        // Draw in batches of 1023
        int total = _visibleMats.Count;
        int start = 0;
        while (start < total)
        {
            int         count = Mathf.Min(BatchSize, total - start);
            Matrix4x4[] batch = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
                batch[i] = _visibleMats[start + i];
            Graphics.DrawMeshInstanced(_bladeMesh, 0, grassMaterial, batch, count);
            start += count;
        }
    }

    private Mesh BuildTaperedBladeMesh(float w, float h)
    {
        int segs         = 3;
        int vertsPerQuad = (segs + 1) * 2;

        Vector3[] verts = new Vector3[vertsPerQuad * 2];
        Vector2[] uvs   = new Vector2[vertsPerQuad * 2];
        var       tris  = new List<int>();

        void BuildBlade(int vertOffset, bool alongX)
        {
            for (int s = 0; s <= segs; s++)
            {
                float t    = (float)s / segs;
                float hw   = (w * 0.5f) * (1f - t);
                float lean = t * t * h * 0.25f;
                int   vi   = vertOffset + s * 2;

                if (alongX)
                {
                    verts[vi]     = new Vector3(-hw, t * h, lean);
                    verts[vi + 1] = new Vector3( hw, t * h, lean);
                }
                else
                {
                    verts[vi]     = new Vector3(lean, t * h, -hw);
                    verts[vi + 1] = new Vector3(lean, t * h,  hw);
                }

                uvs[vi]     = new Vector2(0f, t);
                uvs[vi + 1] = new Vector2(1f, t);
            }

            for (int s = 0; s < segs; s++)
            {
                int bl = vertOffset + s * 2;
                int tl = bl + 2;
                tris.Add(bl); tris.Add(bl+1); tris.Add(tl+1);
                tris.Add(bl); tris.Add(tl+1); tris.Add(tl);
                tris.Add(bl); tris.Add(tl+1); tris.Add(bl+1);
                tris.Add(bl); tris.Add(tl);   tris.Add(tl+1);
            }
        }

        BuildBlade(0,            true);
        BuildBlade(vertsPerQuad, false);

        Mesh mesh = new Mesh();
        mesh.name      = "GrassBlade_Tapered";
        mesh.vertices  = verts;
        mesh.uv        = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float Frac(float x) => x - Mathf.Floor(x);
    private static int   Hash(float a, float b) => (int)(Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5f);
}