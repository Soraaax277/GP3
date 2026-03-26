using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PowerGridOverlay : MonoBehaviour
{
    public static PowerGridOverlay Instance;

    [Header("Settings")]
    public bool isEnabled = false;
    public float pulseSpeed = 2f;
    public float meshHeight = 0.8f;
    public int bottleneckThreshold = 8; // High load if supporting > 8 nodes

    [Header("Visuals")]
    public Color normalFlowColor = new Color(0f, 0.5f, 1f, 0.7f); // Blue
    public Color overloadedColor = new Color(1f, 0.3f, 0f, 1f); // Orange/Red

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;

    private List<Vector3> verts = new List<Vector3>();
    private List<int> tris = new List<int>();
    private List<Color> colors = new List<Color>();
    private List<Vector2> uvs = new List<Vector2>();

    private void Awake()
    {
        Instance = this;
        
        // Use a dedicated child object so we don't conflict with GameManager's components
        GameObject holder = new GameObject("Overlay_Mesh");
        holder.transform.SetParent(this.transform);
        holder.transform.localPosition = Vector3.zero;
        
        meshFilter = holder.AddComponent<MeshFilter>();
        meshRenderer = holder.AddComponent<MeshRenderer>();
        mesh = new Mesh();
        meshFilter.mesh = mesh;
        
        // Use a simple material that supports vertex colors and scrolling
        meshRenderer.material = new Material(Shader.Find("Unlit/Transparent"));
        meshRenderer.material.color = Color.white;
        
        // Hidden by default
        holder.SetActive(false);
        this.gameObject.SetActive(true); // Parent stays active for Input checks
    }

    private void Update()
    {
        // Toggle view
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            ToggleOverlay();
        }

        if (isEnabled)
        {
            // Update texture offset for pulsing effect
            // Note: This assumes a material with _MainTex offset support
            meshRenderer.material.mainTextureOffset = new Vector2(-Time.time * pulseSpeed, 0f);
        }
    }

    public void ToggleOverlay()
    {
        isEnabled = !isEnabled;
        gameObject.SetActive(isEnabled);
        if (isEnabled) Refresh();
    }

    public void Refresh()
    {
        if (!isEnabled) return;
        if (PowerGridManager.Instance == null) return;

        ClearMesh();

        foreach (var entry in PowerGridManager.Instance.powerFlowMap)
        {
            HexTile child = entry.Key;
            HexTile parent = entry.Value;

            if (child == null || parent == null) continue;

            int load = PowerGridManager.Instance.powerLoad.ContainsKey(parent) ? PowerGridManager.Instance.powerLoad[parent] : 0;
            Color edgeColor = (load > bottleneckThreshold) ? overloadedColor : normalFlowColor;

            DrawFlowEdge(parent.transform.position, child.transform.position, edgeColor);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void DrawFlowEdge(Vector3 start, Vector3 end, Color color)
    {
        start.y = meshHeight;
        end.y = meshHeight;

        Vector3 dir = (end - start).normalized;
        Vector3 right = Vector3.Cross(dir, Vector3.up).normalized;
        float width = 0.12f;

        int baseIdx = verts.Count;

        verts.Add(start - right * width);
        verts.Add(start + right * width);
        verts.Add(end - right * width);
        verts.Add(end + right * width);

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        // UVs for scrolling pulses: X is along the line, Y is across
        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));

        tris.Add(baseIdx + 0);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 1);

        tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 3);
    }

    private void ClearMesh()
    {
        mesh.Clear();
        verts.Clear();
        tris.Clear();
        colors.Clear();
        uvs.Clear();
    }
}
