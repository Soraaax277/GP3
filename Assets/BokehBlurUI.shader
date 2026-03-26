Shader "Custom/URP/BokehBlurUI"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Bokeh Blur)]
        _BlurSize      ("Blur Size",      Range(0.0, 8.0)) = 2.0
        _Darkness      ("Darkness",       Range(0.0, 1.0)) = 0.35
        _TintColor     ("Tint Color",     Color)            = (0,0,0,0)
        _TintStrength  ("Tint Strength",  Range(0.0, 1.0)) = 0.0
        _ApertureSides ("Aperture Sides", Range(3, 32))     = 6
        _SampleCount   ("Sample Count",   Range(8, 64))     = 32

        [Header(Scan Lines)]
        _ScanlineIntensity ("Scanline Intensity", Range(0.0, 1.0))  = 0.25
        _ScanlineCount     ("Scanline Count",     Range(100, 2000)) = 600

        [Header(Noise and Grain)]
        _GrainIntensity ("Grain Intensity", Range(0.0, 0.5)) = 0.08
        _NoiseIntensity ("Noise Intensity", Range(0.0, 1.0)) = 0.06

        [Header(Signal Disruption)]
        _ChromaticStrength ("Chromatic Split",    Range(0.0, 8.0)) = 2.5
        _JitterIntensity   ("Jitter Intensity",   Range(0.0, 1.0)) = 0.15
        _JitterSpeed       ("Jitter Speed",       Range(0.0, 8.0)) = 3.0
        _RollIntensity     ("Signal Roll",        Range(0.0, 1.0)) = 0.08
        _RollSpeed         ("Roll Speed",         Range(0.0, 4.0)) = 0.5
        _BlockGlitch       ("Block Glitch",       Range(0.0, 1.0)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Overlay"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off  ZWrite Off  ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BokehSourceTex);
            SAMPLER(sampler_BokehSourceTex);
            float4 _BokehSourceTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _BlurSize;
                float  _Darkness;
                float4 _TintColor;
                float  _TintStrength;
                float  _ApertureSides;
                float  _SampleCount;

                float  _ScanlineIntensity;
                float  _ScanlineCount;

                float  _GrainIntensity;
                float  _NoiseIntensity;

                float  _ChromaticStrength;
                float  _JitterIntensity;
                float  _JitterSpeed;
                float  _RollIntensity;
                float  _RollSpeed;
                float  _BlockGlitch;
                float  _ManualTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos   : TEXCOORD0;
                float4 color       : COLOR;
            };

            #define PI     3.14159265359
            #define TWO_PI 6.28318530718

            // ── Hash / noise ─────────────────────────────────────────────────
            float hash1(float  n) { return frac(sin(n) * 43758.5453123); }
            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // Value noise 0..1
            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash2(i);
                float b = hash2(i + float2(1, 0));
                float c = hash2(i + float2(0, 1));
                float d = hash2(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // ── Bokeh helpers ────────────────────────────────────────────────
            float2 VogelSample(int i, int total)
            {
                float r     = sqrt(float(i) + 0.5) / sqrt(float(total));
                float theta = float(i) * 2.39996323;
                return float2(cos(theta), sin(theta)) * r;
            }

            float2 ApertureClip(float2 p, float sides)
            {
                float len = length(p);
                if (len < 0.0001) return p;
                float angle    = atan2(p.y, p.x);
                float sector   = TWO_PI / sides;
                float nearest  = round(angle / sector) * sector;
                float apothem  = cos(PI / sides);
                float edgeDist = apothem / max(cos(angle - nearest), 1e-5);
                return (p / len) * min(len, edgeDist);
            }

            // ── Bokeh blur ───────────────────────────────────────────────────
            float4 BokehBlur(float2 uv)
            {
                if (_BlurSize <= 0.001)
                    return SAMPLE_TEXTURE2D(_BokehSourceTex, sampler_BokehSourceTex, uv);

                float2 step   = _BokehSourceTex_TexelSize.xy * _BlurSize * 3.0;
                int    samples = clamp((int)_SampleCount, 8, 64);
                float  sides   = max(_ApertureSides, 3.0);
                float4 col     = 0;

                for (int i = 0; i < samples; i++)
                {
                    float2 offset = VogelSample(i, samples);
                    offset        = ApertureClip(offset, sides);
                    col += SAMPLE_TEXTURE2D(_BokehSourceTex, sampler_BokehSourceTex,
                                           uv + offset * step);
                }
                return col / float(samples);
            }

            // ── Vertex ───────────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos   = ComputeScreenPos(OUT.positionHCS);
                OUT.color       = IN.color;
                return OUT;
            }

            // ── Fragment ─────────────────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.screenPos.xy / IN.screenPos.w;
                float  time = _ManualTime;

                // ── Signal roll: slow vertical UV drift ──────────────────────
                // Looks like a TV losing vertical sync
                float roll     = sin(uv.y * 3.0 + time * _RollSpeed) * _RollIntensity * 0.005;
                uv.x          += roll;

                // ── Horizontal jitter on random scan bands ───────────────────
                // Picks a random row band each frame and shifts it horizontally
                float jitterRow   = floor(uv.y * 80.0);
                float jitterTime  = floor(time * _JitterSpeed);
                float jitterRand  = hash2(float2(jitterRow, jitterTime));
                float jitterBand  = step(0.92, jitterRand); // ~8% of bands jitter
                float jitterAmt   = (hash2(float2(jitterRow + 0.5, jitterTime)) * 2.0 - 1.0)
                                    * _JitterIntensity * 0.02 * jitterBand;
                uv.x             += jitterAmt;

                // ── Block glitch: rare large horizontal slice displacement ────
                float blockRow  = floor(uv.y * 24.0);
                float blockTime = floor(time * 1.3);
                float blockRand = hash2(float2(blockRow * 7.3, blockTime));
                float isBlock   = step(1.0 - _BlockGlitch * 0.25, blockRand);
                float blockShift = (hash2(float2(blockRow, blockTime + 0.5)) * 2.0 - 1.0)
                                   * 0.04 * isBlock;
                float2 glitchUV = uv + float2(blockShift, 0);

                // ── Chromatic split on glitchUV ──────────────────────────────
                float2 caOffset = float2(_ChromaticStrength * _BokehSourceTex_TexelSize.x, 0);
                float4 colR = BokehBlur(glitchUV + caOffset);
                float4 colG = BokehBlur(glitchUV);
                float4 colB = BokehBlur(glitchUV - caOffset);
                float4 col  = float4(colR.r, colG.g, colB.b, 1.0);

                // ── Darken ───────────────────────────────────────────────────
                col.rgb *= (1.0 - _Darkness);

                // ── Tint ─────────────────────────────────────────────────────
                col.rgb = lerp(col.rgb, _TintColor.rgb, _TintStrength);

                // ── Scan lines ───────────────────────────────────────────────
                // Animate slowly downward like a real CRT interference pattern
                float scanScroll = time * 0.08;
                float scan       = sin((uv.y + scanScroll) * _ScanlineCount * PI);
                scan             = scan * 0.5 + 0.5;          // 0..1
                scan             = lerp(1.0, scan, _ScanlineIntensity);
                col.rgb         *= scan;

                // Thin bright lines (every ~60px) — telecom carrier lines
                float carrierLine = 1.0 - smoothstep(0.0, 0.002,
                                    abs(frac(uv.y * 18.0 + time * 0.04) - 0.5) - 0.48);
                col.rgb += carrierLine * 0.06 * _ScanlineIntensity;

                // ── Noise & grain ────────────────────────────────────────────
                // Static noise (block-style, like signal dropout)
                float2 noisePx = floor(uv * float2(
                    _BokehSourceTex_TexelSize.z,
                    _BokehSourceTex_TexelSize.w) / 2.0);
                float  noiseT  = floor(time * 24.0);
                float  staticN = hash2(noisePx + noiseT * 17.3) * 2.0 - 1.0;
                col.rgb       += staticN * _NoiseIntensity;

                // Fine film grain (per pixel, per frame)
                float2 grainPx = floor(uv * float2(
                    _BokehSourceTex_TexelSize.z,
                    _BokehSourceTex_TexelSize.w));
                float  grainT  = frac(time * 0.073);
                float  grain   = hash2(grainPx + grainT * 431.7) * 2.0 - 1.0;
                col.rgb       += grain * _GrainIntensity;

                return float4(saturate(col.rgb), IN.color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
