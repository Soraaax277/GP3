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

            // ─── Uniforms ──────────────────────────────────────────────────────────
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

            // Neon Outline
            float  _OutlineIntensity;
            float  _OutlineThickness;
            float4 _OutlineColor;      // HDR-capable

            // HUD Hex Grid
            float  _HexOpacity;
            float  _HexPanelWidth;
            float  _HexGridScale;
            float4 _HexColor;

            // ─── Utility ───────────────────────────────────────────────────────────
            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float3 GammaToLinear(float3 c) { return pow(max(c, 0.0001), 2.2); }
            float3 LinearToGamma(float3 c) { return pow(max(c, 0.0001), 1.0 / 2.2); }
            float  Luma(float3 c)          { return dot(c, float3(0.299, 0.587, 0.114)); }

            // ─── Color Grading ─────────────────────────────────────────────────────

            // Gentle black crush - MUCH less aggressive than before
            float3 CrushBlacks(float3 c, float crush)
            {
                float l = Luma(c);
                float mask = 1.0 - smoothstep(0.0, 0.35, l);
                return c * (1.0 - mask * crush * 0.85);
            }

            // Teal grade: shadows and midtones shift cool, NOT full wash
            float3 TealGrade(float3 col, float shadowAmt, float midAmt)
            {
                float l = Luma(col);
                float shadowMask = 1.0 - smoothstep(0.0, 0.5, l);
                float midMask    = smoothstep(0.1, 0.5, l) * (1.0 - smoothstep(0.5, 0.9, l));

                col.r -= shadowMask * shadowAmt * 0.20;
                col.g += shadowMask * shadowAmt * 0.08;
                col.b += shadowMask * shadowAmt * 0.18;

                col.r -= midMask * midAmt * 0.06;
                col.g += midMask * midAmt * 0.05;
                col.b += midMask * midAmt * 0.04;

                return col;
            }

            // Neonize: only affects the very brightest highlights
            float3 Neonize(float3 col, float threshold, float bloomStr, float sat, float huePush)
            {
                float l = Luma(col);
                float neonMask = smoothstep(threshold, threshold + 0.25, l);
                if (neonMask < 0.001) return col;

                float maxC  = max(col.r, max(col.g, col.b));
                float minC  = min(col.r, min(col.g, col.b));
                float chroma = maxC - minC;

                float3 grey = float3(l, l, l);
                float3 s    = lerp(grey, col, 1.0 + sat * neonMask);

                float cyanness    = min(s.g, s.b) - s.r;
                float magentaness = min(s.r, s.b) - s.g;
                float yellowness  = min(s.r, s.g) - s.b;

                s.g += max(0.0, cyanness)    * huePush * 0.15 * neonMask;
                s.b += max(0.0, cyanness)    * huePush * 0.15 * neonMask;
                s.r += max(0.0, magentaness) * huePush * 0.20 * neonMask;
                s.b += max(0.0, magentaness) * huePush * 0.10 * neonMask;
                s.r += max(0.0, yellowness)  * huePush * 0.12 * neonMask;
                s.g += max(0.0, yellowness)  * huePush * 0.12 * neonMask;

                s += s * bloomStr * neonMask * chroma;
                return s;
            }

            // Subtle horizon haze - not a full fog wall
            float3 NeonFog(float3 col, float2 uv, float density)
            {
                float horizonFog = 1.0 - abs(uv.y - 0.5) * 2.0;
                horizonFog = pow(saturate(horizonFog), 3.0) * density;
                float3 fogColor = float3(0.02, 0.12, 0.18);
                return lerp(col, col + fogColor * horizonFog, horizonFog * 0.4);
            }

            // Unsharp mask - higher sharpness to keep detail crisp
            float3 Sharpen(float2 uv, float3 col, float strength)
            {
                float2 tx  = _BlitTexture_TexelSize.xy;
                float3 n   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,   tx.y)).rgb;
                float3 sv  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0,  -tx.y)).rgb;
                float3 e   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( tx.x, 0  )).rgb;
                float3 w   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-tx.x, 0  )).rgb;
                float3 blr = (n + sv + e + w) * 0.25;
                return col + (col - blr) * strength;
            }

            // ─── Neon Outline (Sobel on luma of original image) ────────────────────
            float EdgeDetect(float2 uv)
            {
                // Thickness scales the sample offset so higher values = thicker line
                float2 tx = _BlitTexture_TexelSize.xy * max(1.0, _OutlineThickness);

                float l00 = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-tx.x,  tx.y)).rgb);
                float l10 = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( 0,      tx.y)).rgb);
                float l20 = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( tx.x,  tx.y)).rgb);
                float l01 = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-tx.x,  0   )).rgb);
                float l21 = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( tx.x,  0   )).rgb);
                float l02 = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-tx.x, -tx.y)).rgb);
                float l12 = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( 0,     -tx.y)).rgb);
                float l22 = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( tx.x, -tx.y)).rgb);

                float gx = -l00 - 2.0*l01 - l02 + l20 + 2.0*l21 + l22;
                float gy =  l00 + 2.0*l10 + l20 - l02 - 2.0*l12 - l22;

                // Scale so the result is well-distributed [0,1]
                return saturate(sqrt(gx*gx + gy*gy) * 2.5);
            }

            // ─── Hexagonal HUD ─────────────────────────────────────────────────────

            // Exact hexagon SDF (IQ). r = inradius. Negative = inside, positive = outside.
            float HexSDF(float2 p, float r)
            {
                const float2 k  = float2(-0.866025404, 0.5);
                const float  k3 = 0.577350269;
                p = abs(p);
                p -= 2.0 * min(dot(k, p), 0.0) * k;
                p -= float2(clamp(p.x, -k3 * r, k3 * r), r);
                return length(p) * sign(p.y);
            }

            // Tiling hex grid: returns local position within the nearest hex cell
            // and a stable per-cell ID for hashing.
            void GetHexCell(float2 p, out float2 localPos, out float2 cellID)
            {
                // Pointy-top hex period
                const float2 r = float2(1.7320508, 1.0);
                const float2 h = r * 0.5;

                float2 aLocal = fmod(p,     r) - h;
                float2 aID    = floor(p     / r);
                float2 bLocal = fmod(p - h, r) - h;
                float2 bID    = floor((p - h) / r);

                if (dot(aLocal, aLocal) < dot(bLocal, bLocal)) {
                    localPos = aLocal;
                    cellID   = aID;
                } else {
                    localPos = bLocal;
                    cellID   = bID + 0.5;
                }
            }

            float3 ApplyHexHUD(float3 col, float2 uv, float time)
            {
                // ── Side panel masks ───────────────────────────────────────────────
                // Fade smoothly from screen edge inward; fully gone by panelWidth
                float leftMask  = smoothstep(_HexPanelWidth,       _HexPanelWidth * 0.35, uv.x);
                float rightMask = smoothstep(1.0 - _HexPanelWidth, 1.0 - _HexPanelWidth * 0.35, uv.x);
                float panelMask = saturate(leftMask + rightMask);

                // Vertical fade: not at very top / bottom (keeps UI feel)
                panelMask *= smoothstep(0.0, 0.12, uv.y) * smoothstep(1.0, 0.88, uv.y);

                if (panelMask < 0.001) return col;

                // ── Hex grid (aspect-correct) ──────────────────────────────────────
                float aspect = _BlitTexture_TexelSize.z / _BlitTexture_TexelSize.w;
                float2 p = float2(uv.x * aspect, uv.y) * _HexGridScale;

                float2 localPos, cellID;
                GetHexCell(p, localPos, cellID);

                // SDF: inradius 0.46 leaves a visible border gap of ~0.04
                float hexDist = HexSDF(localPos, 0.46);

                // Thin border glow
                float borderW = 0.035;
                float border  = 1.0 - smoothstep(0.0, borderW, abs(hexDist));

                // ── Per-cell random pulse ──────────────────────────────────────────
                float rnd      = hash2(cellID);
                float pulse    = sin(time * (0.7 + rnd * 1.8) + rnd * 6.28318) * 0.5 + 0.5;
                float isActive = step(0.62, rnd);            // ~38% of hexes glow
                // Dim interior fill for active hexes only
                float fill = (hexDist < -borderW) ? isActive * pulse * pulse * 0.22 : 0.0;

                // Random "alert" hexes: brighter burst
                float isAlert  = step(0.90, rnd);
                float alert    = isAlert * pow(pulse, 4.0) * 0.55 * (hexDist < -borderW ? 1.0 : 0.0);

                float hexGlow = border + fill + alert;

                // ── Panel background (semi-transparent dark teal) ──────────────────
                float3 panelBg = col * 0.20 + float3(0.00, 0.05, 0.12);
                col = lerp(col, panelBg, panelMask * 0.50);

                // ── Hex color overlay ──────────────────────────────────────────────
                float3 hexCol = _HexColor.rgb;
                col += hexGlow * hexCol * panelMask * _HexOpacity;

                // Extra thin bloom around bright hex borders
                float bloomBorder = max(0.0, 1.0 - smoothstep(0.0, borderW * 3.5, abs(hexDist))) * 0.35;
                col += bloomBorder * hexCol * panelMask * _HexOpacity;

                // ── Horizontal scanlines on HUD area (data readout feel) ───────────
                float scan = sin(uv.y * 220.0) * 0.5 + 0.5;
                col -= scan * 0.04 * panelMask;

                // ── Slowly scrolling horizontal data lines ─────────────────────────
                float dataLine = step(0.96, frac(uv.y * 28.0 - time * 0.08));
                col += dataLine * hexCol * 0.12 * panelMask;

                return col;
            }

            // ─── Fragment ──────────────────────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.texcoord;
                float  time = _Time.y;

                // ── Edge detection FIRST (on unmodified image) ──
                float edgeStr = 0.0;
                if (_OutlineIntensity > 0.001)
                    edgeStr = EdgeDetect(uv);

                // ── Chromatic Aberration ────────────────────────────────────────────
                float2 ca  = float2(_ChromaticStrength * _BlitTexture_TexelSize.x, 0.0);
                float  r   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + ca).r;
                float  g   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv     ).g;
                float  b   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - ca).b;
                float3 col = float3(r, g, b);

                // ── Sharpen (higher default preserves model detail) ─────────────────
                if (_Sharpness > 0.001)
                    col = Sharpen(uv, col, _Sharpness);
                col = saturate(col);

                // ── Linear space ────────────────────────────────────────────────────
                col = GammaToLinear(col);

                // ── Exposure ────────────────────────────────────────────────────────
                col *= _Exposure;

                // ── Contrast S-curve ────────────────────────────────────────────────
                col = LinearToGamma(saturate(col));
                col = saturate((col - 0.5) * _Contrast + 0.5);
                col = GammaToLinear(col);

                // ── Black crush (VERY subtle now) ───────────────────────────────────
                col = CrushBlacks(col, _BlackCrush);

                // ── Teal grade (shadow hint, not a bath) ────────────────────────────
                col = TealGrade(col, _TealShadows, _TealMidtones);

                // ── Neonize highlights only ─────────────────────────────────────────
                col = Neonize(col, _NeonThreshold, _NeonBloom, _NeonSaturation, _NeonHuePush);

                // ── Subtle atmospheric haze ─────────────────────────────────────────
                if (_FogDensity > 0.001)
                    col = NeonFog(col, uv, _FogDensity);

                col = LinearToGamma(saturate(col));

                // ── Neon Outline ────────────────────────────────────────────────────
                if (_OutlineIntensity > 0.001 && edgeStr > 0.08)
                {
                    float neonEdge   = smoothstep(0.08, 0.55, edgeStr) * _OutlineIntensity;
                    float3 outlineC  = _OutlineColor.rgb;

                    // Core outline: replace with neon color
                    col = lerp(col, outlineC, neonEdge * 0.75);

                    // Soft glow halo (brighter, wider fade)
                    float halo = smoothstep(0.05, 0.30, edgeStr) * _OutlineIntensity * 0.45;
                    col += outlineC * halo;
                }

                // ── HUD Hex Grid ────────────────────────────────────────────────────
                if (_HexOpacity > 0.001)
                    col = ApplyHexHUD(col, uv, time);

                // ── Film Grain ──────────────────────────────────────────────────────
                if (_GrainIntensity > 0.001)
                {
                    float2 gp       = floor(uv / (_GrainSize * _BlitTexture_TexelSize.x));
                    float  seed     = frac(time * 0.04731);
                    float  gr       = hash2(gp + seed * 317.3) * 2.0 - 1.0;
                    float  lumaG    = Luma(col);
                    float  gMask    = 1.0 - smoothstep(0.0, 0.5, lumaG) * 0.7;
                    col += gr * _GrainIntensity * gMask;
                }

                // ── Vignette ────────────────────────────────────────────────────────
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
