Shader "Custom/URP/SignalGlitchTransition"
{
    Properties { }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // 0 = clean passthrough   0.5 = peak chaos   1 = clean passthrough
            float _GlitchProgress;

            // ── Hash helpers ─────────────────────────────────────────────────
            float  Hash1(float  n) { return frac(sin(n)                         * 43758.5453); }
            float  Hash2(float2 p) { return frac(sin(dot(p, float2(127.1,311.7)))* 43758.5453); }

            // ── Intensity curve ───────────────────────────────────────────────
            // Triangle wave: 0 at t=0, peaks 1.0 at t=0.5, back to 0 at t=1.
            // pow(,0.5) sharpens the peak so chaos hits hard and clears fast.
            float Intensity(float t)
            {
                float tri = 1.0 - abs(t * 2.0 - 1.0);
                return pow(saturate(tri), 0.5);
            }

            // ── Horizontal tear bands ─────────────────────────────────────────
            // Slabs of the image jump left/right — classic coax dropout.
            float2 TearBands(float2 uv, float intensity, float time)
            {
                float bandH  = 0.05 + Hash1(floor(time * 13.0)) * 0.07;
                float band   = floor(uv.y / bandH);
                float seed   = Hash1(band * 31.7 + floor(time * 19.0) * 5.3);
                float active = step(1.0 - intensity * 0.8, seed);
                float shift  = (seed * 2.0 - 1.0) * 0.09 * intensity * active;

                // Fine sub-pixel jitter on top
                float band2  = floor(uv.y / 0.01);
                float jitter = (Hash1(band2 * 7.1 + time * 37.0) * 2.0 - 1.0)
                             * 0.004 * intensity;

                return float2(saturate(uv.x + shift + jitter), uv.y);
            }

            // ── RGB channel split ─────────────────────────────────────────────
            // Desynced composite — channels drift apart horizontally at peak.
            float3 ChannelSplit(float2 uv, float intensity)
            {
                float s = intensity * 0.04;
                float r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                              float2(uv.x + s, uv.y)).r;
                float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                              uv).g;
                float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                              float2(uv.x - s, uv.y)).b;
                return float3(r, g, b);
            }

            // ── Vertical hold slip ────────────────────────────────────────────
            // A brightness wave rolls down — analogue vertical sync loss.
            float ScanRoll(float2 uv, float intensity, float time)
            {
                float pos  = frac(time * 2.1);
                float dist = abs(frac(uv.y - pos) - 0.5) * 2.0;
                float band = 1.0 - smoothstep(0.0, 0.15, dist);
                return band * 0.4 * intensity;
            }

            // ── Static burst ──────────────────────────────────────────────────
            // White-noise frame at peak — the "signal lost" moment.
            // The GO + filter swap fires exactly here, completely hidden.
            float3 StaticBurst(float2 uv, float intensity, float time)
            {
                float burst = smoothstep(0.72, 1.0, intensity);
                float2 p    = floor(uv * float2(320.0, 240.0));
                float  n    = Hash2(p + frac(time * 113.0));
                return float3(n, n, n) * burst;
            }

            // ── Edge crush ────────────────────────────────────────────────────
            // Vignette + desaturate at borders — signal bleeding out.
            float3 EdgeCrush(float2 uv, float3 col, float intensity)
            {
                float2 e     = uv * (1.0 - uv.yx);
                float  vign  = pow(saturate(e.x * e.y * 8.0), 0.3);
                float  crush = (1.0 - vign) * intensity * 0.65;
                float  luma  = dot(col, float3(0.299, 0.587, 0.114));
                col  = lerp(col, float3(luma, luma, luma), crush);
                col *= (1.0 - crush * 0.45);
                return col;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float  t         = _GlitchProgress;
                float  intensity = Intensity(t);
                float  time      = _Time.y;
                float2 uv        = IN.texcoord;

                // Early out — no cost at clean endpoints
                if (intensity < 0.005)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // 1. Tear bands
                float2 tornUV = TearBands(uv, intensity, time);

                // 2. RGB channel split
                float3 col = ChannelSplit(tornUV, intensity);

                // 3. Scan roll brightness wave
                col += ScanRoll(uv, intensity, time);

                // 4. Static burst near peak (covers the hard swap)
                float3 stat  = StaticBurst(uv, intensity, time);
                col = lerp(col, stat, smoothstep(0.55, 1.0, intensity));

                // 5. Edge crush
                col = EdgeCrush(uv, col, intensity);

                // 6. Blend with original — low intensity = subtle, peak = full override
                float3 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                col = lerp(original, col, intensity);

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
