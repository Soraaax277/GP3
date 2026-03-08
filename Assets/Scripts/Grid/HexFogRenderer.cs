using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Builds a procedural mesh from all unexplored (shroud) tiles,
/// floats it Y+fogYOffset above the map, and drives a particle system
/// on that shape to produce the volumetric fog cloud effect.
///
/// On reveal:
///   - The fog fade quad dissolves smoothly with InOutSine easing
///   - Each newly revealed tile rises from below its resting position
///   - Tiles stagger their reveal delay by distance from the reveal centroid
///     so the effect ripples outward from the vision source
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexFogRenderer : MonoBehaviour
{
    public static HexFogRenderer Instance;

    [Header("Fog Mesh")]
    [Tooltip("How high above the tile layer the fog mesh floats.")]
    public float fogYOffset = 1f;
    [Tooltip("The radius of each hex tile in world units. Match your GridManager hexSize.")]
    public float hexRadius = 1f;

    [Header("Particle System")]
    public ParticleSystem fogParticleSystem;
    [Tooltip("How many particles to emit per unexplored tile.")]
    public float particlesPerTile = 50f;

    [Header("Fade Settings")]
    [Tooltip("Material used for per-tile fade quads. Use the FogFadeQuad shader.")]
    public Material fadeMaterial;
    [Tooltip("How long each tile's fog quad takes to fully fade out.")]
    public float fadeDuration = 1.5f;
    [Tooltip("Short pause before the fade begins, giving particles a moment to react.")]
    public float fadeDelay = 0.1f;
    [Tooltip("Color of the fade quad — match your fog shroud tile color.")]
    public Color fadeColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [Header("Tile Reveal Animation")]
    [Tooltip("How far below their resting position tiles start before rising up.")]
    public float riseAmount = 0.4f;
    [Tooltip("How long each tile takes to rise to its final position.")]
    public float riseDuration = 0.6f;
    [Tooltip("Extra delay added per unit of distance from the reveal center, creating the ripple.")]
    public float rippleDelayPerUnit = 0.04f;
    [Tooltip("Maximum stagger delay cap so far tiles don't wait forever.")]
    public float maxRippleDelay = 0.5f;

    private MeshFilter meshFilter;

    // Track which tiles were unexplored last update so we can detect newly explored ones
    private HashSet<HexTile> previouslyUnexplored = new HashSet<HexTile>();

    // Store each tile's resting Y so we always rise back to the right position
    private Dictionary<HexTile, float> tileRestingY = new Dictionary<HexTile, float>();

    private void Awake()
    {
        Instance = this;
        meshFilter = GetComponent<MeshFilter>();

        Vector3 pos = transform.position;
        pos.y = fogYOffset;
        transform.position = pos;
    }

    private void Start()
    {
        StartCoroutine(InitOnGridReady());
    }

    private IEnumerator InitOnGridReady()
    {
        while (GridManager.Instance == null || !GridManager.Instance.IsReady)
            yield return null;

        // Cache every tile's resting Y before any animation touches it
        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
            tileRestingY[tile] = tile.transform.position.y;

        UpdateFog();
    }

    /// <summary>
    /// Called by FieldOfViewManager at the end of each UpdateFogOfWar().
    /// </summary>
    public void UpdateFog()
    {
        if (GridManager.Instance == null) return;

        List<HexTile> unexploredTiles = new List<HexTile>();
        HashSet<HexTile> currentlyUnexplored = new HashSet<HexTile>();

        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
        {
            if (!tile.isExplored)
            {
                unexploredTiles.Add(tile);
                currentlyUnexplored.Add(tile);
            }
        }

        // Find tiles that just became explored this update
        List<HexTile> newlyRevealed = new List<HexTile>();
        foreach (HexTile tile in previouslyUnexplored)
        {
            if (!currentlyUnexplored.Contains(tile))
                newlyRevealed.Add(tile);
        }

        previouslyUnexplored = currentlyUnexplored;

        if (newlyRevealed.Count > 0)
            AnimateReveal(newlyRevealed);

        // Full map explored — stop everything
        if (unexploredTiles.Count == 0)
        {
            if (fogParticleSystem != null) fogParticleSystem.Stop();
            if (meshFilter.sharedMesh != null)
            {
                Destroy(meshFilter.sharedMesh);
                meshFilter.mesh = null;
            }
            return;
        }

        // Rebuild the procedural fog mesh for remaining unexplored tiles
        Mesh fogMesh = BuildProceduralMesh(unexploredTiles);
        if (meshFilter.sharedMesh != null) Destroy(meshFilter.sharedMesh);
        meshFilter.mesh = fogMesh;

        if (fogParticleSystem != null)
        {
            var shape = fogParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.mesh = fogMesh;

            var emission = fogParticleSystem.emission;
            emission.rateOverTime = unexploredTiles.Count * particlesPerTile;

            fogParticleSystem.Simulate(5f, true, true);
            fogParticleSystem.Play();
        }
    }

    /// <summary>
    /// Handles both the fade quad dissolve and the tile rise animation
    /// for all tiles revealed in this update, with ripple stagger.
    /// </summary>
    private void AnimateReveal(List<HexTile> newlyRevealed)
    {
        // Compute the centroid of all newly revealed tiles so ripple radiates
        // outward from the center of the vision burst rather than a fixed point
        Vector3 centroid = Vector3.zero;
        foreach (HexTile tile in newlyRevealed)
            centroid += tile.transform.position;
        centroid /= newlyRevealed.Count;

        foreach (HexTile tile in newlyRevealed)
        {
            float dist = Vector3.Distance(
                new Vector3(tile.transform.position.x, 0, tile.transform.position.z),
                new Vector3(centroid.x, 0, centroid.z)
            );

            // Stagger delay grows with distance from centroid, capped at maxRippleDelay
            float stagger = Mathf.Min(dist * rippleDelayPerUnit, maxRippleDelay);

            // 1. Fade quad dissolve — starts slightly after stagger so tile is already
            //    beginning to rise before the fog above it clears
            SpawnFadeQuad(tile, stagger);

            // 2. Tile rise — snap tile below ground, then tween up to resting Y
            AnimateTileRise(tile, stagger);
        }
    }

    /// <summary>
    /// Instantly drops the tile below its resting Y, then DOTweens it back up
    /// with an OutBack ease for a satisfying overshoot land.
    /// </summary>
    private void AnimateTileRise(HexTile tile, float delay)
    {
        if (!tileRestingY.TryGetValue(tile, out float restY)) return;

        // Kill any existing tween on this tile to prevent conflicts
        tile.transform.DOKill();

        // Snap the tile to its sunken position immediately
        Vector3 pos = tile.transform.position;
        pos.y = restY - riseAmount;
        tile.transform.position = pos;

        // Tween back up to resting Y with OutBack for a gentle overshoot
        tile.transform.DOMoveY(restY, riseDuration)
            .SetDelay(delay)
            .SetEase(Ease.OutBack, 0.8f); // Subtle overshoot, not bouncy
    }

    /// <summary>
    /// Spawns a flat quad at the tile's position at fog height,
    /// then DOTweens its alpha 1 to 0 with a smooth InOutSine curve.
    /// </summary>
    private void SpawnFadeQuad(HexTile tile, float delay)
    {
        if (fadeMaterial == null) return;

        GameObject fadeObj = new GameObject("FogFadeQuad");
        fadeObj.transform.position = new Vector3(
            tile.transform.position.x,
            fogYOffset,
            tile.transform.position.z
        );

        MeshFilter mf   = fadeObj.AddComponent<MeshFilter>();
        MeshRenderer mr = fadeObj.AddComponent<MeshRenderer>();
        mf.mesh = BuildSingleQuad(hexRadius);

        Material mat = new Material(fadeMaterial);
        mat.color = fadeColor;
        mr.material = mat;

        mat.DOFade(0f, fadeDuration)
            .SetDelay(fadeDelay + delay)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => Destroy(fadeObj));
    }

    private Mesh BuildSingleQuad(float r)
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-r, 0,  r),
            new Vector3( r, 0,  r),
            new Vector3( r, 0, -r),
            new Vector3(-r, 0, -r),
        };
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(1, 0), new Vector2(0, 0),
        };
        mesh.RecalculateNormals();
        return mesh;
    }

    private Mesh BuildProceduralMesh(List<HexTile> tiles)
    {
        int tileCount   = tiles.Count;
        Vector3[] verts = new Vector3[tileCount * 4];
        int[]     tris  = new int[tileCount * 6];
        Vector2[] uvs   = new Vector2[tileCount * 4];
        float r         = hexRadius;

        for (int i = 0; i < tileCount; i++)
        {
            Vector3 localPos = transform.InverseTransformPoint(tiles[i].transform.position);
            localPos.y = 0f;

            int vi = i * 4;
            verts[vi + 0] = localPos + new Vector3(-r, 0,  r);
            verts[vi + 1] = localPos + new Vector3( r, 0,  r);
            verts[vi + 2] = localPos + new Vector3( r, 0, -r);
            verts[vi + 3] = localPos + new Vector3(-r, 0, -r);

            uvs[vi + 0] = new Vector2(0, 1); uvs[vi + 1] = new Vector2(1, 1);
            uvs[vi + 2] = new Vector2(1, 0); uvs[vi + 3] = new Vector2(0, 0);

            int ti = i * 6;
            tris[ti + 0] = vi; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 2;
            tris[ti + 3] = vi; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;
        }

        Mesh mesh = new Mesh();
        mesh.name        = "Combined_Fog_Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices    = verts;
        mesh.triangles   = tris;
        mesh.uv          = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}