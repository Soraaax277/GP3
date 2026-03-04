using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// Civ-style 2D Minimap — generates a static texture from tile data.
/// Only shows terrain colors, fog of war, and influence borders.
/// No 3D camera means no holograms, range indicators, or buildings.
/// Attach to the MiniMap_Area UI object.
public class MinimapController : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    public static MinimapController Instance;

    [Header("Texture")]
    public int textureSize = 300;

    [Header("Colors — Land")]
    public Color32 landVisible  = new Color32(130, 135, 85, 255);
    public Color32 landFogged   = new Color32(45, 45, 35, 255);

    [Header("Colors — Water")]
    public Color32 waterVisible = new Color32(30, 55, 120, 255);
    public Color32 waterFogged  = new Color32(12, 20, 45, 255);

    [Header("Colors — Territory")]
    public Color32 playerColor  = new Color32(0, 200, 255, 255);
    public Color32 enemyColor   = new Color32(220, 50, 50, 255);
    public Color32 borderPixelColor = new Color32(0, 255, 255, 255);

    [Header("Viewport Indicator")]
    public Color trapezoidColor = Color.white;
    public float lineWidth = 2f;

    // Runtime
    private RectTransform myRect;
    private Texture2D mapTex;
    private Color32[] pixels;
    private RawImage mapImage;
    private ViewportTrapezoid viewport;
    private Bounds cityBounds;

    // Tile to pixel mapping
    private struct TileEntry
    {
        public HexTile tile;
        public int px, py;
    }
    private List<TileEntry> tileEntries = new List<TileEntry>();
    private int tileRadius = 2;
    private float pixelScale; // pixels per world unit
    private float offsetX, offsetY;

    // Shroud color (matches background)
    private Color32 shroud = new Color32(5, 5, 8, 255);

    private void Awake()
    {
        Instance = this;
        myRect = GetComponent<RectTransform>();
    }

    private void Start()
    {
        Invoke(nameof(Init), 0.3f);
    }

    private void Init()
    {
        ComputeBounds();
        BuildMapping();
        CreateTexture();
        SetupUI();
        CreateViewport();
    }

    // =====================================================================
    //  BOUNDS
    // =====================================================================
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

    // =====================================================================
    //  TILE → PIXEL MAPPING
    // =====================================================================
    private void BuildMapping()
    {
        float scaleX = (textureSize - 6f) / cityBounds.size.x;
        float scaleZ = (textureSize - 6f) / cityBounds.size.z;
        pixelScale = Mathf.Min(scaleX, scaleZ);

        float usedW = cityBounds.size.x * pixelScale;
        float usedH = cityBounds.size.z * pixelScale;
        offsetX = (textureSize - usedW) * 0.5f;
        offsetY = (textureSize - usedH) * 0.5f;

        tileRadius = Mathf.Max(1, Mathf.RoundToInt(GridManager.Instance.hexSize * pixelScale * 0.7f));

        foreach (HexTile tile in GridManager.Instance.tiles.Values)
        {
            Vector3 pos = tile.transform.position;
            int px = Mathf.RoundToInt((pos.x - cityBounds.min.x) * pixelScale + offsetX);
            int py = Mathf.RoundToInt((pos.z - cityBounds.min.z) * pixelScale + offsetY);
            px = Mathf.Clamp(px, tileRadius, textureSize - tileRadius - 1);
            py = Mathf.Clamp(py, tileRadius, textureSize - tileRadius - 1);

            tileEntries.Add(new TileEntry { tile = tile, px = px, py = py });
        }
    }

    // =====================================================================
    //  TEXTURE
    // =====================================================================
    private void CreateTexture()
    {
        mapTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        mapTex.filterMode = FilterMode.Bilinear;
        mapTex.wrapMode = TextureWrapMode.Clamp;
        pixels = new Color32[textureSize * textureSize];
    }

    // =====================================================================
    //  UI
    // =====================================================================
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

        mapImage.texture = mapTex;
        mapImage.raycastTarget = false;

        // Keep the Image component for IPointerClickHandler raycast
        Image bg = GetComponent<Image>();
        if (bg != null) bg.color = new Color(0, 0, 0, 0.01f);
    }

    // =====================================================================
    //  VIEWPORT TRAPEZOID (custom UI Graphic)
    // =====================================================================
    private void CreateViewport()
    {
        GameObject go = new GameObject("ViewportTrapezoid");
        go.layer = gameObject.layer;
        go.transform.SetParent(myRect, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        viewport = go.AddComponent<ViewportTrapezoid>();
        viewport.color = trapezoidColor;
        viewport.lineWidth = lineWidth;
        viewport.raycastTarget = false;

        go.transform.SetAsLastSibling();
    }

    // =====================================================================
    //  LATE UPDATE — repaint minimap + update viewport
    // =====================================================================
    private void LateUpdate()
    {
        if (mapTex == null) return;
        PaintMap();
        UpdateViewportCorners();
    }

    private void PaintMap()
    {
        // 1. Clear to shroud (inside circle) or transparent (outside circle)
        float cx = textureSize * 0.5f;
        float cy = textureSize * 0.5f;
        float rSq = cx * cy;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                bool inside = (dx * dx + dy * dy) <= rSq;
                
                pixels[y * textureSize + x] = inside ? shroud : new Color32(0, 0, 0, 0);
            }
        }

        // 2. Base Sea pass: draw slightly larger sea circle around EXPLORED tiles
        // This makes the blue sea naturally bleed out around the edge of the discovered map.
        int seaRadius = tileRadius + Mathf.Max(2, Mathf.RoundToInt(tileRadius * 0.8f));
        for (int i = 0; i < tileEntries.Count; i++)
        {
            TileEntry e = tileEntries[i];
            if (e.tile == null || !e.tile.isExplored) continue;

            // Use fogged water color or visible water color based on tile's current visibility
            Color32 baseSeaCol = e.tile.isVisible ? waterVisible : waterFogged;
            FillCircle(e.px, e.py, seaRadius, baseSeaCol);
        }

        // 3. Tile pass: paint the actual land/water on top (only for explored tiles)
        for (int i = 0; i < tileEntries.Count; i++)
        {
            TileEntry e = tileEntries[i];
            if (e.tile == null || !e.tile.isExplored) continue;

            Color32 col = GetTileColor(e.tile);
            FillCircle(e.px, e.py, tileRadius, col);
        }

        // 3. Draw influence borders
        DrawInfluenceBorders();

        // 4. Apply
        mapTex.SetPixelData(pixels, 0);
        mapTex.Apply(false);
    }

    private Color32 GetTileColor(HexTile tile)
    {
        bool isWater = tile.type == HexTile.TileType.Water;

        if (!tile.isExplored) return shroud;

        if (!tile.isVisible)
            return isWater ? waterFogged : landFogged;

        // Visible — check territory
        Color32 baseCol = isWater ? waterVisible : landVisible;

        // Find dominant owner
        PlayerData dominant = null;
        int maxInf = 0;
        foreach (var kvp in tile.influenceByPlayer)
        {
            if (kvp.Value > maxInf) { maxInf = kvp.Value; dominant = kvp.Key; }
        }

        if (dominant != null && maxInf > 0)
        {
            // Tint with player color
            Color32 tint = dominant.isAI ? enemyColor : playerColor;
            baseCol = LerpColor32(baseCol, tint, 0.35f);
        }

        return baseCol;
    }

    private void DrawInfluenceBorders()
    {
        for (int i = 0; i < tileEntries.Count; i++)
        {
            TileEntry e = tileEntries[i];
            if (e.tile == null || !e.tile.isExplored) continue;

            // Find dominant owner of this tile
            PlayerData owner = null;
            int maxInf = 0;
            foreach (var kvp in e.tile.influenceByPlayer)
            {
                if (kvp.Value > maxInf) { maxInf = kvp.Value; owner = kvp.Key; }
            }
            if (owner == null || maxInf <= 0) continue;

            // Check neighbors — if a neighbor has a different owner or no influence, draw border
            List<HexTile> neighbors = GridManager.Instance.GetNeighbors(e.tile);
            foreach (HexTile n in neighbors)
            {
                PlayerData nOwner = null;
                int nMax = 0;
                foreach (var kvp in n.influenceByPlayer)
                {
                    if (kvp.Value > nMax) { nMax = kvp.Value; nOwner = kvp.Key; }
                }

                if (nOwner != owner)
                {
                    // Draw a border pixel halfway between the two tiles
                    Vector3 nPos = n.transform.position;
                    int bx = Mathf.RoundToInt(((e.tile.transform.position.x + nPos.x) * 0.5f - cityBounds.min.x) * pixelScale + offsetX);
                    int by = Mathf.RoundToInt(((e.tile.transform.position.z + nPos.z) * 0.5f - cityBounds.min.z) * pixelScale + offsetY);

                    Color32 bCol = owner.isAI ? enemyColor : borderPixelColor;
                    SetPixelSafe(bx, by, bCol);
                    SetPixelSafe(bx + 1, by, bCol);
                    SetPixelSafe(bx - 1, by, bCol);
                    SetPixelSafe(bx, by + 1, bCol);
                    SetPixelSafe(bx, by - 1, bCol);
                }
            }
        }
    }

    // =====================================================================
    //  VIEWPORT TRAPEZOID UPDATE
    // =====================================================================
    private void UpdateViewportCorners()
    {
        if (viewport == null || Camera.main == null || myRect == null) return;

        Camera cam = Camera.main;
        Vector3 camPos = cam.transform.position;

        // Get flat forward direction (ignoring pitch)
        Vector3 flatForward = cam.transform.forward;
        flatForward.y = 0;
        if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        // Calculate a fixed focal point based on height, IGNORING actual camera pitch
        // This stops the trapezoid from flying away when the player looks up.
        // Assume a standard 60-degree downward angle to place the center of the rectangle
        float fixedPitch = 60f * Mathf.Deg2Rad;
        float distForward = camPos.y / Mathf.Tan(fixedPitch);
        
        Vector3 focusPoint = camPos + flatForward * distForward;

        Vector2 uiCenter = WorldToUI(focusPoint);

        // Scale trapezoid based on camera height (zoom)
        float zoomFactor = Mathf.Clamp(camPos.y / 35f, 0.25f, 1.5f);
        
        float w = 70f * zoomFactor;     // Bottom width
        float h = 45f * zoomFactor;     // Height
        float tw = w * 0.6f;            // Top width

        Vector2 uBL = uiCenter + new Vector2(-w * 0.5f, -h * 0.5f);
        Vector2 uBR = uiCenter + new Vector2(w * 0.5f, -h * 0.5f);
        Vector2 uTL = uiCenter + new Vector2(-tw * 0.5f, h * 0.5f);
        Vector2 uTR = uiCenter + new Vector2(tw * 0.5f, h * 0.5f);

        viewport.SetCorners(uBL, uBR, uTL, uTR);
    }

    private Vector2 WorldToUI(Vector3 worldPos)
    {
        // World → normalized 0..1 within city bounds
        float nx = Mathf.InverseLerp(cityBounds.min.x, cityBounds.max.x, worldPos.x);
        float nz = Mathf.InverseLerp(cityBounds.min.z, cityBounds.max.z, worldPos.z);

        // Map to local rect coords (pivot-relative)
        float w = myRect.rect.width;
        float h = myRect.rect.height;
        float lx = (nx - 0.5f) * w;
        float ly = (nz - 0.5f) * h;

        return new Vector2(lx, ly);
    }

    // =====================================================================
    //  CLICK / DRAG TO NAVIGATE
    // =====================================================================
    public void OnPointerClick(PointerEventData e) { NavigateTo(e); }
    public void OnDrag(PointerEventData e)         { NavigateTo(e); }

    private void NavigateTo(PointerEventData e)
    {
        if (myRect == null || CameraController.Instance == null) return;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            myRect, e.position, e.pressEventCamera, out local))
            return;

        float nx = (local.x - myRect.rect.xMin) / myRect.rect.width;
        float nz = (local.y - myRect.rect.yMin) / myRect.rect.height;
        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);

        float wx = Mathf.Lerp(cityBounds.min.x, cityBounds.max.x, nx);
        float wz = Mathf.Lerp(cityBounds.min.z, cityBounds.max.z, nz);

        Vector3 camPos = CameraController.Instance.transform.position;
        CameraController.Instance.transform.position = new Vector3(wx, camPos.y, wz);
    }

    // =====================================================================
    //  PIXEL HELPERS
    // =====================================================================
    private void FillCircle(int cx, int cy, int r, Color32 col)
    {
        int r2 = r * r;
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy <= r2)
                    SetPixelSafe(cx + dx, cy + dy, col);
            }
        }
    }

    private void SetPixelSafe(int x, int y, Color32 col)
    {
        if (x >= 0 && x < textureSize && y >= 0 && y < textureSize)
        {
            float cx = textureSize * 0.5f;
            float cy = textureSize * 0.5f;
            float rSq = cx * cy;
            float dx = x - cx;
            float dy = y - cy;
            if ((dx * dx + dy * dy) <= rSq)
                pixels[y * textureSize + x] = col;
        }
    }

    private static Color32 LerpColor32(Color32 a, Color32 b, float t)
    {
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

/// Custom UI Graphic that draws 4 line segments forming a trapezoid.
/// Used for the Civ-style viewport indicator on the minimap.
[RequireComponent(typeof(CanvasRenderer))]
public class ViewportTrapezoid : Graphic
{
    public float lineWidth = 2f;
    private Vector2[] corners = new Vector2[4]; // BL, BR, TL, TR

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (corners == null || corners.Length < 4) return;

        // BL→BR (bottom)
        AddLine(vh, corners[0], corners[1]);
        // BR→TR (right)
        AddLine(vh, corners[1], corners[3]);
        // TR→TL (top)
        AddLine(vh, corners[3], corners[2]);
        // TL→BL (left)
        AddLine(vh, corners[2], corners[0]);
    }

    private void AddLine(VertexHelper vh, Vector2 a, Vector2 b)
    {
        Vector2 dir = (b - a);
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

    public void SetCorners(Vector2 bl, Vector2 br, Vector2 tl, Vector2 tr)
    {
        corners[0] = bl;
        corners[1] = br;
        corners[2] = tl;
        corners[3] = tr;
        SetVerticesDirty();
    }
}
