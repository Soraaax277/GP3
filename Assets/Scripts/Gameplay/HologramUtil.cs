using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class RendererMaterialPair
{
    public Renderer renderer;
    public Material[] originalMaterials;
}

public class OriginalMaterialStorage : MonoBehaviour
{
    public List<RendererMaterialPair> pairs = new List<RendererMaterialPair>();

    public void Store(Renderer[] renderers)
    {
        pairs.Clear();
        foreach (var r in renderers)
        {
            // Skip utility children that shouldn't be captured
            if (r.gameObject.name.Contains("RangeIndicator") || r.gameObject.name.Contains("Cylinder")) continue;

            pairs.Add(new RendererMaterialPair 
            { 
                renderer = r, 
                originalMaterials = r.sharedMaterials 
            });
        }
    }

    public void AddMissing(Renderer[] renderers)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (r.gameObject.name.Contains("RangeIndicator") || r.gameObject.name.Contains("Cylinder")) continue;

            bool found = false;
            foreach (var pair in pairs)
            {
                if (pair.renderer == r)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                pairs.Add(new RendererMaterialPair 
                { 
                    renderer = r, 
                    originalMaterials = r.sharedMaterials 
                });
            }
        }
    }

    public void Restore()
    {
        foreach (var pair in pairs)
        {
            if (pair.renderer != null)
            {
                pair.renderer.sharedMaterials = pair.originalMaterials;
            }
        }
    }
}

public static class HologramUtil
{
    public static void MakeHologram(GameObject obj, Color color)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        
        // Store original materials if not already stored, or update if new ones arrived
        OriginalMaterialStorage storage = obj.GetComponent<OriginalMaterialStorage>();
        if (storage == null)
        {
            storage = obj.AddComponent<OriginalMaterialStorage>();
            storage.Store(renderers);
        }
        else
        {
            storage.AddMissing(renderers);
        }

        foreach (Renderer r in renderers)
        {
            // Skip range indicators or other internal primitives if needed
            if (r.gameObject.name.Contains("RangeIndicator") || r.gameObject.name.Contains("Cylinder")) continue;

            Material[] holoMats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < holoMats.Length; i++)
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = color;
                holoMats[i] = mat;
            }

            r.materials = holoMats;
        }
    }

    public static void MakeSolid(GameObject obj)
    {
        OriginalMaterialStorage storage = obj.GetComponent<OriginalMaterialStorage>();
        if (storage != null)
        {
            storage.Restore();
            // We can keep the component if we ever need to go back to hologram (e.g. undoing)
            // But usually we can just destroy it if it's "built"
        }
        
        // Ensure any newly-created active children (from Era visuals that weren't caught in MakeHologram initially)
        // are forcefully set to solid by removing any tint they might have accidentally inherited.
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r.gameObject.name.Contains("RangeIndicator") || r.gameObject.name.Contains("Cylinder")) continue;
            
            if (storage != null)
            {
                 bool found = false;
                 foreach (var pair in storage.pairs) 
                     if (pair.renderer == r) { found = true; break; }
                 if (found) continue;
            }

            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    // Check before modifying alpha
                    bool fromHologram = (Mathf.Approximately(c.r, 0f) && Mathf.Approximately(c.g, 0.5f) && Mathf.Approximately(c.b, 1f));
                    c.a = 1f; // Force opaque
                    if (fromHologram) c = Color.white; // Revert holograms exactly
                    mat.color = c;
                }
            }
        }
    }
}
