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

            // Blit.hlsl declares: _BlitTexture, _BlitTexture_TexelSize, sampler_LinearClamp, Varyings, Vert

            float  _Exposure;
            float  _Contrast;
            float  _Saturation;
            float  _GreenTint;
            float  _GreenShadowLift;
            float  _RedDrain;
            float  _BlueShift;
            float  _HighlightBlow;
            float  _GrainIntensity;
            float  _GrainSize;
            float  _Sharpness;
            float  _VignetteIntensity;
            float  _BlackCrush;
            float  _LetterboxAmount;

            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float3 GammaToLinear(float3 c) { return pow(max(c, 0.0001), 2.2); }
            float3 LinearToGamma(float3 c) { return pow(max(c, 0.0001), 1.0 / 2.2); }
            float  Luma(float3 c)          { return dot(c, float3(0.299, 0.587, 0.114)); }

            // Lifted S-curve - lifts shadows slightly while punching mids
            float3 SCurve(float3 c, float strength)
            {
                // Raise the toe so blacks never fully crush
                float3 lifted = c * (1.0 - 0.04) + 0.04;
                // Standard S
                float3 s = lifted - 0.5;
                s = s * strength;
                s = s + 0.5;
                return s;
            }

            // Unsharp mask - clean, not crunchy
            float3 Sharpen(float2 uv, float3 col, float strength)
            {
                float2 tx  = _BlitTexture_TexelSize.xy;
                float3 n   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,  tx.y)).rgb;
                float3 s   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, -tx.y)).rgb;
                float3 e   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( tx.x, 0)).rgb;
                float3 w   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-tx.x, 0)).rgb;
                float3 blr = (n + s + e + w) * 0.25;
                return col + (col - blr) * strength;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.texcoord;
                float  time = _Time.y;

                // Letterbox - hard black bars top and bottom
                float halfBar = _LetterboxAmount * 0.5;
                if (uv.y < halfBar || uv.y > (1.0 - halfBar))
                    return float4(0.0, 0.0, 0.0, 1.0);

                // Base sample
                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // 1. Sharpen first - music videos have that over-sharpened DV/prosumer look
                if (_Sharpness > 0.001)
                    col = Sharpen(uv, col, _Sharpness);
                col = saturate(col);

                // Work in linear
                col = GammaToLinear(col);

                // 2. Exposure
                col *= _Exposure;

                // 3. Saturation pump - vivid, almost too much
                float luma = Luma(col);
                col = lerp(float3(luma, luma, luma), col, _Saturation);

                // 4. Channel grade - the 2000s music video recipe:
                //    drain reds slightly, pump greens, push blues toward cyan
                float lumaW = Luma(col);
                col.r = col.r * (1.0 - _RedDrain  * 0.12);
                col.g = col.g * (1.0 + _GreenTint  * 0.18);
                col.b = col.b * (1.0 + _BlueShift  * 0.10);

                // 5. Shadow green lift - darks go green/teal
                float shadowMask = 1.0 - smoothstep(0.0, 0.4, Luma(col));
                col.g += shadowMask * _GreenShadowLift * 0.06;
                col.b += shadowMask * _GreenShadowLift * 0.03;
                col.r -= shadowMask * _GreenShadowLift * 0.02;

                // 6. Highlight blow - slightly overexposed whites feel like cheap video camera
                float highMask = smoothstep(0.6, 1.0, Luma(col));
                col += highMask * _HighlightBlow * 0.12;

                // Back to gamma
                col = LinearToGamma(saturate(col));

                // 7. S-curve contrast with lifted blacks
                col = SCurve(col, _Contrast);

                // 8. Black crush after curve
                col = max(0.0, col - _BlackCrush * 0.04);

                // 9. Fine digital grain - DV camera noise, very tight
                if (_GrainIntensity > 0.001)
                {
                    float2 gp   = floor(uv / (_GrainSize * _BlitTexture_TexelSize.x));
                    float  seed = frac(time * 0.05731);
                    float  gr   = hash2(gp + seed * 213.7) * 2.0 - 1.0;
                    // Slightly more grain in mids, less in shadows/highlights (DV behavior)
                    float  lumaG = Luma(col);
                    float  grainMask = 1.0 - abs(lumaG * 2.0 - 1.0) * 0.5;
                    col += gr * _GrainIntensity * grainMask;
                }

                // 10. Subtle vignette - not heavy, just a slight edge darkening
                if (_VignetteIntensity > 0.001)
                {
                    float2 vig  = uv * (1.0 - uv.yx);
                    float  vPow = pow(saturate(vig.x * vig.y * 16.0), 0.6);
                    col        *= lerp(1.0 - _VignetteIntensity, 1.0, vPow);
                }

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
