Shader "Custom/URP/CyberpunkFilter"
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

            float  _Exposure;
            float  _Contrast;
            float  _BlackCrush;
            float  _TealShadows;
            float  _TealMidtones;
            float  _NeonBloom;
            float  _NeonThreshold;
            float  _NeonSaturation;
            float  _NeonHuePush;
            float  _FogDensity;
            float  _Sharpness;
            float  _GrainIntensity;
            float  _GrainSize;
            float  _VignetteIntensity;
            float  _ChromaticStrength;

            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float3 GammaToLinear(float3 c) { return pow(max(c, 0.0001), 2.2); }
            float3 LinearToGamma(float3 c) { return pow(max(c, 0.0001), 1.0 / 2.2); }
            float  Luma(float3 c)          { return dot(c, float3(0.299, 0.587, 0.114)); }

            // Crushes blacks hard while protecting mids
            float3 CrushBlacks(float3 c, float crush)
            {
                float l = Luma(c);
                float mask = 1.0 - smoothstep(0.0, 0.35, l);
                return c * (1.0 - mask * crush * 0.85);
            }

            // Teal grade: shadows go deep teal, mids shift cool
            float3 TealGrade(float3 col, float shadowAmt, float midAmt)
            {
                float l = Luma(col);
                float shadowMask = 1.0 - smoothstep(0.0, 0.5, l);
                float midMask    = smoothstep(0.1, 0.5, l) * (1.0 - smoothstep(0.5, 0.9, l));

                // Shadows: lift blue+green, drain red
                col.r -= shadowMask * shadowAmt * 0.20;
                col.g += shadowMask * shadowAmt * 0.08;
                col.b += shadowMask * shadowAmt * 0.18;

                // Midtones: push toward teal
                col.r -= midMask * midAmt * 0.06;
                col.g += midMask * midAmt * 0.05;
                col.b += midMask * midAmt * 0.04;

                return col;
            }

            // Neon bloom: isolates bright pixels and pumps their saturation + hue
            // making them feel like emissive signs
            float3 Neonize(float3 col, float threshold, float bloomStrength, float saturation, float huePush)
            {
                float l = Luma(col);
                // Only pixels above threshold get neonized
                float neonMask = smoothstep(threshold, threshold + 0.25, l);

                if (neonMask < 0.001) return col;

                // Find dominant channel to decide hue direction
                float maxC = max(col.r, max(col.g, col.b));
                float minC = min(col.r, min(col.g, col.b));
                float chroma = maxC - minC;

                // Saturate aggressively in the bright range
                float3 grey = float3(l, l, l);
                float3 sat  = lerp(grey, col, 1.0 + saturation * neonMask);

                // Hue push: shift toward nearest primary/secondary neon color
                // Cyan bias (matches cyberpunk aesthetic)
                float cyanness  = min(sat.g, sat.b) - sat.r;
                float magentaness = min(sat.r, sat.b) - sat.g;
                float yellowness  = min(sat.r, sat.g) - sat.b;

                sat.g += max(0.0, cyanness)  * huePush * 0.15 * neonMask;
                sat.b += max(0.0, cyanness)  * huePush * 0.15 * neonMask;
                sat.r += max(0.0, magentaness) * huePush * 0.20 * neonMask;
                sat.b += max(0.0, magentaness) * huePush * 0.10 * neonMask;
                sat.r += max(0.0, yellowness) * huePush * 0.12 * neonMask;
                sat.g += max(0.0, yellowness) * huePush * 0.12 * neonMask;

                // Bloom: add a glowing halo by brightening
                sat += sat * bloomStrength * neonMask * chroma;

                return sat;
            }

            // Atmospheric fog tint - gives the city haze feel
            float3 NeonFog(float3 col, float2 uv, float density)
            {
                // Fog is strongest toward horizon (mid-y) and edges
                float horizonFog = 1.0 - abs(uv.y - 0.5) * 2.0;
                horizonFog = pow(saturate(horizonFog), 3.0) * density;
                float3 fogColor = float3(0.02, 0.12, 0.18); // deep teal fog
                return lerp(col, col + fogColor * horizonFog, horizonFog * 0.4);
            }

            // Unsharp mask
            float3 Sharpen(float2 uv, float3 col, float strength)
            {
                float2 tx  = _BlitTexture_TexelSize.xy;
                float3 n   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,  tx.y)).rgb;
                float3 sv  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, -tx.y)).rgb;
                float3 e   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( tx.x, 0)).rgb;
                float3 w   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-tx.x, 0)).rgb;
                float3 blr = (n + sv + e + w) * 0.25;
                return col + (col - blr) * strength;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.texcoord;
                float  time = _Time.y;

                // Chromatic aberration - adds cheap lens feel
                float2 ca  = float2(_ChromaticStrength * _BlitTexture_TexelSize.x, 0.0);
                float  r   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + ca).r;
                float  g   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                float  b   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - ca).b;
                float3 col = float3(r, g, b);

                // 1. Sharpen - makes neon signs crisp
                if (_Sharpness > 0.001)
                    col = Sharpen(uv, col, _Sharpness);
                col = saturate(col);

                // Work in linear
                col = GammaToLinear(col);

                // 2. Exposure
                col *= _Exposure;

                // 3. Hard contrast S-curve
                col = LinearToGamma(saturate(col));
                float3 s = col - 0.5;
                col = saturate(s * _Contrast + 0.5);
                col = GammaToLinear(col);

                // 4. Crush blacks deep - key to the cyberpunk look
                col = CrushBlacks(col, _BlackCrush);

                // 5. Teal grade for shadows and midtones
                col = TealGrade(col, _TealShadows, _TealMidtones);

                // 6. Neonize bright spots
                col = Neonize(col, _NeonThreshold, _NeonBloom, _NeonSaturation, _NeonHuePush);

                // 7. Atmospheric neon fog
                if (_FogDensity > 0.001)
                    col = NeonFog(col, uv, _FogDensity);

                col = LinearToGamma(saturate(col));

                // 8. Fine grain - night photography noise
                if (_GrainIntensity > 0.001)
                {
                    float2 gp   = floor(uv / (_GrainSize * _BlitTexture_TexelSize.x));
                    float  seed = frac(time * 0.04731);
                    float  gr   = hash2(gp + seed * 317.3) * 2.0 - 1.0;
                    float  lumaG = Luma(col);
                    // More grain in shadows - night ISO noise
                    float  grainMask = 1.0 - smoothstep(0.0, 0.5, lumaG) * 0.7;
                    col += gr * _GrainIntensity * grainMask;
                }

                // 9. Deep vignette - tunnels the eye down the street
                if (_VignetteIntensity > 0.001)
                {
                    float2 vig  = uv * (1.0 - uv.yx);
                    float  vPow = pow(saturate(vig.x * vig.y * 14.0), 0.35);
                    col        *= lerp(1.0 - _VignetteIntensity, 1.0, vPow);
                }

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
