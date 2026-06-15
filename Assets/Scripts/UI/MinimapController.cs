using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Civ-style 2D Minimap — generates a texture from tile data.
///
/// IMPROVEMENTS over original:
///   - Dirty-flag repainting: only redraws when fog/influence changes (call MarkDirty())
///   - Correct circular mask formula (cx*cx not cx*cy)
///   - Fog border dithering for soft explored-edge transitions
///   - Fogged neighbor guard on influence borders (no ghost borders in shroud)
///   - Coroutine init instead of fragile Invoke delay
///   - Unit dots with per-player coloring + fog respect
///   - HQ star markers
///   - Built tower dots
///   - Topographic noise baked into land for depth
///   - Scanline overlay for tactical map feel
///   - Vignette darkening toward circle edge
///   - Decorative outer ring drawn around the circle
///   - Click guard: ignores clicks outside the circular map
///   - WorldToPx/WorldToPy helpers (eliminate duplicate math)
/// </summary>
public class MinimapController : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    public static MinimapController Instance;

    [Header("Texture")]
    public int textureSize = 300;

    [Header("Colors — Land")]
    public Color32 landVisible       = new Color32(118, 122, 74,  255);
    public Color32 landFogged        = new Color32(38,  40,  28,  255);
    public Color32 landHighTopo      = new Color32(148, 152, 94,  255); // topo highlight
    public Color32 landShadowTopo    = new Color32(88,  92,  54,  255); // topo shadow

    [Header("Colors — Water")]
    public Color32 waterVisible      = new Color32(28,  58,  118, 255);
    public Color32 waterShallow      = new Color32(38,  78,  140, 255); // near-shore tint
    public Color32 waterFogged       = new Color32(10,  18,  42,  255);

    [Header("Colors — Territory")]
    public Color32 playerColor       = new Color32(0,   200, 255, 255);
    public Color32 enemyColor        = new Color32(220, 60,  60,  255);
    public Color32 borderPixelColor  = new Color32(0,   240, 255, 255);

    [Header("Colors — Units & Structures")]
    public Color32 playerUnitColor   = new Color32(0,   255, 200, 255);
    public Color32 enemyUnitColor    = new Color32(255, 80,  80,  255);
    public Color32 hqColor           = new Color32(255, 240, 80,  255);
    public Color32 towerColor        = new Color32(180, 220, 255, 255);

    [Header("Shape")]
    [Tooltip("Number of sides for the minimap polygon mask.\n"
           + "Higher = rounder. 16 gives a low-poly circle look.\n"
           + "Set to 0 to use a perfect circle instead.")]
    [Range(3, 64)]
    public int polygonSides = 16;

    [Header("Visual Style")]
    [Tooltip("Alpha 0..1 of scanline overlay — adds a CRT/tactical-screen feel.")]
    [Range(0f, 0.4f)]
    public float scanlineAlpha       = 0.12f;
    [Tooltip("Strength of the vignette darkening toward the circle edge. 0 = off.")]
    [Range(0f, 1f)]
    public float vignetteStrength    = 0.55f;
    [Tooltip("How many pixels wide the dithered fog-edge transition is.")]
    [Range(0, 8)]
    public int   fogDitherWidth      = 4;

    [Header("Viewport Indicator")]
    public Color trapezoidColor      = new Color(1f, 1f, 1f, 0.85f);
    public float lineWidth           = 2f;

    // ─────────────────────────────────────────────────────────────────────
    //  Runtime state
    // ─────────────────────────────────────────────────────────────────────
    private RectTransform  myRect;
    private Texture2D      mapTex;
    private Color32[]      pixels;
    private RawImage       mapImage;
    private ViewportTrapezoid viewport;
    private Bounds         cityBounds;
    private bool           _isDirty   = true;
    private bool           _inited    = false;

    private struct TileEntry
    {
        public HexTile tile;
        public int     px, py;
    }
    private List<TileEntry> tileEntries = new List<TileEntry>();
    private int   tileRadius;
    private float pixelScale;
    private float offsetX, offsetY;

    // Baked noise offsets for topographic texture — generated once in Init
    private float[] topoNoise;

    private Color32 shroud = new Color32(4, 5, 6, 255);

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        myRect   = GetComponent<RectTransform>();
    }

    private void Start()
    {
        StartCoroutine(InitWhenReady());
    }

    private IEnumerator InitWhenReady()
    {
        // Wait for GridManager to be fully ready instead of fragile Invoke delay
        while (GridManager.Instance == null || !GridManager.Instance.IsReady)
            yield return null;
        // One extra frame for everything else to settle
        yield return null;
        Init();
    }

    private void Init()
    {
        ComputeBounds();
        BuildMapping();
        BakeTopoNoise();
        CreateTexture();
        SetupUI();
        CreateViewport();
        _inited  = true;
        _isDirty = true;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Public dirty flag — call this whenever fog/influence changes
    // ─────────────────────────────────────────────────────────────────────
    public void MarkDirty() => _isDirty = true;

    // ─────────────────────────────────────────────────────────────────────
    //  BOUNDS
    // ─────────────────────────────────────────────────────────────────────
    private void ComputeBounds()
    {
        if (GridManager.Instance == null || GridManager.Instance.tiles.Count == 0)
        {
            cityBounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            return;
        }
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (HexTile t in GridManager.Instance.tiles.Values)
        {
            Vector3 p = t.transform.position;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }
        float pad = GridManager.Instance.hexSize + 1f;
        minX -= pad; minZ -= pad;
        maxX += pad; maxZ += pad;
        cityBounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f),
            new Vector3(maxX - minX, 0f, maxZ - minZ));
    }

    // ─────────────────────────────────────────────────────────────────────
    //  TILE → PIXEL MAPPING
    // ─────────────────────────────────────────────────────────────────────
    private void BuildMapping()
    {
        float scaleX = (textureSize - 6f) / cityBounds.size.x;
        float scaleZ = (textureSize - 6f) / cityBounds.size.z;
        pixelScale   = Mathf.Min(scaleX, scaleZ);
        float usedW  = cityBounds.size.x * pixelScale;
        float usedH  = cityBounds.size.z * pixelScale;
        offsetX      = (textureSize - usedW) * 0.5f;
        offsetY      = (textureSize - usedH) * 0.5f;
        tileRadius   = Mathf.Max(1, Mathf.RoundToInt(GridManager.Instance.hexSize * pixelScale * 0.7f));

        foreach (HexTile tile in GridManager.Instance.tiles.Values)
        {
            Vector3 pos = tile.transform.position;
            int px = WorldToPx(pos.x);
            int py = WorldToPy(pos.z);
            px = Mathf.Clamp(px, tileRadius, textureSize - tileRadius - 1);
            py = Mathf.Clamp(py, tileRadius, textureSize - tileRadius - 1);
            tileEntries.Add(new TileEntry { tile = tile, px = px, py = py });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  TOPOGRAPHIC NOISE (baked once — avoids per-frame Perlin sampling)
    // ─────────────────────────────────────────────────────────────────────
    private void BakeTopoNoise()
    {
        topoNoise = new float[tileEntries.Count];
        float seed = Random.Range(0f, 100f);
        for (int i = 0; i < tileEntries.Count; i++)
        {
            Vector3 pos = tileEntries[i].tile.transform.position;
            // Two octaves of Perlin for organic topo lines
            float n = Mathf.PerlinNoise(pos.x * 0.08f + seed, pos.z * 0.08f + seed)
                    + Mathf.PerlinNoise(pos.x * 0.22f + seed, pos.z * 0.22f + seed) * 0.4f;
            n /= 1.4f; // normalize back to ~0..1
            topoNoise[i] = n;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  TEXTURE SETUP
    // ─────────────────────────────────────────────────────────────────────
    private void CreateTexture()
    {
        mapTex            = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        mapTex.filterMode = FilterMode.Bilinear;
        mapTex.wrapMode   = TextureWrapMode.Clamp;
        pixels            = new Color32[textureSize * textureSize];
    }

    // ─────────────────────────────────────────────────────────────────────
    //  UI SETUP
    // ─────────────────────────────────────────────────────────────────────
    private void SetupUI()
    {
        if (myRect == null) return;
        mapImage = GetComponentInChildren<RawImage>();
        if (mapImage == null)
        {
            GameObject go = new GameObject("MinimapTexture");
            go.layer = gameObject.layer;
            go.transform.SetParent(myRect, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            mapImage = go.AddComponent<RawImage>();
        }
        mapImage.texture       = mapTex;
        mapImage.raycastTarget = false;

        Image bg = GetComponent<Image>();
        if (bg != null) bg.color = new Color(0, 0, 0, 0.01f);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  VIEWPORT TRAPEZOID
    // ─────────────────────────────────────────────────────────────────────
    private void CreateViewport()
    {
        GameObject go = new GameObject("ViewportTrapezoid");
        go.layer = gameObject.layer;
        go.transform.SetParent(myRect, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
        rt.pivot      = new Vector2(0.5f, 0.5f);
        viewport              = go.AddComponent<ViewportTrapezoid>();
        viewport.color        = trapezoidColor;
        viewport.lineWidth    = lineWidth;
        viewport.raycastTarget = false;
        go.transform.SetAsLastSibling();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  LATE UPDATE
    // ─────────────────────────────────────────────────────────────────────
    private void LateUpdate()
    {
        if (!_inited || mapTex == null) return;

        // Units move every turn, so mark dirty each frame for their dots
        // (cheap dirty: only the unit/tower pass, rest is cached via flag)
        _isDirty = true; // units/towers always need refresh; remove this line
                         // once you wire MarkDirty() to FOV + Influence events
                         // for a proper cached-terrain approach

        if (_isDirty)
        {
            PaintMap();
            _isDirty = false;
        }

        UpdateViewportCorners();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PAINT MAP
    // ─────────────────────────────────────────────────────────────────────
    private void PaintMap()
    {
        float cx  = textureSize * 0.5f;
        float cy  = textureSize * 0.5f;
        float rSq = cx * cx; 

        // ── 1. Clear: shroud inside circle, transparent outside ────────────
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = x - cx, dy = y - cy;
                pixels[y * textureSize + x] = (dx * dx + dy * dy) <= rSq
                    ? shroud
                    : new Color32(0, 0, 0, 0);
            }
        }

        // ── 2. Sea bleed: wider circle around explored tiles ───────────────
        int seaRadius = tileRadius + Mathf.Max(2, Mathf.RoundToInt(tileRadius * 0.8f));
        for (int i = 0; i < tileEntries.Count; i++)
        {
            TileEntry e = tileEntries[i];
            if (e.tile == null || !e.tile.isExplored) continue;
            Color32 seaCol = e.tile.isVisible ? waterShallow : waterFogged;
            FillCircle(e.px, e.py, seaRadius, seaCol);
        }

        // ── 3. Terrain pass with topographic tinting ──────────────────────
        for (int i = 0; i < tileEntries.Count; i++)
        {
            TileEntry e = tileEntries[i];
            if (e.tile == null || !e.tile.isExplored) continue;
            Color32 col = GetTileColor(e.tile, i);
            FillCircle(e.px, e.py, tileRadius, col);
        }

        // ── 4. Fog dither edge (soft transition at explored border) ────────
        if (fogDitherWidth > 0)
            DrawFogDitherEdge();

        // ── 5. Influence borders ───────────────────────────────────────────
        DrawInfluenceBorders();

        // ── 6. Structures (towers + HQs) ──────────────────────────────────
        PaintStructures();

        // ── 7. Units ──────────────────────────────────────────────────────
        PaintUnits();

        // ── 8. Scanline overlay ────────────────────────────────────────────
        if (scanlineAlpha > 0f)
            DrawScanlines();

        // ── 9. Vignette darkening toward circle edge ───────────────────────
        if (vignetteStrength > 0f)
            DrawVignette(cx, cy, rSq);

        // ── 10. Decorative outer ring ──────────────────────────────────────
        DrawOuterRing(cx, cy, rSq);

        mapTex.SetPixelData(pixels, 0);
        mapTex.Apply(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  TILE COLOR with topo noise
    // ─────────────────────────────────────────────────────────────────────
    private Color32 GetTileColor(HexTile tile, int entryIndex)
    {
        bool isWater = tile.type == HexTile.TileType.Water;
        if (!tile.isExplored) return shroud;

        if (!tile.isVisible)
            return isWater ? waterFogged : landFogged;

        // Topographic tint on land tiles only
        Color32 baseCol;
        if (isWater)
        {
            baseCol = waterVisible;
        }
        else
        {
            float n = (topoNoise != null && entryIndex < topoNoise.Length) ? topoNoise[entryIndex] : 0.5f;
            // Topo lines: light band above 0.65, shadow band below 0.35
            if      (n > 0.68f) baseCol = landHighTopo;
            else if (n < 0.32f) baseCol = landShadowTopo;
            else                baseCol = landVisible;
        }

        // Territory tint
        PlayerData dominant = null;
        int maxInf = 0;
        foreach (var kvp in tile.influenceByPlayer)
            if (kvp.Value > maxInf) { maxInf = kvp.Value; dominant = kvp.Key; }

        if (dominant != null && maxInf > 0)
        {
            Color32 tint = dominant.isAI ? enemyColor : playerColor;
            baseCol = LerpColor32(baseCol, tint, 0.28f);
        }

        return baseCol;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  FOG DITHER EDGE
    // ─────────────────────────────────────────────────────────────────────
    private void DrawFogDitherEdge()
    {
        // For each explored tile, check if any neighbor is unexplored.
        // If so, dot a dithered transition around this tile.
        for (int i = 0; i < tileEntries.Count; i++)
        {
            TileEntry e = tileEntries[i];
            if (e.tile == null || !e.tile.isExplored) continue;

            bool hasFogNeighbor = false;
            foreach (HexTile n in GridManager.Instance.GetNeighbors(e.tile))
            {
                if (n != null && !n.isExplored) { hasFogNeighbor = true; break; }
            }
            if (!hasFogNeighbor) continue;

            // Draw dithered pixels in a ring around this tile
            int r = tileRadius + fogDitherWidth;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int dist2 = dx * dx + dy * dy;
                    int inner = tileRadius * tileRadius;
                    int outer = r * r;
                    if (dist2 <= inner || dist2 > outer) continue;

                    // Dither pattern: use (x+y) parity for a diagonal screen-door effect
                    int px = e.px + dx;
                    int py = e.py + dy;
                    if (px < 0 || px >= textureSize || py < 0 || py >= textureSize) continue;

                    float t = Mathf.InverseLerp(inner, outer, dist2);
                    // Only draw if dither threshold passes
                    bool draw = ((px + py) % 2 == 0) ? (t < 0.7f) : (t < 0.35f);
                    if (draw)
                    {
                        Color32 existing = pixels[py * textureSize + px];
                        pixels[py * textureSize + px] = LerpColor32(existing, shroud, t * 0.8f);
                    }
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  INFLUENCE BORDERS
    // ─────────────────────────────────────────────────────────────────────
    private void DrawInfluenceBorders()
    {
        for (int i = 0; i < tileEntries.Count; i++)
        {
            TileEntry e = tileEntries[i];
            if (e.tile == null || !e.tile.isExplored) continue;

            PlayerData owner = null;
            int maxInf = 0;
            foreach (var kvp in e.tile.influenceByPlayer)
                if (kvp.Value > maxInf) { maxInf = kvp.Value; owner = kvp.Key; }
            if (owner == null || maxInf <= 0) continue;

            foreach (HexTile n in GridManager.Instance.GetNeighbors(e.tile))
            {
                // don't draw borders into unexplored/fogged tiles
                if (n == null || !n.isExplored) continue;

                PlayerData nOwner = null;
                int nMax = 0;
                foreach (var kvp in n.influenceByPlayer)
                    if (kvp.Value > nMax) { nMax = kvp.Value; nOwner = kvp.Key; }

                if (nOwner == owner) continue;

                // Midpoint between tiles
                int bx = Mathf.RoundToInt(((e.tile.transform.position.x + n.transform.position.x) * 0.5f - cityBounds.min.x) * pixelScale + offsetX);
                int by = Mathf.RoundToInt(((e.tile.transform.position.z + n.transform.position.z) * 0.5f - cityBounds.min.z) * pixelScale + offsetY);

                Color32 bCol = owner.isAI ? enemyColor : borderPixelColor;
                // 2-pixel wide border for visibility
                for (int bdy = -1; bdy <= 1; bdy++)
                    for (int bdx = -1; bdx <= 1; bdx++)
                        SetPixelSafe(bx + bdx, by + bdy, bCol);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  STRUCTURES (HQs + Towers)
    // ─────────────────────────────────────────────────────────────────────
    private void PaintStructures()
    {
        if (GameManager.Instance?.players == null) return;

        foreach (PlayerData p in GameManager.Instance.players)
        {
            if (p == null) continue;
            // HQs — draw a small cross/star marker in gold
            foreach (SignalNode hq in p.ownedNodes)
            {
                if (hq == null) continue;
                Vector3 pos = hq.tile != null ? hq.tile.transform.position : hq.transform.position;
                if (!IsTileVisibleOrOwned(pos, p)) continue;
                int px = WorldToPx(pos.x);
                int py = WorldToPy(pos.z);
                DrawCross(px, py, 3, hqColor);
            }
        }

        // Built towers — small 2px colored square
        if (TurnManager.Instance == null) return;
        foreach (TowerNode t in TurnManager.Instance.GetAllTowers())
        {
            if (t == null || t.tile == null) continue;
            if (!t.tile.isVisible && (t.owner?.isAI ?? false)) continue;
            if (!t.tile.isExplored) continue;
            int px = WorldToPx(t.tile.transform.position.x);
            int py = WorldToPy(t.tile.transform.position.z);
            Color32 col = (t.owner?.isAI ?? false) ? enemyColor : towerColor;
            // Slightly different dot sizes per state
            int r = t.IsBuilt() ? 2 : 1;
            FillCircle(px, py, r, col);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  UNITS
    // ─────────────────────────────────────────────────────────────────────
    private void PaintUnits()
    {
        if (TurnManager.Instance == null) return;

        foreach (Unit u in TurnManager.Instance.GetAllUnits())
        {
            if (u == null || u.currentTile == null) continue;
            // Respect fog — don't reveal enemy units in unexplored tiles
            if (!u.currentTile.isExplored) continue;
            if (!u.currentTile.isVisible && (u.owner?.isAI ?? false)) continue;

            int px = WorldToPx(u.currentTile.transform.position.x);
            int py = WorldToPy(u.currentTile.transform.position.z);

            bool isEnemy = u.owner?.isAI ?? false;
            Color32 col  = isEnemy ? enemyUnitColor : playerUnitColor;

            // Soft halo (1px lighter ring then solid center)
            Color32 halo = LerpColor32(col, new Color32(255, 255, 255, 255), 0.4f);
            FillCircle(px, py, 3, halo);
            FillCircle(px, py, 2, col);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  SCANLINES
    // ─────────────────────────────────────────────────────────────────────
    private void DrawScanlines()
    {
        byte alpha = (byte)(scanlineAlpha * 255f);
        Color32 line = new Color32(0, 0, 0, alpha);
        for (int y = 0; y < textureSize; y += 2) // every other row
        {
            for (int x = 0; x < textureSize; x++)
            {
                int idx = y * textureSize + x;
                Color32 existing = pixels[idx];
                if (existing.a == 0) continue; // skip transparent (outside circle)
                pixels[idx] = LerpColor32(existing, new Color32(0, 0, 0, 255), scanlineAlpha);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  VIGNETTE
    // ─────────────────────────────────────────────────────────────────────
    private void DrawVignette(float cx, float cy, float rSq)
    {
        float r = Mathf.Sqrt(rSq);
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                int idx = y * textureSize + x;
                if (pixels[idx].a == 0) continue;

                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01((dist / r - 0.55f) / 0.45f); // starts at 55% radius
                float dark = t * t * vignetteStrength;
                if (dark <= 0f) continue;
                pixels[idx] = LerpColor32(pixels[idx], new Color32(0, 0, 0, 255), dark);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  DECORATIVE OUTER RING
    // ─────────────────────────────────────────────────────────────────────
    private void DrawOuterRing(float cx, float cy, float rSq)
    {
        float r = Mathf.Sqrt(rSq);
        // Two concentric rings: a thin bright inner ring and a 1px dark outer ring
        Color32 ringInner = new Color32(60, 90, 80, 220);
        Color32 ringOuter = new Color32(20, 30, 25, 180);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);
                if (d >= r - 2f && d < r)
                    pixels[y * textureSize + x] = ringInner;
                else if (d >= r && d < r + 1.5f)
                    pixels[y * textureSize + x] = ringOuter;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  VIEWPORT
    // ─────────────────────────────────────────────────────────────────────
    private void UpdateViewportCorners()
    {
        if (viewport == null || Camera.main == null || myRect == null) return;

        Camera  cam     = Camera.main;
        Vector3 camPos  = cam.transform.position;

        Vector3 flatForward = cam.transform.forward;
        flatForward.y = 0;
        if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        float fixedPitch  = 60f * Mathf.Deg2Rad;
        float distForward = camPos.y / Mathf.Tan(fixedPitch);
        Vector3 focusPoint = camPos + flatForward * distForward;
        Vector2 uiCenter   = WorldToUI(focusPoint);

        float zoomFactor = Mathf.Clamp(camPos.y / 35f, 0.25f, 1.5f);
        float w  = 70f * zoomFactor;
        float h  = 45f * zoomFactor;
        float tw = w * 0.6f;

        Vector2 uBL = uiCenter + new Vector2(-w  * 0.5f, -h * 0.5f);
        Vector2 uBR = uiCenter + new Vector2( w  * 0.5f, -h * 0.5f);
        Vector2 uTL = uiCenter + new Vector2(-tw * 0.5f,  h * 0.5f);
        Vector2 uTR = uiCenter + new Vector2( tw * 0.5f,  h * 0.5f);

        // pass the circle clip radius so ViewportTrapezoid can clip each
        // line segment to the circular boundary before drawing it.
        // The circle in UI space is centred on the pivot (0,0) with radius = half the rect width.
        viewport.circleRadius = myRect.rect.width * 0.5f;
        viewport.SetCorners(uBL, uBR, uTL, uTR);
    }

    private Vector2 WorldToUI(Vector3 worldPos)
    {
        float nx = Mathf.InverseLerp(cityBounds.min.x, cityBounds.max.x, worldPos.x);
        float nz = Mathf.InverseLerp(cityBounds.min.z, cityBounds.max.z, worldPos.z);
        float w  = myRect.rect.width;
        float h  = myRect.rect.height;
        return new Vector2((nx - 0.5f) * w, (nz - 0.5f) * h);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  CLICK / DRAG
    // ─────────────────────────────────────────────────────────────────────
    public void OnPointerClick(PointerEventData e) { NavigateTo(e); }
    public void OnDrag(PointerEventData e)         { NavigateTo(e); }

    private void NavigateTo(PointerEventData e)
    {
        if (myRect == null || CameraController.Instance == null) return;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                myRect, e.position, e.pressEventCamera, out local)) return;

        // Ignore clicks outside the polygon minimap boundary.
        // normX/normY are in [-1, 1] space — pass radius=1 to IsInsidePolygon.
        float halfW = myRect.rect.width  * 0.5f;
        float halfH = myRect.rect.height * 0.5f;
        float normX = local.x / halfW;
        float normY = local.y / halfH;
        if (!IsInsidePolygon(normX, normY, 1f, polygonSides)) return;

        float nx = (local.x - myRect.rect.xMin) / myRect.rect.width;
        float nz = (local.y - myRect.rect.yMin) / myRect.rect.height;
        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);
        float wx = Mathf.Lerp(cityBounds.min.x, cityBounds.max.x, nx);
        float wz = Mathf.Lerp(cityBounds.min.z, cityBounds.max.z, nz);
        Vector3 camPos = CameraController.Instance.transform.position;
        CameraController.Instance.transform.position = new Vector3(wx, camPos.y, wz);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  COORDINATE HELPERS
    // ─────────────────────────────────────────────────────────────────────
    private int WorldToPx(float worldX) =>
        Mathf.Clamp(Mathf.RoundToInt((worldX - cityBounds.min.x) * pixelScale + offsetX),
                    0, textureSize - 1);

    private int WorldToPy(float worldZ) =>
        Mathf.Clamp(Mathf.RoundToInt((worldZ - cityBounds.min.z) * pixelScale + offsetY),
                    0, textureSize - 1);

    private bool IsTileVisibleOrOwned(Vector3 worldPos, PlayerData player)
    {
        int px = WorldToPx(worldPos.x);
        int py = WorldToPy(worldPos.z);
        // Find nearest tile entry to check visibility
        foreach (TileEntry e in tileEntries)
        {
            if (Mathf.Abs(e.px - px) <= tileRadius && Mathf.Abs(e.py - py) <= tileRadius)
                return e.tile.isExplored || !player.isAI;
        }
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PIXEL HELPERS
    // ─────────────────────────────────────────────────────────────────────
    private void FillCircle(int cx, int cy, int r, Color32 col)
    {
        int r2 = r * r;
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (dx * dx + dy * dy <= r2)
                    SetPixelSafe(cx + dx, cy + dy, col);
    }

    /// <summary>Draws a + cross marker — used for HQ icons.</summary>
    private void DrawCross(int cx, int cy, int halfLen, Color32 col)
    {
        // Horizontal arm
        for (int dx = -halfLen; dx <= halfLen; dx++)
            SetPixelSafe(cx + dx, cy,     col);
        // Vertical arm
        for (int dy = -halfLen; dy <= halfLen; dy++)
            SetPixelSafe(cx,     cy + dy, col);
        // Thicken center dot
        SetPixelSafe(cx - 1, cy,     col);
        SetPixelSafe(cx + 1, cy,     col);
        SetPixelSafe(cx,     cy - 1, col);
        SetPixelSafe(cx,     cy + 1, col);
    }

    private void SetPixelSafe(int x, int y, Color32 col)
    {
        if (x < 0 || x >= textureSize || y < 0 || y >= textureSize) return;
        float cx = textureSize * 0.5f;
        float cy = textureSize * 0.5f;
        float dx = x - cx, dy = y - cy;
        float radius = cx; // use half the texture size as the bounding radius

        if (!IsInsidePolygon(dx, dy, radius, polygonSides)) return;
        pixels[y * textureSize + x] = col;
    }

    /// <summary>
    /// Returns true if point (dx, dy) — relative to the polygon centre — lies
    /// inside a regular N-sided polygon with the given circumradius.
    ///
    /// The polygon is oriented so one flat edge sits at the top (vertices are
    /// offset by half a sector so no vertex points straight up/down).
    ///
    /// When sides &lt;= 0 the test falls back to a perfect circle.
    /// </summary>
    private static bool IsInsidePolygon(float dx, float dy, float radius, int sides)
    {
        if (sides <= 0)
        {
            // Fallback: perfect circle
            return dx * dx + dy * dy <= radius * radius;
        }

        float dist  = Mathf.Sqrt(dx * dx + dy * dy);
        if (dist <= 0.0001f) return true; // centre point always inside

        // Angle of this pixel from the centre, offset by half a sector so the
        // polygon sits flat-edge-up rather than vertex-up.
        float sectorAngle = Mathf.PI * 2f / sides;
        float halfSector  = sectorAngle * 0.5f;
        float angle       = Mathf.Atan2(dy, dx) + halfSector;

        // Normalise angle into the range [0, sectorAngle) to find position
        // within the current sector.
        float sectorFrac     = angle / sectorAngle;
        float angleInSector  = (sectorFrac - Mathf.Floor(sectorFrac) - 0.5f) * sectorAngle;

        // Distance from centre to the polygon edge at this angle.
        // Derived from the apothem (inradius): apothem = radius * cos(π/N)
        // At angle θ within a sector the edge distance is apothem / cos(θ).
        float edgeDist = radius * Mathf.Cos(halfSector) / Mathf.Cos(angleInSector);

        return dist <= edgeDist;
    }

    private static Color32 LerpColor32(Color32 a, Color32 b, float t)
    {
        t = Mathf.Clamp01(t);
        return new Color32(
            (byte)(a.r + (b.r - a.r) * t),
            (byte)(a.g + (b.g - a.g) * t),
            (byte)(a.b + (b.b - a.b) * t),
            255);
    }

    private void OnDestroy()
    {
        if (mapTex != null) Destroy(mapTex);
    }
}

// =========================================================================
//  ViewportTrapezoid — custom UI Graphic for the camera viewport indicator
//  each line segment is clipped to the circular minimap boundary before
//  drawing, so the trapezoid never renders outside the circle.
// =========================================================================
[RequireComponent(typeof(CanvasRenderer))]
public class ViewportTrapezoid : Graphic
{
    public float lineWidth   = 2f;
    // Radius of the circular clip region in UI (local rect) space.
    // Set this from MinimapController.UpdateViewportCorners() each frame.
    // 0 = no clipping (legacy behaviour).
    public float circleRadius = 0f;

    private Vector2[] corners = new Vector2[4]; // BL, BR, TL, TR

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (corners == null || corners.Length < 4) return;
        AddLine(vh, corners[0], corners[1]); // BL → BR
        AddLine(vh, corners[1], corners[3]); // BR → TR
        AddLine(vh, corners[3], corners[2]); // TR → TL
        AddLine(vh, corners[2], corners[0]); // TL → BL
    }

    // Draws a line from a to b, clipped so neither endpoint lies outside the
    // circle of radius circleRadius centred on the origin (rect pivot = 0.5,0.5).
    //
    // Algorithm — line-circle intersection:
    //   P(t) = a + t*(b-a),  find t where |P(t)|² = r²
    //   Solve quadratic: |d|²t² + 2(a·d)t + |a|²-r² = 0
    //   Keep only the t∈[0,1] roots that are closer to the original endpoints.
    private void AddLine(VertexHelper vh, Vector2 a, Vector2 b)
    {
        // Clip segment endpoints to circle if radius is set
        if (circleRadius > 0f)
        {
            if (!ClipSegmentToCircle(ref a, ref b, circleRadius))
                return; // entire segment is outside the circle — skip
        }

        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();
        Vector2 perp = new Vector2(-dir.y, dir.x) * lineWidth * 0.5f;

        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(a.x + perp.x, a.y + perp.y), color, Vector2.zero);
        vh.AddVert(new Vector3(a.x - perp.x, a.y - perp.y), color, Vector2.zero);
        vh.AddVert(new Vector3(b.x - perp.x, b.y - perp.y), color, Vector2.zero);
        vh.AddVert(new Vector3(b.x + perp.x, b.y + perp.y), color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    // Clips the segment [a,b] to the circle |p| ≤ r.
    // Modifies a and b in-place to the clipped endpoints.
    // Returns false if the segment lies entirely outside the circle.
    private static bool ClipSegmentToCircle(ref Vector2 a, ref Vector2 b, float r)
    {
        Vector2 d  = b - a;
        float   A  = Vector2.Dot(d, d);
        float   B  = 2f * Vector2.Dot(a, d);
        float   C  = Vector2.Dot(a, a) - r * r;
        float   disc = B * B - 4f * A * C;

        bool aInside = Vector2.Dot(a, a) <= r * r;
        bool bInside = Vector2.Dot(b, b) <= r * r;

        if (aInside && bInside) return true;   // both inside — no clip needed
        if (disc < 0f)          return false;  // no intersection — fully outside

        float sqrtDisc = Mathf.Sqrt(disc);
        float t0 = (-B - sqrtDisc) / (2f * A);
        float t1 = (-B + sqrtDisc) / (2f * A);

        // Ensure t0 <= t1
        if (t0 > t1) { float tmp = t0; t0 = t1; t1 = tmp; }

        // Segment is outside the circle's chord
        if (t1 < 0f || t0 > 1f) return false;

        // Clamp to [0,1] — keep the portion of the segment that's inside
        float tStart = Mathf.Max(t0, 0f);
        float tEnd   = Mathf.Min(t1, 1f);

        // Only clip endpoints that are outside
        if (!aInside) a = a + d * tStart;
        if (!bInside) b = a + d * (tEnd - tStart); // recalculate from new a

        // Final safety: clamp both points to circle edge if still slightly outside
        if (Vector2.Dot(a, a) > r * r) a = a.normalized * r;
        if (Vector2.Dot(b, b) > r * r) b = b.normalized * r;

        return true;
    }

    public void SetCorners(Vector2 bl, Vector2 br, Vector2 tl, Vector2 tr)
    {
        corners[0] = bl; corners[1] = br;
        corners[2] = tl; corners[3] = tr;
        SetVerticesDirty();
    }
}