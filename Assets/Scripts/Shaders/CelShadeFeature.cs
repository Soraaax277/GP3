using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CelShadeFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;

        [Header("Posterization")]
        [Tooltip("Number of discrete color bands. 3-5 = BotW feel. Lower = more aggressive.")]
        [Range(2f, 16f)]  public float posterizeSteps     = 5f;
        [Tooltip("How strongly posterization is applied. 0 = off, 1 = full.")]
        [Range(0f, 1f)]   public float posterizeStrength  = 0.75f;

        [Header("Saturation")]
        [Tooltip("Boosts color vibrancy to compensate for flat banding.")]
        [Range(0f, 1f)]   public float saturationBoost    = 0.25f;

        [Header("Outlines")]
        [Tooltip("Outline width in pixels.")]
        [Range(0f, 4f)]   public float outlineThickness   = 1.0f;
        [Tooltip("How sensitive depth edges are. Lower = more outlines.")]
        [Range(0f, 1f)]   public float depthThreshold     = 0.20f;
        [Tooltip("Multiplier on depth gradient strength.")]
        [Range(0f, 20f)]  public float depthScale         = 8.0f;
        [Tooltip("How sensitive normal edges are. Lower = more outlines on curved surfaces.")]
        [Range(0f, 1f)]   public float normalThreshold    = 0.25f;
        [Tooltip("Outline color and opacity (alpha controls blend strength).")]
        [ColorUsage(false, false)]
        public Color outlineColor = new Color(0.15f, 0.15f, 0.18f);
        [Range(0f, 1f)]   public float outlineOpacity       = 0.70f;

        [Header("Depth Limits (stops halo around island edges)")]
        [Tooltip("Pixels with depth below this are skipped for outlines (water, sky, far terrain). 0=near, 1=far in NDC.")]
        [Range(0f, 1f)]   public float outlineMaxDepth      = 0.001f;
        [Tooltip("Max depth difference between a pixel and its neighbours before the edge is ignored as a silhouette.")]
        [Range(0f, 0.1f)] public float outlineMaxDepthDelta = 0.002f;
    }

    public Settings settings = new Settings();
    Material _mat;
    CelShadePass _pass;

    static readonly int ID_PosterizeSteps         = Shader.PropertyToID("_PosterizeSteps");
    static readonly int ID_PosterizeStrength      = Shader.PropertyToID("_PosterizeStrength");
    static readonly int ID_SaturationBoost        = Shader.PropertyToID("_SaturationBoost");
    static readonly int ID_OutlineMaxDepth        = Shader.PropertyToID("_OutlineMaxDepth");
    static readonly int ID_OutlineMaxDepthDelta   = Shader.PropertyToID("_OutlineMaxDepthDelta");
    static readonly int ID_OutlineThickness       = Shader.PropertyToID("_OutlineThickness");
    static readonly int ID_OutlineDepthThreshold  = Shader.PropertyToID("_OutlineDepthThreshold");
    static readonly int ID_OutlineDepthScale      = Shader.PropertyToID("_OutlineDepthScale");
    static readonly int ID_OutlineNormalThreshold = Shader.PropertyToID("_OutlineNormalThreshold");
    static readonly int ID_OutlineColor           = Shader.PropertyToID("_OutlineColor");

    public override void Create()
    {
        _mat = settings.material;

        if (_mat == null)
        {
            Debug.LogWarning("[CelShadeFeature] No material assigned. " +
                             "Create a material using Custom/URP/CelShadeFilter and assign it.");
            return;
        }

        _pass = new CelShadePass(_mat);
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (_mat == null) return;

        Color oc = settings.outlineColor;
        _mat.SetFloat(ID_PosterizeSteps,         settings.posterizeSteps);
        _mat.SetFloat(ID_PosterizeStrength,      settings.posterizeStrength);
        _mat.SetFloat(ID_SaturationBoost,        settings.saturationBoost);
        _mat.SetFloat(ID_OutlineThickness,       settings.outlineThickness);
        _mat.SetFloat(ID_OutlineDepthThreshold,  settings.depthThreshold);
        _mat.SetFloat(ID_OutlineDepthScale,      settings.depthScale);
        _mat.SetFloat(ID_OutlineNormalThreshold, settings.normalThreshold);
        _mat.SetVector(ID_OutlineColor,          new Vector4(oc.r, oc.g, oc.b, settings.outlineOpacity));
        _mat.SetFloat(ID_OutlineMaxDepth,        settings.outlineMaxDepth);
        _mat.SetFloat(ID_OutlineMaxDepthDelta,   settings.outlineMaxDepthDelta);

        renderer.EnqueuePass(_pass);
    }

    class CelShadePass : ScriptableRenderPass
    {
        Material _mat;
        public CelShadePass(Material m) { _mat = m; }

        class PassData
        {
            public TextureHandle src;
            public Material      mat;
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer ctx)
        {
            var resourceData = ctx.Get<UniversalResourceData>();

            var desc = rg.GetTextureDesc(resourceData.activeColorTexture);
            desc.name        = "_CelShadeTmp";
            desc.clearBuffer = false;
            var temp = rg.CreateTexture(desc);

            using (var builder = rg.AddRasterRenderPass<PassData>("CelShadeFilter", out var data))
            {
                data.src = resourceData.activeColorTexture;
                data.mat = _mat;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(temp, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0));
            }

            using (var builder = rg.AddRasterRenderPass<PassData>("CelShadeFilter CopyBack", out var data))
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