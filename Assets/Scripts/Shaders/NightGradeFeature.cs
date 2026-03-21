using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class NightGradeFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;

        [Header("Exposure & Contrast")]
        [Range(0.5f, 2f)]  public float exposure          = 1.10f;  // slightly hot like a prosumer cam
        [Range(0.5f, 2f)]  public float contrast          = 1.30f;  // punchy mids
        [Range(0f, 1f)]    public float blackCrush        = 0.20f;  // not too deep - 2000s had lifted blacks

        [Header("Saturation")]
        [Range(0f, 3f)]    public float saturation        = 1.70f;  // over-saturated video look

        [Header("Green Tint")]
        [Range(0f, 2f)]    public float greenTint         = 1.00f;  // pump the greens
        [Range(0f, 2f)]    public float greenShadowLift   = 1.20f;  // greens in the darks
        [Range(0f, 2f)]    public float redDrain          = 0.60f;  // slight red pulldown
        [Range(0f, 2f)]    public float blueShift         = 0.40f;  // slight cyan push

        [Header("Highlights")]
        [Range(0f, 1f)]    public float highlightBlow     = 0.30f;  // slightly clipped whites

        [Header("Sharpness")]
        [Range(0f, 4f)]    public float sharpness         = 1.20f;  // over-sharpened DV look

        [Header("Film Grain")]
        [Range(0f, 0.3f)]  public float grainIntensity    = 0.055f;
        [Range(1f, 4f)]    public float grainSize         = 1.2f;

        [Header("Vignette")]
        [Range(0f, 1f)]    public float vignetteIntensity = 0.20f;  // very subtle

        [Header("Letterbox")]
        [Range(0f, 0.2f)]  public float letterboxAmount   = 0.07f;  // 2.35:1 style black bars
    }

    public Settings settings = new Settings();
    Material _mat;
    NightGradePass _pass;

    static readonly int ID_Exposure          = Shader.PropertyToID("_Exposure");
    static readonly int ID_Contrast          = Shader.PropertyToID("_Contrast");
    static readonly int ID_BlackCrush        = Shader.PropertyToID("_BlackCrush");
    static readonly int ID_Saturation        = Shader.PropertyToID("_Saturation");
    static readonly int ID_GreenTint         = Shader.PropertyToID("_GreenTint");
    static readonly int ID_GreenShadowLift   = Shader.PropertyToID("_GreenShadowLift");
    static readonly int ID_RedDrain          = Shader.PropertyToID("_RedDrain");
    static readonly int ID_BlueShift         = Shader.PropertyToID("_BlueShift");
    static readonly int ID_HighlightBlow     = Shader.PropertyToID("_HighlightBlow");
    static readonly int ID_Sharpness         = Shader.PropertyToID("_Sharpness");
    static readonly int ID_GrainIntensity    = Shader.PropertyToID("_GrainIntensity");
    static readonly int ID_GrainSize         = Shader.PropertyToID("_GrainSize");
    static readonly int ID_VignetteIntensity = Shader.PropertyToID("_VignetteIntensity");
    static readonly int ID_LetterboxAmount   = Shader.PropertyToID("_LetterboxAmount");

    public override void Create()
    {
        _mat = settings.material;

        if (_mat == null)
        {
            Debug.LogWarning("[NightGradeFeature] No material assigned. " +
                             "Create a material using Custom/URP/NightGradeFilter and assign it.");
            return;
        }

        _pass = new NightGradePass(_mat, settings);
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (_mat == null) return;

        _mat.SetFloat(ID_Exposure,          settings.exposure);
        _mat.SetFloat(ID_Contrast,          settings.contrast);
        _mat.SetFloat(ID_BlackCrush,        settings.blackCrush);
        _mat.SetFloat(ID_Saturation,        settings.saturation);
        _mat.SetFloat(ID_GreenTint,         settings.greenTint);
        _mat.SetFloat(ID_GreenShadowLift,   settings.greenShadowLift);
        _mat.SetFloat(ID_RedDrain,          settings.redDrain);
        _mat.SetFloat(ID_BlueShift,         settings.blueShift);
        _mat.SetFloat(ID_HighlightBlow,     settings.highlightBlow);
        _mat.SetFloat(ID_Sharpness,         settings.sharpness);
        _mat.SetFloat(ID_GrainIntensity,    settings.grainIntensity);
        _mat.SetFloat(ID_GrainSize,         settings.grainSize);
        _mat.SetFloat(ID_VignetteIntensity, settings.vignetteIntensity);
        _mat.SetFloat(ID_LetterboxAmount,   settings.letterboxAmount);

        renderer.EnqueuePass(_pass);
    }

    class NightGradePass : ScriptableRenderPass
    {
        Material _mat;
        Settings _s;

        public NightGradePass(Material m, Settings s) { _mat = m; _s = s; }

        class PassData
        {
            public TextureHandle src;
            public Material      mat;
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer ctx)
        {
            var resourceData = ctx.Get<UniversalResourceData>();

            var desc = rg.GetTextureDesc(resourceData.activeColorTexture);
            desc.name        = "_NightGradeTmp";
            desc.clearBuffer = false;
            var temp = rg.CreateTexture(desc);

            using (var builder = rg.AddRasterRenderPass<PassData>("NightGradeFilter", out var data))
            {
                data.src = resourceData.activeColorTexture;
                data.mat = _mat;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(temp, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0));
            }

            using (var builder = rg.AddRasterRenderPass<PassData>("NightGradeFilter CopyBack", out var data))
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
