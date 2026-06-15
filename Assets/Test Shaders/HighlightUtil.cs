using UnityEngine;
using System.Collections.Generic;

public static class HighlightUtil
{
    private class Entry
    {
        public Dictionary<Renderer, Material[]> baseline = new();
        public Material idleMat;
    }

    private static readonly Dictionary<GameObject, Entry> _entries = new();

    public static void ApplyIdle(GameObject obj, Color color)
    {
        if (obj == null) return;
        if (HighlightSettings.Instance?.glowMat == null)
        {
            Debug.LogWarning("[HighlightUtil] glowMat not assigned on HighlightSettings.");
            return;
        }

        Remove(obj);

        var entry = new Entry();
        CaptureBaseline(obj, entry);

        entry.idleMat = new Material(HighlightSettings.Instance.glowMat);
        entry.idleMat.SetColor("_GlowColor",    color);
        entry.idleMat.SetFloat("_GlowIntensity", 4.0f);
        entry.idleMat.SetFloat("_GlowPower",     3.0f);

        _entries[obj] = entry;
        RebuildRenderers(obj, entry);
    }

    public static void Remove(GameObject obj)
    {
        if (obj == null || !_entries.TryGetValue(obj, out Entry entry)) return;

        foreach (var (rend, origMats) in entry.baseline)
        {
            if (rend != null)
                rend.sharedMaterials = origMats;
        }

        if (entry.idleMat != null) Object.Destroy(entry.idleMat);
        _entries.Remove(obj);
    }

    private static void CaptureBaseline(GameObject obj, Entry entry)
    {
        foreach (Renderer rend in obj.GetComponentsInChildren<Renderer>(true))
        {
            if (ShouldSkip(rend)) continue;
            entry.baseline[rend] = rend.sharedMaterials;
        }
    }

    private static void RebuildRenderers(GameObject obj, Entry entry)
    {
        foreach (var (rend, baseMats) in entry.baseline)
        {
            if (rend == null) continue;
            var newMats = new Material[baseMats.Length + 1];
            System.Array.Copy(baseMats, newMats, baseMats.Length);
            newMats[baseMats.Length] = entry.idleMat;
            rend.materials = newMats;
        }
    }

    private static bool ShouldSkip(Renderer r)
    {
        if (r == null || !r.enabled) return true;
        string n = r.gameObject.name;
        return n.Contains("RangeIndicator") || n.Contains("Cylinder");
    }
}