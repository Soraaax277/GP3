using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Standalone driver for fog shaders when used outside of TechTreeWindowManager.
/// Attach this to any GameObject that has fog Image components as children,
/// or assign the Images manually in the Inspector.
/// </summary>
public class FogTimeDriver : MonoBehaviour
{
    [Header("Fog Images")]
    [Tooltip("Assign all Image components using a fog shader here.")]
    [SerializeField] private List<Image> fogImages;

    [Header("Settings")]
    [Tooltip("Use unscaled time so fog keeps moving when game is paused.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("If true, will auto-find all Image components on children at start.")]
    [SerializeField] private bool autoFindChildren = false;

    private List<Material> _instancedMaterials = new List<Material>();

    private void Start()
    {
        if (autoFindChildren)
        {
            fogImages = new List<Image>(GetComponentsInChildren<Image>());
        }

        // Instance each material so they don't share state
        _instancedMaterials.Clear();
        foreach (var img in fogImages)
        {
            if (img == null) continue;
            var mat = Instantiate(img.material);
            img.material = mat;
            _instancedMaterials.Add(mat);
        }
    }

    private void Update()
    {
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        foreach (var mat in _instancedMaterials)
            if (mat != null) mat.SetFloat("_ManualTime", t);
    }

    private void OnDestroy()
    {
        foreach (var mat in _instancedMaterials)
            if (mat != null) Destroy(mat);
        _instancedMaterials.Clear();
    }
}