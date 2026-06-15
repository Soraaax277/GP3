using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

// Builds a procedural mesh from all unexplored (shroud) tiles,
// floats it Y+fogYOffset above the map, and drives a particle system
// on that shape to produce the volumetric fog cloud effect.
//
// On reveal:
//   - The fog fade quad dissolves smoothly with InOutSine easing
//   - Each newly revealed tile rises from below its resting position
//   - Tiles stagger their reveal delay by distance from the reveal centroid
//     so the effect ripples outward from the vision source
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

        // Safety net: if any tiles are already explored at this point (e.g. pre-marked
        // by a save system), reveal them immediately so they start with correct materials.
        if (HexTileReveal.Instance != null)
        {
            foreach (HexTile tile in GridManager.Instance.GetAllTiles())
            {
                if (tile.isExplored)
                    HexTileReveal.Instance.RevealTile(tile);
            }
        }

        UpdateFog();
    }

    // Called by FieldOfViewManager at the end of each UpdateFogOfWar().
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

    // <summary>
    // Instantly tears down ALL fog geometry and particles with no DOTween tweens.
    //
    // Called by DebugCheatManager.CheatRevealMap() instead of UpdateFog(), which
    // would spawn hundreds of AnimateTileRise + SpawnFadeQuad tweens in a single
    // frame and cause DOTween to flood the log with pool-expansion warnings.
    //
    // This method:
    //   1. Destroys ALL active FogFadeQuad GameObjects immediately.
    //      CRITICAL: DOTween.KillAll() (called before this in CheatRevealMap) stops
    //      their fade tweens but does NOT destroy the GameObjects — they stay in the
    //      scene at full black opacity, producing the black-patch visual glitch.
    //   2. Kills every active DOTween tween on all tile transforms (in-flight rises).
    //   3. Snaps every tile back to its resting Y so none are stuck underground.
    //   4. Destroys the procedural fog mesh immediately.
    //   5. Stops and clears the particle system immediately.
    //   6. Clears previouslyUnexplored so the next normal UpdateFog() call won't
    //      misidentify all tiles as "newly revealed" and try to animate them.
    public void RevealAllInstant()
    {
        if (GridManager.Instance == null) return;

        //    SpawnFadeQuad() relies on its DOTween OnComplete callback to call
        //    Destroy(fadeObj). When DOTween.KillAll() is called externally those
        //    callbacks never fire, leaving black quads frozen at full opacity.
        foreach (GameObject fadeQuad in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (fadeQuad != null && fadeQuad.name == "FogFadeQuad")
                Destroy(fadeQuad);

        // 2. Kill tweens + snap all tiles to their resting Y so none are buried
        foreach (HexTile tile in GridManager.Instance.GetAllTiles())
        {
            if (tile == null) continue;

            tile.transform.DOKill();

            if (tileRestingY.TryGetValue(tile, out float restY))
            {
                Vector3 pos = tile.transform.position;
                pos.y = restY;
                tile.transform.position = pos;
            }
        }

        // 3. Destroy the fog mesh immediately — no fade
        if (meshFilter.sharedMesh != null)
        {
            Destroy(meshFilter.sharedMesh);
            meshFilter.mesh = null;
        }

        // 4. Stop and clear the particle system immediately
        if (fogParticleSystem != null)
        {
            fogParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 5. Clear the "previously unexplored" set so UpdateFog() won't try to
        //    animate every tile as newly-revealed the next time it runs normally.
        previouslyUnexplored.Clear();

        // Reveal all tile materials and re-activate all child objects instantly.
        if (HexTileReveal.Instance != null)
            HexTileReveal.Instance.RevealAllTiles();

        Debug.Log("[HexFogRenderer] RevealAllInstant: fog mesh, particles, and fade quads cleared.");
    }

    // Handles both the fade quad dissolve and the tile rise animation
    // for all tiles revealed in this update, with ripple stagger.
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

            // 1. Fade quad dissolve
            SpawnFadeQuad(tile, stagger);

            // 2. Tile rise
            AnimateTileRise(tile, stagger);

            // 3. Swap material ToonLit_Hidden → ToonLit and re-activate all
            //    child objects (buildings, nature props) that were hidden at spawn.
            if (HexTileReveal.Instance != null)
                HexTileReveal.Instance.RevealTile(tile);
        }
    }

    // Instantly drops the tile below its resting Y, then DOTweens it back up
    // with an OutBack ease for a satisfying overshoot land.
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

    // Spawns a flat quad at the tile's position at fog height,
    // then DOTweens its alpha 1 to 0 with a smooth InOutSine curve.
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