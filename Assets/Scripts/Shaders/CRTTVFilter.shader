Shader "Custom/URP/CRTTVFilter"
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

            float  _CurvatureStrength;
            float  _BarrelStrength;
            float  _ScanlineIntensity;
            float  _ScanlineThickness;
            float  _PhosphorIntensity;
            float  _ColorBleedStrength;
            float  _StaticIntensity;
            float  _SyncWobble;
            float  _SignalRollSpeed;
            float  _SignalRollIntensity;
            float  _VignetteIntensity;
            float  _BrightnessBoost;
            float  _ChromaticStrength;
            float  _FlickerIntensity;
            float  _Saturation;
            float  _BlackLift;
            float  _Vibrance;
            float4 _PhosphorTint;

            float hash1(float n)  { return frac(sin(n) * 43758.5453); }
            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // Hard barrel distortion - corners go black, center bulges forward
            float2 BarrelDistort(float2 uv, float k)
            {
                float2 cc = uv - 0.5;
                float  r2 = dot(cc, cc);
                return uv + cc * (r2 * k);
            }

            // Barrel distortion - safe, never pushes UVs outside [0,1]
            float2 Barrel(float2 uv, float strength)
            {
                float2 cc = uv - 0.5;
                float2 warped = uv + cc * (dot(cc, cc) * strength);
                // Remap back into [0,1] so no pixel is ever clipped to black
                return lerp(uv, warped, strength * 4.0);
            }

            // Per-scanline horizontal sync wobble
            float2 ApplySyncWobble(float2 uv, float time, float amount)
            {
                float row    = floor(uv.y * _BlitTexture_TexelSize.w);
                float jitter = (hash1(row * 0.13 + time * 3.7) * 2.0 - 1.0)
                             * amount * _BlitTexture_TexelSize.x;
                uv.x += jitter;
                return uv;
            }

            // Slow vertical signal roll
            float2 ApplySignalRoll(float2 uv, float time, float speed, float amount)
            {
                float roll = frac(time * speed * 0.05);
                float band = frac(uv.y - roll);
                float wave = sin(band * 6.2832) * amount * _BlitTexture_TexelSize.y * 4.0;
                uv.y += wave * smoothstep(0.9, 1.0, band);
                return uv;
            }

            // RGB phosphor stripe mask - matches physical CRT dot pitch
            float3 PhosphorMask(float2 uv, float intensity)
            {
                float col3 = fmod(floor(uv.x * _BlitTexture_TexelSize.z), 3.0);
                float3 msk;
                msk.r = (col3 < 1.0) ? 1.0 : 0.4;
                msk.g = (col3 >= 1.0 && col3 < 2.0) ? 1.0 : 0.4;
                msk.b = (col3 >= 2.0) ? 1.0 : 0.4;
                return lerp(float3(1.0, 1.0, 1.0), msk, intensity);
            }

            // NTSC horizontal color bleed
            float3 ColorBleed(float2 uv, float amount)
            {
                float  o     = _BlitTexture_TexelSize.x * amount;
                float3 left  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(o * 2.0, 0)).rgb;
                float3 mid   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float3 right = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(o, 0)).rgb;
                float3 bleed;
                bleed.r = lerp(mid.r, (left.r + mid.r + right.r) / 3.0, 0.65);
                bleed.g = mid.g;
                bleed.b = lerp(mid.b, (left.b + mid.b + right.b) / 3.0, 0.65);
                return bleed;
            }

            // Luminance weight
            float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            // Saturation: >1 vivid, <1 muted
            float3 AdjustSaturation(float3 c, float sat)
            {
                float luma = Luma(c);
                return lerp(float3(luma, luma, luma), c, sat);
            }

            // Vibrance: boosts low-saturation pixels more than already-vivid ones
            float3 AdjustVibrance(float3 c, float vibrance)
            {
                float luma    = Luma(c);
                float maxComp = max(c.r, max(c.g, c.b));
                float minComp = min(c.r, min(c.g, c.b));
                float sat     = maxComp - minComp;
                // pixels closer to grey get a stronger boost
                float boost   = vibrance * (1.0 - sat * 1.5);
                return lerp(float3(luma, luma, luma), c, 1.0 + boost);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.texcoord;
                float  time = _Time.y;

                // 0. Hard barrel / lens warp - corners rendered black
                if (_BarrelStrength > 0.001)
                {
                    uv = BarrelDistort(uv, _BarrelStrength);
                    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                        return float4(0.0, 0.0, 0.0, 1.0);
                }

                // 1. Subtle barrel distortion - stays inside [0,1], never black
                float2 distUV = (_CurvatureStrength > 0.001)
                    ? clamp(Barrel(uv, _CurvatureStrength), 0.0, 1.0)
                    : uv;

                // 2. Signal roll
                if (_SignalRollIntensity > 0.001)
                    distUV = ApplySignalRoll(distUV, time, _SignalRollSpeed, _SignalRollIntensity);

                // 3. Sync wobble
                if (_SyncWobble > 0.001)
                    distUV = ApplySyncWobble(distUV, time, _SyncWobble);

                // 4. Chromatic aberration
                float2 ca  = float2(_ChromaticStrength * _BlitTexture_TexelSize.x, 0.0);
                float  r   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distUV + ca).r;
                float  g   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distUV).g;
                float  b   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distUV - ca).b;
                float3 col = float3(r, g, b);

                // 5. NTSC color bleed
                if (_ColorBleedStrength > 0.001)
                    col = ColorBleed(distUV, _ColorBleedStrength);

                // 6. Scanlines
                if (_ScanlineIntensity > 0.001)
                {
                    float scanRow = fmod(distUV.y * _BlitTexture_TexelSize.w, 2.0);
                    float scanVal = smoothstep(0.0, _ScanlineThickness, scanRow)
                                  * smoothstep(2.0, 2.0 - _ScanlineThickness, scanRow);
                    col *= lerp(1.0, scanVal, _ScanlineIntensity);
                }

                // 7. Phosphor mask
                if (_PhosphorIntensity > 0.001)
                    col *= PhosphorMask(distUV, _PhosphorIntensity);

                // 8. Static/snow
                if (_StaticIntensity > 0.001)
                {
                    float noise = hash2(float2(distUV.x, distUV.y + frac(time * 73.1)));
                    col += (noise * 2.0 - 1.0) * _StaticIntensity;
                }

                // 9. Vignette
                if (_VignetteIntensity > 0.001)
                {
                    float2 vig  = uv * (1.0 - uv.yx);
                    float  vPow = pow(saturate(vig.x * vig.y * 12.0), 0.4);
                    col        *= lerp(1.0 - _VignetteIntensity, 1.0, vPow);
                }

                // 10. Color grade - muted lift + vibrant phosphor pop
                // Black lift: raises shadows so they never crush to pure black (faded/muted feel)
                col = col * (1.0 - _BlackLift) + _BlackLift * 0.18;
                // Phosphor tint: pushes colours toward the CRT phosphor cast
                float luma = Luma(col);
                col = lerp(col, col * _PhosphorTint.rgb, _PhosphorTint.a);
                // Saturation layer
                col = AdjustSaturation(col, _Saturation);
                // Vibrance layer - lifts dull colours without blowing vivid ones
                col = AdjustVibrance(col, _Vibrance);

                // 10. Brightness boost
                col = pow(saturate(col), 0.9) * _BrightnessBoost;

                // 11. Flicker
                if (_FlickerIntensity > 0.001)
                    col *= 1.0 + (hash1(floor(time * 50.0)) * 2.0 - 1.0) * _FlickerIntensity * 0.06;

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
