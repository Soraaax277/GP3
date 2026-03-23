using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CyberpunkFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;

        // ─── Exposure & Contrast ───────────────────────────────────────────────
        [Header("Exposure & Contrast")]
        [Range(0.1f, 2f)]   public float exposure          = 1.00f;  // neutral – don't underexpose
        [Range(0.5f, 3f)]   public float contrast          = 1.15f;  // gentle punch, not crushing

        // ─── Black Crush ───────────────────────────────────────────────────────
        [Header("Black Crush")]
        [Range(0f, 1f)]     public float blackCrush        = 0.15f;  // just a whisper – keep detail!

        // ─── Teal Grade ────────────────────────────────────────────────────────
        [Header("Teal Grade")]
        [Range(0f, 2f)]     public float tealShadows       = 0.28f;  // hint of cool in shadows
        [Range(0f, 1f)]     public float tealMidtones      = 0.12f;  // barely any mid-tone shift

        // ─── Neon Effect ───────────────────────────────────────────────────────
        [Header("Neon Effect")]
        [Range(0f, 1f)]     public float neonThreshold     = 0.68f;  // only the very bright highlights
        [Range(0f, 2f)]     public float neonBloom         = 0.35f;
        [Range(0f, 3f)]     public float neonSaturation    = 1.10f;
        [Range(0f, 2f)]     public float neonHuePush       = 0.70f;

        // ─── Atmosphere ────────────────────────────────────────────────────────
        [Header("Atmosphere")]
        [Range(0f, 2f)]     public float fogDensity        = 0.18f;  // subtle horizon haze only

        // ─── Chromatic Aberration ──────────────────────────────────────────────
        [Header("Chromatic Aberration")]
        [Range(0f, 6f)]     public float chromaticStrength = 0.70f;  // lens feel, not VHS chaos

        // ─── Sharpness ─────────────────────────────────────────────────────────
        [Header("Sharpness")]
        [Range(0f, 3f)]     public float sharpness         = 1.30f;  // higher – keeps model crisp

        // ─── Grain ─────────────────────────────────────────────────────────────
        [Header("Grain")]
        [Range(0f, 0.3f)]   public float grainIntensity    = 0.04f;
        [Range(1f, 4f)]     public float grainSize         = 1.3f;

        // ─── Vignette ──────────────────────────────────────────────────────────
        [Header("Vignette")]
        [Range(0f, 1f)]     public float vignetteIntensity = 0.30f;  // gentle tunnel, not cave

        // ─── Neon Outline ──────────────────────────────────────────────────────
        // Sobel edge detection → neon glow on every object silhouette / detail edge
        [Header("Neon Outline")]
        [Range(0f, 1f)]     public float outlineIntensity  = 0.80f;
        [Range(0f, 5f)]     public float outlineThickness  = 1.50f;  // scales sample offset
        public Color        outlineColor = new Color(0.00f, 1.00f, 0.92f, 1.0f); // electric cyan

        // ─── HUD Hex Grid ──────────────────────────────────────────────────────
        // Procedural hexagonal grid drawn on the left/right screen sides
        [Header("HUD Hex Grid")]
        [Range(0f, 1f)]     public float hexOpacity        = 0.65f;
        [Range(0f, 0.45f)]  public float hexPanelWidth     = 0.22f;  // fraction of screen width each side
        [Range(1f, 40f)]    public float hexGridScale      = 14f;    // density of hexagons
        public Color        hexColor     = new Color(0.00f, 0.82f, 1.00f, 1.0f); // cyan-blue
    }

    public Settings settings = new Settings();
    Material _mat;
    CyberpunkPass _pass;

    // ─── Shader property IDs ──────────────────────────────────────────────────
    static readonly int ID_Exposure          = Shader.PropertyToID("_Exposure");
    static readonly int ID_Contrast          = Shader.PropertyToID("_Contrast");
    static readonly int ID_BlackCrush        = Shader.PropertyToID("_BlackCrush");
    static readonly int ID_TealShadows       = Shader.PropertyToID("_TealShadows");
    static readonly int ID_TealMidtones      = Shader.PropertyToID("_TealMidtones");
    static readonly int ID_NeonThreshold     = Shader.PropertyToID("_NeonThreshold");
    static readonly int ID_NeonBloom         = Shader.PropertyToID("_NeonBloom");
    static readonly int ID_NeonSaturation    = Shader.PropertyToID("_NeonSaturation");
    static readonly int ID_NeonHuePush       = Shader.PropertyToID("_NeonHuePush");
    static readonly int ID_FogDensity        = Shader.PropertyToID("_FogDensity");
    static readonly int ID_ChromaticStrength = Shader.PropertyToID("_ChromaticStrength");
    static readonly int ID_Sharpness         = Shader.PropertyToID("_Sharpness");
    static readonly int ID_GrainIntensity    = Shader.PropertyToID("_GrainIntensity");
    static readonly int ID_GrainSize         = Shader.PropertyToID("_GrainSize");
    static readonly int ID_VignetteIntensity = Shader.PropertyToID("_VignetteIntensity");
    // New
    static readonly int ID_OutlineIntensity  = Shader.PropertyToID("_OutlineIntensity");
    static readonly int ID_OutlineThickness  = Shader.PropertyToID("_OutlineThickness");
    static readonly int ID_OutlineColor      = Shader.PropertyToID("_OutlineColor");
    static readonly int ID_HexOpacity        = Shader.PropertyToID("_HexOpacity");
    static readonly int ID_HexPanelWidth     = Shader.PropertyToID("_HexPanelWidth");
    static readonly int ID_HexGridScale      = Shader.PropertyToID("_HexGridScale");
    static readonly int ID_HexColor          = Shader.PropertyToID("_HexColor");

    public override void Create()
    {
        _mat = settings.material;

        if (_mat == null)
        {
            Debug.LogWarning("[CyberpunkFeature] No material assigned. " +
                             "Create a material using Custom/URP/CyberpunkFilter and assign it.");
            return;
        }

        _pass = new CyberpunkPass(_mat, settings);
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (_mat == null) return;

        // Push every setting to the material each frame so inspector tweaks are live
        _mat.SetFloat(ID_Exposure,          settings.exposure);
        _mat.SetFloat(ID_Contrast,          settings.contrast);
        _mat.SetFloat(ID_BlackCrush,        settings.blackCrush);
        _mat.SetFloat(ID_TealShadows,       settings.tealShadows);
        _mat.SetFloat(ID_TealMidtones,      settings.tealMidtones);
        _mat.SetFloat(ID_NeonThreshold,     settings.neonThreshold);
        _mat.SetFloat(ID_NeonBloom,         settings.neonBloom);
        _mat.SetFloat(ID_NeonSaturation,    settings.neonSaturation);
        _mat.SetFloat(ID_NeonHuePush,       settings.neonHuePush);
        _mat.SetFloat(ID_FogDensity,        settings.fogDensity);
        _mat.SetFloat(ID_ChromaticStrength, settings.chromaticStrength);
        _mat.SetFloat(ID_Sharpness,         settings.sharpness);
        _mat.SetFloat(ID_GrainIntensity,    settings.grainIntensity);
        _mat.SetFloat(ID_GrainSize,         settings.grainSize);
        _mat.SetFloat(ID_VignetteIntensity, settings.vignetteIntensity);
        // Outline
        _mat.SetFloat(ID_OutlineIntensity,  settings.outlineIntensity);
        _mat.SetFloat(ID_OutlineThickness,  settings.outlineThickness);
        _mat.SetColor(ID_OutlineColor,      settings.outlineColor);
        // Hex HUD
        _mat.SetFloat(ID_HexOpacity,        settings.hexOpacity);
        _mat.SetFloat(ID_HexPanelWidth,     settings.hexPanelWidth);
        _mat.SetFloat(ID_HexGridScale,      settings.hexGridScale);
        _mat.SetColor(ID_HexColor,          settings.hexColor);

        renderer.EnqueuePass(_pass);
    }

    // ─── Render pass ─────────────────────────────────────────────────────────
    class CyberpunkPass : ScriptableRenderPass
    {
        Material _mat;
        Settings _s;

        public CyberpunkPass(Material m, Settings s) { _mat = m; _s = s; }

        class PassData
        {
            public TextureHandle src;
            public Material      mat;
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer ctx)
        {
            var resourceData = ctx.Get<UniversalResourceData>();

            var desc = rg.GetTextureDesc(resourceData.activeColorTexture);
            desc.name        = "_CyberpunkTmp";
            desc.clearBuffer = false;
            var temp = rg.CreateTexture(desc);

            // Blit active → temp through our shader
            using (var builder = rg.AddRasterRenderPass<PassData>("CyberpunkFilter", out var data))
            {
                data.src = resourceData.activeColorTexture;
                data.mat = _mat;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(temp, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0));
            }

            // Copy temp back to active color buffer
            using (var builder = rg.AddRasterRenderPass<PassData>("CyberpunkFilter CopyBack", out var data))
            {
                data.src = temp;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), 0, false));
            }
        }
    }
}