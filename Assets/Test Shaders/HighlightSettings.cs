using UnityEngine;

public class HighlightSettings : MonoBehaviour
{
    public static HighlightSettings Instance { get; private set; }

    [Header("Highlight Material Sources")]
    [Tooltip("Material using Custom/FresnelGlow shader")]
    public Material glowMat;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
}