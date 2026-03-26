using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// URP Renderer Feature that applies a two-pass Gaussian blur + darkening
// to the camera output. 
public class EraBlurFeature : ScriptableRendererFeature
{
    // Static toggle — Settings/Pause menus flip this to true/false
    public static bool IsActive = false;

    // Fixed values for when the menus are open
    private static float _targetBlurSize = 3.5f;
    private static float _targetDarkness = 0.45f;

    [System.Serializable]
    public class Settings
    {
        [Tooltip("Material using the Custom/URP/EraBlur shader.")]
        public Material blurMaterial;
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
        IsActive = false; // Start inactive
        _pass = new EraBlurPass(settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!IsActive) return;
        if (settings.blurMaterial == null) return;

        // Forcefully set the blur values while the menus are active
        settings.blurMaterial.SetFloat("_BlurSize", _targetBlurSize);
        settings.blurMaterial.SetFloat("_Darkness", _targetDarkness);

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