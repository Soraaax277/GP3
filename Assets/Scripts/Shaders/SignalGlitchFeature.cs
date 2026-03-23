using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

// URP Renderer Feature — coax signal glitch transition.
// Renamed from GlitchFeature to SignalGlitchFeature to avoid conflicts.
//
// SETUP
//   1. Import SignalGlitchTransition.shader into your project.
//   2. Create a new Material using that shader.
//   3. In your URP Renderer asset add this feature and assign the material.
//   4. Leave the feature checkbox UNCHECKED by default — the MainMenuManager
//      enables it at runtime only during a swap.
//
// The only value the manager ever sets is _GlitchProgress (0..1).
// 0 = clean passthrough, 0.5 = peak chaos (swap fires here), 1 = clean.
public class SignalGlitchFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Material using the SignalGlitchTransition shader.")]
        public Material material;
    }

    public Settings settings = new Settings();

    private SignalGlitchPass _pass;

    private static readonly int ID_Progress = Shader.PropertyToID("_GlitchProgress");

    // ── Called by MainMenuManager every frame during a transition ────────────
    public void SetProgress(float t)
    {
        if (settings.material != null)
            settings.material.SetFloat(ID_Progress, Mathf.Clamp01(t));
    }

    // ── ScriptableRendererFeature ─────────────────────────────────────────────
    public override void Create()
    {
        if (settings.material == null)
        {
            Debug.LogWarning("[SignalGlitchFeature] No material assigned — " +
                             "create a material from the SignalGlitchTransition shader.");
            return;
        }

        _pass = new SignalGlitchPass(settings.material);
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null || _pass == null) return;
        renderer.EnqueuePass(_pass);
    }

    // ── Inner pass ────────────────────────────────────────────────────────────
    private class SignalGlitchPass : ScriptableRenderPass
    {
        private readonly Material _mat;

        public SignalGlitchPass(Material mat) { _mat = mat; }

        private class PassData
        {
            public TextureHandle src;
            public Material      mat;
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer ctx)
        {
            var resourceData = ctx.Get<UniversalResourceData>();

            var desc         = rg.GetTextureDesc(resourceData.activeColorTexture);
            desc.name        = "_SignalGlitchTmp";
            desc.clearBuffer = false;
            var tmp          = rg.CreateTexture(desc);

            // Pass 1 — blit through glitch shader into temp
            using (var b = rg.AddRasterRenderPass<PassData>("SignalGlitch_Blit", out var d))
            {
                d.src = resourceData.activeColorTexture;
                d.mat = _mat;
                b.UseTexture(d.src);
                b.SetRenderAttachment(tmp, 0);
                b.SetRenderFunc((PassData pd, RasterGraphContext rc) =>
                    Blitter.BlitTexture(rc.cmd, pd.src, new Vector4(1, 1, 0, 0), pd.mat, 0));
            }

            // Pass 2 — copy temp back to active colour
            using (var b = rg.AddRasterRenderPass<PassData>("SignalGlitch_CopyBack", out var d))
            {
                d.src = tmp;
                b.UseTexture(d.src);
                b.SetRenderAttachment(resourceData.activeColorTexture, 0);
                b.SetRenderFunc((PassData pd, RasterGraphContext rc) =>
                    Blitter.BlitTexture(rc.cmd, pd.src, new Vector4(1, 1, 0, 0), 0, false));
            }
        }
    }
}
