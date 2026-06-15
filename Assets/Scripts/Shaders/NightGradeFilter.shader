Shader "Custom/URP/NightGradeFilter"
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

            // ── Exposure & Contrast ───────────────────────────────────────────
            float  _Exposure;
            float  _Contrast;
            float  _BlackCrush;
            float  _HighlightRolloff;

            // ── Saturation ────────────────────────────────────────────────────
            float  _Saturation;

            // ── Teal-Orange 3-Way Grade ───────────────────────────────────────
            float  _ShadowTealStrength;
            float  _MidtoneBalance;
            float  _HighlightWarmth;
            float  _ShadowLift;

            // ── H.264 Chroma Smear ────────────────────────────────────────────
            float  _ChromaSmear;

            // ── Sharpness ─────────────────────────────────────────────────────
            float  _Sharpness;

            // ── Digital Noise ─────────────────────────────────────────────────
            float  _NoiseIntensity;
            float  _NoiseSize;

            // ── Vignette ──────────────────────────────────────────────────────
            float  _VignetteIntensity;

            // ── Letterbox ─────────────────────────────────────────────────────
            float  _LetterboxAmount;

            // ─────────────────────────────────────────────────────────────────
            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Luma(float3 c)  { return dot(c, float3(0.299, 0.587, 0.114)); }

            // NOTE ON COLOR SPACE:
            // This pass runs at AfterRenderingPostProcessing — URP's tonemapper has
            // already executed, so the active color buffer is in display/gamma space.
            // Applying any gamma conversion here would double-encode and blow out mids.
            // All operations run directly in the buffer's native space.

            // Unsharp mask — gentle edge pop
            float3 Sharpen(float2 uv, float3 col, float strength)
            {
                float2 tx  = _BlitTexture_TexelSize.xy;
                float3 n   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,  tx.y)).rgb;
                float3 s   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, -tx.y)).rgb;
                float3 e   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( tx.x, 0)).rgb;
                float3 w   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-tx.x, 0)).rgb;
                float3 blr = (n + s + e + w) * 0.25;
                return saturate(col + (col - blr) * strength);
            }

            // H.264 horizontal chroma smear in dark areas
            float3 ChromaSmear(float2 uv, float3 col, float amount)
            {
                if (amount <= 0.001) return col;
                float  darkMask = 1.0 - smoothstep(0.0, 0.5, Luma(col));
                float  offset   = _BlitTexture_TexelSize.x * amount * 3.0;
                float3 left1    = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(offset,       0)).rgb;
                float3 left2    = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(offset * 2.0, 0)).rgb;
                float3 smeared  = (col + left1 + left2) / 3.0;
                float  lumaOrig = Luma(col);
                float3 result   = smeared + (lumaOrig - Luma(smeared));
                return lerp(col, result, darkMask * amount);
            }

            // Smooth S-curve contrast (perceptual space)
            float3 SCurve(float3 c, float strength)
            {
                c = c - 0.5;
                c = c / (1.0 + abs(c) * (strength - 1.0) * 1.5);
                return c + 0.5;
            }

            // DSLR-style highlight shoulder — smooth roll-off, no hard clip
            float3 HighlightRolloff(float3 c, float strength)
            {
                float3 rolled = 1.0 - exp(-c * (1.0 + strength * 2.0));
                float  lumaC  = Luma(c);
                float  hiMask = smoothstep(0.60, 1.0, lumaC);
                return lerp(c, rolled, hiMask * strength);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.texcoord;
                float  time = _Time.y;

                // ── Letterbox ─────────────────────────────────────────────────
                float halfBar = _LetterboxAmount * 0.5;
                if (uv.y < halfBar || uv.y > (1.0 - halfBar))
                    return float4(0, 0, 0, 1);

                // ── Base sample (URP buffer is linear — no GammaToLinear here) ─
                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // 1. Sharpness — slight edge pop
                if (_Sharpness > 0.001)
                    col = Sharpen(uv, col, _Sharpness);

                // 2. H.264 chroma smear
                col = ChromaSmear(uv, col, _ChromaSmear);

                // 3. Exposure
                col *= _Exposure;

                // 4. Highlight roll-off before saturation to avoid hue shift on clipped highlights
                if (_HighlightRolloff > 0.001)
                    col = HighlightRolloff(col, _HighlightRolloff);

                col = saturate(col);

                // 5. Saturation
                float luma = Luma(col);
                col = lerp(float3(luma, luma, luma), col, _Saturation);

                // ── Tonal Masks ───────────────────────────────────────────────
                luma = Luma(col);

                // Dark-scene adaptive factor:
                // When luma is very low everywhere, we have no real highlights so the
                // teal push would dominate the whole image. sceneAdapt scales the
                // shadow effect down so it stays relative, not absolute.
                float sceneAdapt  = smoothstep(0.0, 0.30, luma);

                // Wider shadow/highlight ranges so midtones stay neutral
                float shadowMask  = (1.0 - smoothstep(0.0, 0.50, luma));
                // Dampen teal push when the whole scene is dark
                float tealMask    = shadowMask * saturate(sceneAdapt * 1.8 + 0.25);
                float highMask    = smoothstep(0.55, 1.0, luma);
                float midMask     = saturate(1.0 - shadowMask - highMask);

                // 6. Shadow: teal-GREEN (r drain, green lead, subtle blue)
                //    Old ratio: r-0.07, g+0.03, b+0.09  → blue-dominant teal (wrong)
                //    New ratio: r-0.07, g+0.07, b+0.03  → proper green-teal / cyan
                col.r -= tealMask * _ShadowTealStrength * 0.07;
                col.g += tealMask * _ShadowTealStrength * 0.07;
                col.b += tealMask * _ShadowTealStrength * 0.03;

                // 7. Shadow lift — raised blacks, prevents dark-scene collapse
                //    darkFloor only fires for pixels near pure black (< 0.06 luma)
                float darkFloor = max(0.0, 0.06 - luma) * 0.30;
                col += (shadowMask * _ShadowLift * 0.055) + darkFloor;

                // 8. Midtone temperature
                float midShift = (_MidtoneBalance - 1.0) * 0.07;
                col.r += midMask * midShift;
                col.b -= midMask * midShift * 0.5;

                // 9. Highlight warmth — orange/amber push on bright areas
                col.r += highMask * _HighlightWarmth * 0.10;
                col.g += highMask * _HighlightWarmth * 0.03;
                col.b -= highMask * _HighlightWarmth * 0.09;

                col = saturate(col);

                // 10. S-curve contrast — operates directly in buffer space
                col = SCurve(col, _Contrast);

                // 11. Gentle black crush
                col = max(0.0, col - _BlackCrush * 0.020);

                // 12. Fine digital sensor noise — luminance only, fades at extremes
                if (_NoiseIntensity > 0.001)
                {
                    float2 gp   = floor(uv / (_NoiseSize * _BlitTexture_TexelSize.x));
                    float  seed = frac(time * 0.07193);
                    float  nr   = hash2(gp + seed * 317.5) * 2.0 - 1.0;
                    float  lumaG = Luma(col);
                    float  nMask = smoothstep(0.0, 0.2, lumaG) * (1.0 - smoothstep(0.75, 1.0, lumaG));
                    col += nr * _NoiseIntensity * nMask;
                }

                // 13. Soft oval vignette — wider than tall, very gentle
                if (_VignetteIntensity > 0.001)
                {
                    float2 uvC = uv - 0.5;
                    uvC.x     *= 0.72;
                    float  dist = dot(uvC, uvC) * 4.0;
                    float  vig  = 1.0 - smoothstep(0.25, 1.5, dist) * _VignetteIntensity;
                    col        *= vig;
                }

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
