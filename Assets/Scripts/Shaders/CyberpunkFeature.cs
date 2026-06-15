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
        [Range(0.1f, 2f)]    public float exposure          = 1.00f;
        [Range(0.5f, 3f)]    public float contrast          = 1.10f;
        [Range(0f, 1f)]      public float blackCrush        = 0.08f;

        // ─── Brightness Balance ────────────────────────────────────────────────
        // ShadowLift: additive fill into dark regions BEFORE blackCrush.
        // MinLuminance: hard floor applied AFTER all grading.
        // Together these prevent the filter from making the scene unreadably dark.
        [Header("Brightness Balance")]
        [Tooltip("Hard luminance floor after all grading — darkest pixels are raised to at least this value.")]
        [Range(0f, 0.15f)]   public float minLuminance      = 0.04f;
        [Tooltip("Additive shadow fill applied before black crush — softens the darkness floor.")]
        [Range(0f, 0.10f)]   public float shadowLift        = 0.012f;

        // ─── Phosphor Green-Cyan Grade ─────────────────────────────────────────
        [Header("Phosphor Grade")]
        [Range(0f, 2f)]      public float tealShadows       = 0.30f;

        // ─── Neon Effect ───────────────────────────────────────────────────────
        [Header("Neon Effect")]
        [Range(0f, 1f)]      public float neonThreshold     = 0.68f;
        [Range(0f, 2f)]      public float neonBloom         = 0.35f;
        [Range(0f, 3f)]      public float neonSaturation    = 1.10f;
        [Range(0f, 2f)]      public float neonHuePush       = 0.70f;
        [Tooltip("How far the neon can randomly flicker down. 0 = stable, 1 = dramatic drops.")]
        [Range(0f, 1f)]      public float neonFlickerAmt    = 0.12f;

        // ─── Atmosphere ────────────────────────────────────────────────────────
        [Header("Atmosphere")]
        [Range(0f, 2f)]      public float fogDensity        = 0.18f;

        // ─── Optics ────────────────────────────────────────────────────────────
        [Header("Optics")]
        [Range(0f, 6f)]      public float chromaticStrength = 0.70f;
        [Tooltip("Amount the chromatic aberration oscillates in amplitude over time.")]
        [Range(0f, 1f)]      public float caOscillation     = 0.30f;
        [Tooltip("Barrel / wide-angle lens warp applied to the UV before sampling.")]
        [Range(0f, 0.8f)]    public float barrelDistort     = 0.12f;
        [Tooltip("Animated low-frequency UV warp — simulates heat shimmer or signal instability.")]
        [Range(0f, 0.20f)]   public float heatHazeStrength  = 0.06f;
        [Tooltip("Subtle periodic UV zoom — simulates a lens hunting for focus.")]
        [Range(0f, 0.01f)]   public float focusBreathAmt    = 0.003f;

        // ─── Sharpness ─────────────────────────────────────────────────────────
        [Header("Sharpness")]
        [Range(0f, 3f)]      public float sharpness         = 1.20f;

        // ─── Digital Sensor Noise ──────────────────────────────────────────────
        [Header("Digital Sensor Noise")]
        [Range(0f, 0.3f)]    public float grainIntensity    = 0.05f;
        [Range(1f, 4f)]      public float grainSize         = 1.40f;

        // ─── Vignette ──────────────────────────────────────────────────────────
        [Header("Vignette")]
        [Range(0f, 1f)]      public float vignetteIntensity = 0.25f;

        // ─── Neon Outline ──────────────────────────────────────────────────────
        [Header("Neon Outline")]
        [Range(0f, 1f)]      public float outlineIntensity  = 0.45f;
        [Range(0f, 5f)]      public float outlineThickness  = 1.20f;
        public Color         outlineColor  = new Color(0.00f, 1.00f, 0.92f, 1.0f);

        // ─── Sensor Artifacts ──────────────────────────────────────────────────
        [Header("Sensor Artifacts")]
        [Tooltip("Fine horizontal scanlines across the full frame.")]
        [Range(0f, 0.15f)]   public float scanlineIntensity = 0.05f;
        [Tooltip("Scanline density — higher = finer lines.")]
        [Range(100f, 1200f)] public float scanlineDensity   = 600f;
        [Tooltip("Speed the scanlines drift downward over time.")]
        [Range(0f, 1f)]      public float scanDriftSpeed    = 0.40f;
        [Tooltip("Odd/even row field-camera shimmer.")]
        [Range(0f, 1f)]      public float interlaceStrength = 0.18f;

        // ─── Glitch System ─────────────────────────────────────────────────────
        [Header("Glitch System")]
        [Tooltip("Average glitch bursts per second. 0 = disabled.")]
        [Range(0f, 2f)]      public float glitchFrequency   = 0.15f;
        [Tooltip("Peak intensity of each burst — controls tear amplitude, CA spike, and dropout strength.")]
        [Range(0f, 1f)]      public float glitchIntensity   = 0.60f;

        // ─── Camera Reticle ────────────────────────────────────────────────────
        [Header("Camera Reticle")]
        [Range(0f, 1f)]      public float reticleOpacity    = 0.60f;
        public Color         reticleColor  = new Color(0.00f, 0.90f, 0.80f, 1.0f);

        // ─── HUD Data Bars ─────────────────────────────────────────────────────
        [Header("HUD Data Bars")]
        [Range(0f, 1f)]      public float dataBarOpacity    = 0.55f;
        public Color         dataBarColor  = new Color(0.00f, 0.82f, 1.00f, 1.0f);
    }

    public Settings settings = new Settings();

    Material      _mat;
    CyberpunkPass _pass;

    // ─── Glitch event state ───────────────────────────────────────────────────
    float _glitchCooldown = 1.0f;   // time until next glitch attempt (s)
    float _glitchTimer    = 0.0f;   // remaining time of current burst (s)
    float _glitchDuration = 0.0f;   // total duration of burst, for the envelope
    float _glitchSeed     = 0.0f;   // random seed frozen for the burst's lifetime
    float _glitchCurrent  = 0.0f;   // smoothed intensity [0..1] sent to shader

    // ─── Shader property IDs ─────────────────────────────────────────────────
    static readonly int ID_Exposure          = Shader.PropertyToID("_Exposure");
    static readonly int ID_Contrast          = Shader.PropertyToID("_Contrast");
    static readonly int ID_BlackCrush        = Shader.PropertyToID("_BlackCrush");
    static readonly int ID_MinLuminance      = Shader.PropertyToID("_MinLuminance");
    static readonly int ID_ShadowLift        = Shader.PropertyToID("_ShadowLift");
    static readonly int ID_TealShadows       = Shader.PropertyToID("_TealShadows");
    static readonly int ID_NeonThreshold     = Shader.PropertyToID("_NeonThreshold");
    static readonly int ID_NeonBloom         = Shader.PropertyToID("_NeonBloom");
    static readonly int ID_NeonSaturation    = Shader.PropertyToID("_NeonSaturation");
    static readonly int ID_NeonHuePush       = Shader.PropertyToID("_NeonHuePush");
    static readonly int ID_NeonFlickerAmt    = Shader.PropertyToID("_NeonFlickerAmt");
    static readonly int ID_FogDensity        = Shader.PropertyToID("_FogDensity");
    static readonly int ID_ChromaticStrength = Shader.PropertyToID("_ChromaticStrength");
    static readonly int ID_CaOscillation     = Shader.PropertyToID("_CaOscillation");
    static readonly int ID_BarrelDistort     = Shader.PropertyToID("_BarrelDistort");
    static readonly int ID_HeatHazeStrength  = Shader.PropertyToID("_HeatHazeStrength");
    static readonly int ID_FocusBreathAmt    = Shader.PropertyToID("_FocusBreathAmt");
    static readonly int ID_Sharpness         = Shader.PropertyToID("_Sharpness");
    static readonly int ID_GrainIntensity    = Shader.PropertyToID("_GrainIntensity");
    static readonly int ID_GrainSize         = Shader.PropertyToID("_GrainSize");
    static readonly int ID_VignetteIntensity = Shader.PropertyToID("_VignetteIntensity");
    static readonly int ID_OutlineIntensity  = Shader.PropertyToID("_OutlineIntensity");
    static readonly int ID_OutlineThickness  = Shader.PropertyToID("_OutlineThickness");
    static readonly int ID_OutlineColor      = Shader.PropertyToID("_OutlineColor");
    static readonly int ID_ScanlineIntensity = Shader.PropertyToID("_ScanlineIntensity");
    static readonly int ID_ScanlineDensity   = Shader.PropertyToID("_ScanlineDensity");
    static readonly int ID_ScanDriftSpeed    = Shader.PropertyToID("_ScanDriftSpeed");
    static readonly int ID_InterlaceStrength = Shader.PropertyToID("_InterlaceStrength");
    static readonly int ID_GlitchIntensity   = Shader.PropertyToID("_GlitchIntensity");
    static readonly int ID_GlitchSeed        = Shader.PropertyToID("_GlitchSeed");
    static readonly int ID_ReticleOpacity    = Shader.PropertyToID("_ReticleOpacity");
    static readonly int ID_ReticleColor      = Shader.PropertyToID("_ReticleColor");
    static readonly int ID_DataBarOpacity    = Shader.PropertyToID("_DataBarOpacity");
    static readonly int ID_DataBarColor      = Shader.PropertyToID("_DataBarColor");

    // ─── Create ───────────────────────────────────────────────────────────────
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

    // ─── Glitch tick — called once per AddRenderPasses ───────────────────────
    // Drives a state machine: idle → wait for cooldown → burst → idle.
    // The burst uses a trapezoid amplitude envelope (ramp-up, hold, ramp-down)
    // and randomises both its duration and inter-burst interval so events feel
    // organic rather than metronomic.
    void TickGlitch()
    {
        float dt = Time.unscaledDeltaTime;

        if (_glitchTimer > 0.0f)
        {
            // Active burst — compute envelope
            _glitchTimer -= dt;
            float t   = 1.0f - Mathf.Clamp01(_glitchTimer / _glitchDuration);
            float env = t < 0.20f ? (t / 0.20f)
                      : t > 0.70f ? ((1.0f - t) / 0.30f)
                      : 1.0f;
            _glitchCurrent = env * settings.glitchIntensity;
        }
        else
        {
            _glitchCurrent = 0.0f;
            _glitchCooldown -= dt;

            if (_glitchCooldown <= 0.0f)
            {
                float freq         = Mathf.Max(0.01f, settings.glitchFrequency);
                float meanInterval = 1.0f / freq;

                // Randomise interval: 40% – 160% of the mean
                _glitchCooldown = Random.Range(meanInterval * 0.40f,
                                               meanInterval * 1.60f);

                // 70% of checks actually fire; the rest are near-misses
                if (settings.glitchFrequency > 0.001f && Random.value < 0.70f)
                {
                    _glitchDuration = Random.Range(0.04f, 0.16f);
                    _glitchTimer    = _glitchDuration;
                    _glitchSeed     = Random.value;
                }
            }
        }
    }

    // ─── AddRenderPasses ──────────────────────────────────────────────────────
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (_mat == null) return;

        TickGlitch();

        _mat.SetFloat(ID_Exposure,          settings.exposure);
        _mat.SetFloat(ID_Contrast,          settings.contrast);
        _mat.SetFloat(ID_BlackCrush,        settings.blackCrush);
        _mat.SetFloat(ID_MinLuminance,      settings.minLuminance);
        _mat.SetFloat(ID_ShadowLift,        settings.shadowLift);
        _mat.SetFloat(ID_TealShadows,       settings.tealShadows);
        _mat.SetFloat(ID_NeonThreshold,     settings.neonThreshold);
        _mat.SetFloat(ID_NeonBloom,         settings.neonBloom);
        _mat.SetFloat(ID_NeonSaturation,    settings.neonSaturation);
        _mat.SetFloat(ID_NeonHuePush,       settings.neonHuePush);
        _mat.SetFloat(ID_NeonFlickerAmt,    settings.neonFlickerAmt);
        _mat.SetFloat(ID_FogDensity,        settings.fogDensity);
        _mat.SetFloat(ID_ChromaticStrength, settings.chromaticStrength);
        _mat.SetFloat(ID_CaOscillation,     settings.caOscillation);
        _mat.SetFloat(ID_BarrelDistort,     settings.barrelDistort);
        _mat.SetFloat(ID_HeatHazeStrength,  settings.heatHazeStrength);
        _mat.SetFloat(ID_FocusBreathAmt,    settings.focusBreathAmt);
        _mat.SetFloat(ID_Sharpness,         settings.sharpness);
        _mat.SetFloat(ID_GrainIntensity,    settings.grainIntensity);
        _mat.SetFloat(ID_GrainSize,         settings.grainSize);
        _mat.SetFloat(ID_VignetteIntensity, settings.vignetteIntensity);
        _mat.SetFloat(ID_OutlineIntensity,  settings.outlineIntensity);
        _mat.SetFloat(ID_OutlineThickness,  settings.outlineThickness);
        _mat.SetColor(ID_OutlineColor,      settings.outlineColor);
        _mat.SetFloat(ID_ScanlineIntensity, settings.scanlineIntensity);
        _mat.SetFloat(ID_ScanlineDensity,   settings.scanlineDensity);
        _mat.SetFloat(ID_ScanDriftSpeed,    settings.scanDriftSpeed);
        _mat.SetFloat(ID_InterlaceStrength, settings.interlaceStrength);
        _mat.SetFloat(ID_GlitchIntensity,   _glitchCurrent);
        _mat.SetFloat(ID_GlitchSeed,        _glitchSeed);
        _mat.SetFloat(ID_ReticleOpacity,    settings.reticleOpacity);
        _mat.SetColor(ID_ReticleColor,      settings.reticleColor);
        _mat.SetFloat(ID_DataBarOpacity,    settings.dataBarOpacity);
        _mat.SetColor(ID_DataBarColor,      settings.dataBarColor);

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

            var desc         = rg.GetTextureDesc(resourceData.activeColorTexture);
            desc.name        = "_CyberpunkTmp";
            desc.clearBuffer = false;
            var temp         = rg.CreateTexture(desc);

            // Pass 1: blit active color → temp through the cyberpunk shader
            using (var builder = rg.AddRasterRenderPass<PassData>("CyberpunkFilter", out var data))
            {
                data.src = resourceData.activeColorTexture;
                data.mat = _mat;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(temp, 0);

                // Required so Blitter can call cmd.SetGlobalTexture("_BlitTexture")
                // internally. Without this the render graph strips the global-state
                // write and _BlitTexture is unbound, making the shader sample (0,0,0).
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0));
            }

            // Pass 2: copy temp back to the active color buffer
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