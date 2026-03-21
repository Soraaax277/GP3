using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class FilmFilterFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;

        [Header("Color Tint")]
        [ColorUsage(false)]
        public Color colorTint      = new Color(1.0f, 0.82f, 0.55f);
        [Range(0f, 1f)]
        public float tintStrength   = 0.18f;

        [Header("Saturation")]
        [Tooltip("1 = original, 0 = fully greyscale, values above 1 boost saturation.")]
        [Range(0f, 2f)]   public float saturation        = 1.0f;

        [Header("Sepia")]
        [Tooltip("0 = no sepia, 1 = full classic sepia tone.")]
        [Range(0f, 1f)]   public float sepiaStrength     = 0.0f;

        [Header("Fade / Bleach")]
        [Tooltip("Fades the image toward a warm paper tone like an old bleached photo.")]
        [Range(0f, 1f)]   public float fadeStrength      = 0.0f;

        [Header("Scanlines")]
        [Range(0f, 1f)]   public float scanlineIntensity = 0.20f;
        [Range(1f, 8f)]   public float scanlineSpacing   = 3f;
        [Range(0f, 30f)]  public float scanlineSpeed     = 8f;

        [Header("Film Grain")]
        [Range(0f, 0.5f)] public float grainIntensity    = 0.10f;
        [Range(1f, 8f)]   public float grainSize         = 2f;

        [Header("Scratch Lines")]
        [Range(0f, 1f)]   public float scratchIntensity  = 0.12f;

        [Header("Aged Photo Overlay")]
        [Tooltip("Master control for all overlay effects below. 0 = all off.")]
        [Range(0f, 1f)]   public float overlayStrength   = 0.0f;
        [Tooltip("Fold/crease lines across the photo surface.")]
        [Range(0f, 1f)]   public float creaseIntensity   = 0.6f;
        [Tooltip("Dust spots and damage speckles.")]
        [Range(0f, 1f)]   public float dustIntensity     = 0.5f;
        [Tooltip("Dark burned border around the photo edges.")]
        [Range(0f, 1f)]   public float edgeBurn          = 0.7f;

        [Header("Vignette")]
        [Range(0f, 1f)]   public float vignetteIntensity  = 0.40f;
        [Range(0.1f, 2f)] public float vignetteSmoothness = 0.45f;

        [Header("Chromatic Aberration")]
        [Range(0f, 6f)]   public float chromaticStrength  = 1.2f;

        [Header("Flicker")]
        [Range(0f, 1f)]   public float flickerIntensity   = 0.35f;

        [Header("Letterbox")]
        [Range(0f, 0.2f)] public float letterboxAmount    = 0.0f;
    }

    public Settings settings = new Settings();
    Material _mat;
    FilmFilterPass _pass;

    static readonly int ID_ColorTint          = Shader.PropertyToID("_ColorTint");
    static readonly int ID_TintStrength       = Shader.PropertyToID("_TintStrength");
    static readonly int ID_Saturation         = Shader.PropertyToID("_Saturation");
    static readonly int ID_SepiaStrength      = Shader.PropertyToID("_SepiaStrength");
    static readonly int ID_FadeStrength       = Shader.PropertyToID("_FadeStrength");
    static readonly int ID_ScanlineIntensity  = Shader.PropertyToID("_ScanlineIntensity");
    static readonly int ID_ScanlineSpacing    = Shader.PropertyToID("_ScanlineSpacing");
    static readonly int ID_ScanlineSpeed      = Shader.PropertyToID("_ScanlineSpeed");
    static readonly int ID_GrainIntensity     = Shader.PropertyToID("_GrainIntensity");
    static readonly int ID_GrainSize          = Shader.PropertyToID("_GrainSize");
    static readonly int ID_ScratchIntensity   = Shader.PropertyToID("_ScratchIntensity");
    static readonly int ID_OverlayStrength    = Shader.PropertyToID("_OverlayStrength");
    static readonly int ID_CreaseIntensity    = Shader.PropertyToID("_CreaseIntensity");
    static readonly int ID_DustIntensity      = Shader.PropertyToID("_DustIntensity");
    static readonly int ID_EdgeBurn           = Shader.PropertyToID("_EdgeBurn");
    static readonly int ID_VignetteIntensity  = Shader.PropertyToID("_VignetteIntensity");
    static readonly int ID_VignetteSmoothness = Shader.PropertyToID("_VignetteSmoothness");
    static readonly int ID_ChromaticStrength  = Shader.PropertyToID("_ChromaticStrength");
    static readonly int ID_FlickerIntensity   = Shader.PropertyToID("_FlickerIntensity");
    static readonly int ID_LetterboxAmount    = Shader.PropertyToID("_LetterboxAmount");

    public override void Create()
    {
        _mat = settings.material;

        if (_mat == null)
        {
            Debug.LogWarning("[FilmFilterFeature] No material assigned. " +
                             "Create a material using Custom/URP/FilmFilter and assign it.");
            return;
        }

        _pass = new FilmFilterPass(_mat, settings);
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (_mat == null) return;

        _mat.SetColor(ID_ColorTint,          settings.colorTint);
        _mat.SetFloat(ID_TintStrength,       settings.tintStrength);
        _mat.SetFloat(ID_Saturation,         settings.saturation);
        _mat.SetFloat(ID_SepiaStrength,      settings.sepiaStrength);
        _mat.SetFloat(ID_FadeStrength,       settings.fadeStrength);
        _mat.SetFloat(ID_ScanlineIntensity,  settings.scanlineIntensity);
        _mat.SetFloat(ID_ScanlineSpacing,    settings.scanlineSpacing);
        _mat.SetFloat(ID_ScanlineSpeed,      settings.scanlineSpeed);
        _mat.SetFloat(ID_GrainIntensity,     settings.grainIntensity);
        _mat.SetFloat(ID_GrainSize,          settings.grainSize);
        _mat.SetFloat(ID_ScratchIntensity,   settings.scratchIntensity);
        _mat.SetFloat(ID_OverlayStrength,    settings.overlayStrength);
        _mat.SetFloat(ID_CreaseIntensity,    settings.creaseIntensity);
        _mat.SetFloat(ID_DustIntensity,      settings.dustIntensity);
        _mat.SetFloat(ID_EdgeBurn,           settings.edgeBurn);
        _mat.SetFloat(ID_VignetteIntensity,  settings.vignetteIntensity);
        _mat.SetFloat(ID_VignetteSmoothness, settings.vignetteSmoothness);
        _mat.SetFloat(ID_ChromaticStrength,  settings.chromaticStrength);
        _mat.SetFloat(ID_FlickerIntensity,   settings.flickerIntensity);
        _mat.SetFloat(ID_LetterboxAmount,    settings.letterboxAmount);

        renderer.EnqueuePass(_pass);
    }

    class FilmFilterPass : ScriptableRenderPass
    {
        Material _mat;
        Settings _s;

        public FilmFilterPass(Material m, Settings s) { _mat = m; _s = s; }

        class PassData
        {
            public TextureHandle src;
            public Material      mat;
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer ctx)
        {
            var resourceData = ctx.Get<UniversalResourceData>();

            var desc = rg.GetTextureDesc(resourceData.activeColorTexture);
            desc.name        = "_FilmFilterTmp";
            desc.clearBuffer = false;
            var temp = rg.CreateTexture(desc);

            using (var builder = rg.AddRasterRenderPass<PassData>("FilmFilter", out var data))
            {
                data.src = resourceData.activeColorTexture;
                data.mat = _mat;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(temp, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0));
            }

            using (var builder = rg.AddRasterRenderPass<PassData>("FilmFilter CopyBack", out var data))
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