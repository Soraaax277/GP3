using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// THE TEMPORARY HOOK 
// Handles per-frame updates for the glitch effect
public class CRTRuntimeHook : BaseMeshEffect
{
    private UICRTEffect _controller;

    public void Setup(UICRTEffect controller)
    {
        _controller = controller;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    private void Update()
    {
        // Force the mesh to update every frame if the game is playing.
        // This is required to make the glitch "animate".
        if (Application.isPlaying && graphic != null && _controller != null)
        {
            graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || _controller == null) return;

        List<UIVertex> verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);

        for (int i = 0; i < verts.Count; i++)
        {
            UIVertex v = verts[i];
            
            // Pass the point to the controller for processing
            v.position = transform.InverseTransformPoint(
                _controller.DistortPoint(transform.TransformPoint(v.position))
            );
            verts[i] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}