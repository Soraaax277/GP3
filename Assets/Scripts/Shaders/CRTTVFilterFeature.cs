using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CRTTVFilterFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;

        [Header("Screen Shape")]
        [Range(0f, 1.5f)] public float barrelStrength     = 0.35f;  // 0 = flat, higher = more curved/rounded

        [Header("CRT Curvature")]
        [Range(0f, 0.5f)] public float curvatureStrength   = 0.02f;  // very subtle - raise to taste

        [Header("Scanlines")]
        [Range(0f, 1f)]   public float scanlineIntensity   = 0.65f;
        [Range(0.1f, 1f)] public float scanlineThickness   = 0.6f;

        [Header("Phosphor Mask")]
        [Range(0f, 1f)]   public float phosphorIntensity   = 0.45f;

        [Header("NTSC Color Bleed")]
        [Range(0f, 8f)]   public float colorBleedStrength  = 4.0f;

        [Header("Static / Snow")]
        [Range(0f, 0.5f)] public float staticIntensity     = 0.03f;

        [Header("Sync Wobble")]
        [Range(0f, 4f)]   public float syncWobble          = 0.5f;

        [Header("Signal Roll")]
        [Range(0f, 1f)]   public float signalRollIntensity = 0.0f;
        [Range(0f, 4f)]   public float signalRollSpeed     = 1.0f;

        [Header("Chromatic Aberration")]
        [Range(0f, 8f)]   public float chromaticStrength   = 5.0f;

        [Header("Vignette")]
        [Range(0f, 1f)]   public float vignetteIntensity   = 0.4f;

        [Header("Brightness")]
        [Range(0.5f, 2f)] public float brightnessBoost     = 1.05f;

        [Header("Flicker")]
        [Range(0f, 1f)]   public float flickerIntensity    = 0.15f;

        [Header("Color Grade")]
        [Range(0f, 2f)]   public float saturation          = 1.35f;  // >1 = more vivid
        [Range(0f, 0.5f)] public float blackLift           = 0.12f;  // lifts shadows, faded feel
        [Range(0f, 2f)]   public float vibrance            = 0.55f;  // boosts dull colours selectively
        [ColorUsage(false)]
        public Color phosphorTint = new Color(0.85f, 1.05f, 0.80f); // slight green-cool CRT cast
        [Range(0f, 1f)]   public float phosphorTintAmount  = 0.25f;
    }

    public Settings settings = new Settings();
    Material _mat;
    CRTTVFilterPass _pass;

    static readonly int ID_CurvatureStrength   = Shader.PropertyToID("_CurvatureStrength");
    static readonly int ID_BarrelStrength      = Shader.PropertyToID("_BarrelStrength");
    static readonly int ID_ScanlineIntensity   = Shader.PropertyToID("_ScanlineIntensity");
    static readonly int ID_ScanlineThickness   = Shader.PropertyToID("_ScanlineThickness");
    static readonly int ID_PhosphorIntensity   = Shader.PropertyToID("_PhosphorIntensity");
    static readonly int ID_ColorBleedStrength  = Shader.PropertyToID("_ColorBleedStrength");
    static readonly int ID_StaticIntensity     = Shader.PropertyToID("_StaticIntensity");
    static readonly int ID_SyncWobble          = Shader.PropertyToID("_SyncWobble");
    static readonly int ID_SignalRollIntensity = Shader.PropertyToID("_SignalRollIntensity");
    static readonly int ID_SignalRollSpeed     = Shader.PropertyToID("_SignalRollSpeed");
    static readonly int ID_ChromaticStrength   = Shader.PropertyToID("_ChromaticStrength");
    static readonly int ID_VignetteIntensity   = Shader.PropertyToID("_VignetteIntensity");
    static readonly int ID_BrightnessBoost     = Shader.PropertyToID("_BrightnessBoost");
    static readonly int ID_FlickerIntensity    = Shader.PropertyToID("_FlickerIntensity");
    static readonly int ID_Saturation          = Shader.PropertyToID("_Saturation");
    static readonly int ID_BlackLift           = Shader.PropertyToID("_BlackLift");
    static readonly int ID_Vibrance            = Shader.PropertyToID("_Vibrance");
    static readonly int ID_PhosphorTint        = Shader.PropertyToID("_PhosphorTint");

    public override void Create()
    {
        _mat = settings.material;

        if (_mat == null)
        {
            Debug.LogWarning("[CRTTVFilterFeature] No material assigned. " +
                             "Create a material using Custom/URP/CRTTVFilter and assign it.");
            return;
        }

        _pass = new CRTTVFilterPass(_mat, settings);
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (_mat == null) return;

        _mat.SetFloat(ID_CurvatureStrength,   settings.curvatureStrength);
        _mat.SetFloat(ID_BarrelStrength,      settings.barrelStrength);
        _mat.SetFloat(ID_ScanlineIntensity,   settings.scanlineIntensity);
        _mat.SetFloat(ID_ScanlineThickness,   settings.scanlineThickness);
        _mat.SetFloat(ID_PhosphorIntensity,   settings.phosphorIntensity);
        _mat.SetFloat(ID_ColorBleedStrength,  settings.colorBleedStrength);
        _mat.SetFloat(ID_StaticIntensity,     settings.staticIntensity);
        _mat.SetFloat(ID_SyncWobble,          settings.syncWobble);
        _mat.SetFloat(ID_SignalRollIntensity, settings.signalRollIntensity);
        _mat.SetFloat(ID_SignalRollSpeed,     settings.signalRollSpeed);
        _mat.SetFloat(ID_ChromaticStrength,   settings.chromaticStrength);
        _mat.SetFloat(ID_VignetteIntensity,   settings.vignetteIntensity);
        _mat.SetFloat(ID_BrightnessBoost,     settings.brightnessBoost);
        _mat.SetFloat(ID_FlickerIntensity,    settings.flickerIntensity);
        _mat.SetFloat(ID_Saturation,          settings.saturation);
        _mat.SetFloat(ID_BlackLift,           settings.blackLift);
        _mat.SetFloat(ID_Vibrance,            settings.vibrance);
        // Pack tint colour + amount into a float4
        _mat.SetVector(ID_PhosphorTint, new Vector4(
            settings.phosphorTint.r,
            settings.phosphorTint.g,
            settings.phosphorTint.b,
            settings.phosphorTintAmount));

        renderer.EnqueuePass(_pass);
    }

    class CRTTVFilterPass : ScriptableRenderPass
    {
        Material _mat;
        Settings _s;

        public CRTTVFilterPass(Material m, Settings s) { _mat = m; _s = s; }

        class PassData
        {
            public TextureHandle src;
            public Material      mat;
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer ctx)
        {
            var resourceData = ctx.Get<UniversalResourceData>();

            var desc = rg.GetTextureDesc(resourceData.activeColorTexture);
            desc.name        = "_CRTTVFilterTmp";
            desc.clearBuffer = false;
            var temp = rg.CreateTexture(desc);

            using (var builder = rg.AddRasterRenderPass<PassData>("CRTTVFilter", out var data))
            {
                data.src = resourceData.activeColorTexture;
                data.mat = _mat;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(temp, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0));
            }

            using (var builder = rg.AddRasterRenderPass<PassData>("CRTTVFilter CopyBack", out var data))
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