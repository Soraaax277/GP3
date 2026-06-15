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
        [Range(0.5f, 2f)]   public float exposure           = 1.00f;
        [Range(0.5f, 2f)]   public float contrast           = 1.25f;
        [Range(0f, 1f)]     public float blackCrush         = 0.12f;
        [Range(0f, 1f)]     public float highlightRolloff   = 0.35f;

        [Header("Saturation")]
        [Range(0f, 2f)]     public float saturation         = 1.15f;

        [Header("Teal-Green Grade")]
        [Tooltip("How strongly shadows push toward teal/green-cyan. Shader now uses equal R-drain + G-push so this is genuinely teal, not blue.")]
        [Range(0f, 2f)]     public float shadowTealStrength = 1.10f;
        [Tooltip("Raises the black floor. Also auto-floors extremely dark pixels so scenes never collapse.")]
        [Range(0f, 1f)]     public float shadowLift         = 0.30f;
        [Tooltip("Midtone temperature. 1 = neutral, >1 = warm, <1 = cool.")]
        [Range(0f, 2f)]     public float midtoneBalance     = 1.08f;
        [Tooltip("How strongly highlights push toward orange/amber. 0 = no push, 1 = strong.")]
        [Range(0f, 2f)]     public float highlightWarmth    = 0.85f;

        [Header("H.264 Chroma Smear")]
        [Range(0f, 1f)]     public float chromaSmear        = 0.25f;

        [Header("Sharpness")]
        [Range(0f, 2f)]     public float sharpness          = 0.45f;

        [Header("Digital Noise")]
        [Range(0f, 0.2f)]   public float noiseIntensity     = 0.04f;
        [Range(1f, 6f)]     public float noiseSize          = 1.5f;

        [Header("Vignette")]
        [Range(0f, 1f)]     public float vignetteIntensity  = 0.28f;

        [Header("Letterbox")]
        [Range(0f, 0.2f)]   public float letterboxAmount    = 0.07f;
    }

    public Settings settings = new Settings();
    Material _mat;
    NightGradePass _pass;

    static readonly int ID_Exposure            = Shader.PropertyToID("_Exposure");
    static readonly int ID_Contrast            = Shader.PropertyToID("_Contrast");
    static readonly int ID_BlackCrush          = Shader.PropertyToID("_BlackCrush");
    static readonly int ID_HighlightRolloff    = Shader.PropertyToID("_HighlightRolloff");
    static readonly int ID_Saturation          = Shader.PropertyToID("_Saturation");
    static readonly int ID_ShadowTealStrength  = Shader.PropertyToID("_ShadowTealStrength");
    static readonly int ID_ShadowLift          = Shader.PropertyToID("_ShadowLift");
    static readonly int ID_MidtoneBalance      = Shader.PropertyToID("_MidtoneBalance");
    static readonly int ID_HighlightWarmth     = Shader.PropertyToID("_HighlightWarmth");
    static readonly int ID_ChromaSmear         = Shader.PropertyToID("_ChromaSmear");
    static readonly int ID_Sharpness           = Shader.PropertyToID("_Sharpness");
    static readonly int ID_NoiseIntensity      = Shader.PropertyToID("_NoiseIntensity");
    static readonly int ID_NoiseSize           = Shader.PropertyToID("_NoiseSize");
    static readonly int ID_VignetteIntensity   = Shader.PropertyToID("_VignetteIntensity");
    static readonly int ID_LetterboxAmount     = Shader.PropertyToID("_LetterboxAmount");

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

        _mat.SetFloat(ID_Exposure,           settings.exposure);
        _mat.SetFloat(ID_Contrast,           settings.contrast);
        _mat.SetFloat(ID_BlackCrush,         settings.blackCrush);
        _mat.SetFloat(ID_HighlightRolloff,   settings.highlightRolloff);
        _mat.SetFloat(ID_Saturation,         settings.saturation);
        _mat.SetFloat(ID_ShadowTealStrength, settings.shadowTealStrength);
        _mat.SetFloat(ID_ShadowLift,         settings.shadowLift);
        _mat.SetFloat(ID_MidtoneBalance,     settings.midtoneBalance);
        _mat.SetFloat(ID_HighlightWarmth,    settings.highlightWarmth);
        _mat.SetFloat(ID_ChromaSmear,        settings.chromaSmear);
        _mat.SetFloat(ID_Sharpness,          settings.sharpness);
        _mat.SetFloat(ID_NoiseIntensity,     settings.noiseIntensity);
        _mat.SetFloat(ID_NoiseSize,          settings.noiseSize);
        _mat.SetFloat(ID_VignetteIntensity,  settings.vignetteIntensity);
        _mat.SetFloat(ID_LetterboxAmount,    settings.letterboxAmount);

        renderer.EnqueuePass(_pass);
    }

    // ─────────────────────────────────────────────────────────────────────────
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

            // Pass 1 — apply grade into temp
            using (var builder = rg.AddRasterRenderPass<PassData>("NightGradeFilter", out var data))
            {
                data.src = resourceData.activeColorTexture;
                data.mat = _mat;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(temp, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0));
            }

            // Pass 2 — copy back to active color
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