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

        [Header("Exposure & Contrast")]
        [Range(0.1f, 2f)]  public float exposure          = 0.90f;  // slightly underexposed
        [Range(0.5f, 3f)]  public float contrast          = 1.55f;  // hard contrast

        [Header("Black Crush")]
        [Range(0f, 1f)]    public float blackCrush        = 0.75f;  // deep blacks, key to cyberpunk

        [Header("Teal Grade")]
        [Range(0f, 2f)]    public float tealShadows       = 1.00f;  // how teal the darks go
        [Range(0f, 1f)]    public float tealMidtones      = 0.50f;  // teal push into midtones

        [Header("Neon Effect")]
        [Range(0f, 1f)]    public float neonThreshold     = 0.52f;  // luma level brights become neon
        [Range(0f, 2f)]    public float neonBloom         = 0.70f;  // how much brights glow
        [Range(0f, 3f)]    public float neonSaturation    = 1.80f;  // saturation of neon areas
        [Range(0f, 2f)]    public float neonHuePush       = 1.20f;  // push hues toward nearest neon

        [Header("Atmosphere")]
        [Range(0f, 2f)]    public float fogDensity        = 0.65f;  // teal city haze

        [Header("Chromatic Aberration")]
        [Range(0f, 6f)]    public float chromaticStrength = 1.80f;

        [Header("Sharpness")]
        [Range(0f, 3f)]    public float sharpness         = 0.80f;

        [Header("Grain")]
        [Range(0f, 0.3f)]  public float grainIntensity    = 0.07f;  // night ISO noise
        [Range(1f, 4f)]    public float grainSize         = 1.3f;

        [Header("Vignette")]
        [Range(0f, 1f)]    public float vignetteIntensity = 0.65f;  // deep tunnel vignette
    }

    public Settings settings = new Settings();
    Material _mat;
    CyberpunkPass _pass;

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

        renderer.EnqueuePass(_pass);
    }

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

            using (var builder = rg.AddRasterRenderPass<PassData>("CyberpunkFilter", out var data))
            {
                data.src = resourceData.activeColorTexture;
                data.mat = _mat;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(temp, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0));
            }

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
