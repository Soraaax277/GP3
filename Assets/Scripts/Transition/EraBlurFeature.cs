using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// URP Renderer Feature that applies a two-pass Gaussian blur + darkening
// to the camera output. Only runs when EraBlurFeature.IsActive is true.
//
// SETUP:
//   1. In Project window, find your URP Renderer Data asset
//      (Project Settings → Graphics → Scriptable Render Pipeline Settings →
//       click the asset → find the Renderer List → click the Renderer asset).
//   2. In the Renderer asset Inspector, click "Add Renderer Feature" → EraBlurFeature.
//   3. Assign the EraBlur material (create a Material using Custom/URP/EraBlur shader).
//   4. Leave BlurSize and Darkness at defaults — EraAnnouncementController tweens them.
public class EraBlurFeature : ScriptableRendererFeature
{
    // Static toggle — EraAnnouncementController flips this
    public static bool IsActive = false;

    [System.Serializable]
    public class Settings
    {
        [Tooltip("Material using the Custom/URP/EraBlur shader.")]
        public Material blurMaterial;
        [Range(0f, 10f)]
        public float blurSize    = 3f;
        [Range(0f, 1f)]
        public float darkness    = 0.4f;
    }

    public Settings settings = new Settings();

    private EraBlurPass _pass;

    protected override void Dispose(bool disposing)
    {
        IsActive = false;
        _pass?.Dispose();
    }

    public override void Create()
    {
        // Always start inactive — EraAnnouncementController enables it when needed
        IsActive = false;

        _pass = new EraBlurPass(settings)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!IsActive) return;
        if (settings.blurMaterial == null) return;
        renderer.EnqueuePass(_pass);
    }

    // ── Render Pass ───────────────────────────────────────────────────────────

    class EraBlurPass : ScriptableRenderPass
    {
        private Settings          _settings;
        private RTHandle          _tempRT;
        private const string      ProfilerTag = "EraBlur";

        public EraBlurPass(Settings settings) { _settings = settings; }

        [System.Obsolete]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _tempRT, desc, FilterMode.Bilinear, name: "_EraBlurTemp");
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_settings.blurMaterial == null) return;

            // Properties are set directly on the material by EraAnnouncementController.SetBlur()
            // so we do NOT override them here — just blit with whatever values are on the material.

            CommandBuffer cmd = CommandBufferPool.Get(ProfilerTag);

            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Horizontal pass: source → tempRT
            Blitter.BlitCameraTexture(cmd, source, _tempRT, _settings.blurMaterial, 0);
            // Vertical pass:   tempRT → source
            Blitter.BlitCameraTexture(cmd, _tempRT, source, _settings.blurMaterial, 1);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }

        public void Dispose() { _tempRT?.Release(); }
    }
}