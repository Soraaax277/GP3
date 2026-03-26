using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BokehTimeDriver : MonoBehaviour
{
    [Header("Bokeh Images")]
    [Tooltip("Assign every Image using the BokehBlurUI material here.")]
    [SerializeField] private List<Image> bokehImages;

    [Tooltip("Use unscaled time so effects animate while game is paused.")]
    [SerializeField] private bool useUnscaledTime = true;

    private List<Material> _instancedMaterials = new List<Material>();

    private void Awake()
    {
        _instancedMaterials.Clear();
        foreach (var img in bokehImages)
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