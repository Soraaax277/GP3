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
            #pragma target   4.5

            // Suppress harmless pow(0) precision warnings
            #pragma warning (disable : 3571)

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // ── NOTE ON COLOR SPACE ────────────────────────────────────────────
            // This pass runs at AfterRenderingPostProcessing. URP's tonemapper has
            // already run, so the active color buffer is in display / gamma space.
            // All math operates in that space — no gamma round-trips needed.

            // ─── Uniforms ──────────────────────────────────────────────────────
            float  _Exposure;
            float  _Contrast;
            float  _BlackCrush;
            float  _MinLuminance;
            float  _ShadowLift;

            float  _TealShadows;

            float  _NeonThreshold;
            float  _NeonBloom;
            float  _NeonSaturation;
            float  _NeonHuePush;
            float  _NeonFlickerAmt;

            float  _FogDensity;

            float  _ChromaticStrength;
            float  _CaOscillation;
            float  _BarrelDistort;
            float  _HeatHazeStrength;
            float  _FocusBreathAmt;

            float  _Sharpness;
            float  _GrainIntensity;
            float  _GrainSize;
            float  _VignetteIntensity;

            float  _OutlineIntensity;
            float  _OutlineThickness;
            float4 _OutlineColor;

            float  _ScanlineIntensity;
            float  _ScanlineDensity;
            float  _ScanDriftSpeed;
            float  _InterlaceStrength;

            float  _GlitchIntensity;
            float  _GlitchSeed;

            float  _ReticleOpacity;
            float4 _ReticleColor;
            float  _DataBarOpacity;
            float4 _DataBarColor;

            // ─── Helpers ───────────────────────────────────────────────────────
            float hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Luma(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            // ─── Optics ────────────────────────────────────────────────────────

            float2 BarrelUV(float2 uv, float k)
            {
                float2 cc = uv - 0.5;
                return uv + cc * dot(cc, cc) * k;
            }

            // Heat haze — two overlapping sines per axis, no texture samples
            float2 HeatHazeUV(float2 uv, float str, float time)
            {
                float wx = sin(uv.y * 23.7 + time * 1.3) * 0.6
                         + sin(uv.y * 41.3 - time * 0.7) * 0.4;
                float wy = sin(uv.x * 19.1 + time * 0.9) * 0.6
                         + sin(uv.x * 37.7 + time * 1.1) * 0.4;
                return uv + float2(wx, wy) * str * _BlitTexture_TexelSize.xy * 3.0;
            }

            // Subtle lens focus-breath — periodic zoom, no texture samples
            float2 FocusBreathUV(float2 uv, float amt, float time)
            {
                float zoom = 1.0 + amt * (sin(time * 0.71) * 0.5 + 0.5);
                return (uv - 0.5) / zoom + 0.5;
            }

            // ─── Tone Ops ─────────────────────────────────────────────────────

            float3 SCurve(float3 c, float s)
            {
                c -= 0.5;
                c  = c / (1.0 + abs(c) * (s - 1.0) * 1.5);
                return c + 0.5;
            }

            // ─── Phosphor Grade ────────────────────────────────────────────────
            float3 PhosphorGrade(float3 col, float amt)
            {
                float l    = Luma(col);
                float sMsk = (1.0 - smoothstep(0.0, 0.52, l))
                           * saturate(smoothstep(0.0, 0.28, l) * 1.9 + 0.20);
                float hMsk = smoothstep(0.68, 1.0, l);
                col.r -= sMsk * amt * 0.055;
                col.g += sMsk * amt * 0.075 + hMsk * amt * 0.022;
                col.b += sMsk * amt * 0.018 + hMsk * amt * 0.038;
                return col;
            }

            // ─── Neon ─────────────────────────────────────────────────────────
            float3 Neonize(float3 col, float thr, float bloom, float sat,
                           float hpush, float flickAmt, float time)
            {
                float l    = Luma(col);
                float mask = smoothstep(thr, thr + 0.25, l);
                if (mask < 0.001) return col;

                // Three beating sines create a chaotic flicker envelope
                float fl  = sin(time * 37.3) * sin(time * 13.7) * sin(time * 5.1);
                bloom    *= 1.0 - saturate(smoothstep(0.6, 1.0, fl)) * flickAmt;

                float maxC   = max(col.r, max(col.g, col.b));
                float minC   = min(col.r, min(col.g, col.b));
                float chroma = maxC - minC;

                float3 s = lerp(l.xxx, col, 1.0 + sat * mask);

                float cyan    = min(s.g, s.b) - s.r;
                float magenta = min(s.r, s.b) - s.g;
                float yellow  = min(s.r, s.g) - s.b;

                s.g += max(0.0, cyan)    * hpush * 0.15 * mask;
                s.b += max(0.0, cyan)    * hpush * 0.15 * mask;
                s.r += max(0.0, magenta) * hpush * 0.20 * mask;
                s.b += max(0.0, magenta) * hpush * 0.10 * mask;
                s.r += max(0.0, yellow)  * hpush * 0.12 * mask;
                s.g += max(0.0, yellow)  * hpush * 0.12 * mask;

                s += s * bloom * mask * chroma;
                return s;
            }

            // ─── Fog ──────────────────────────────────────────────────────────
            float3 NeonFog(float3 col, float2 uv, float density)
            {
                float h = pow(saturate(1.0 - abs(uv.y - 0.5) * 2.0), 3.0) * density;
                return col + float3(0.01, 0.07, 0.10) * h * 0.35;
            }

            // ─── Edge Detect — Roberts Cross (4 samples) ──────────────────────
            // Half the sample cost of full Sobel, plenty sharp for neon outlines.
            float EdgeDetect(float2 uv)
            {
                float2 tx = _BlitTexture_TexelSize.xy * max(1.0, _OutlineThickness);
                float a = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                              uv + float2( tx.x,  tx.y)).rgb);
                float b = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                              uv + float2(-tx.x, -tx.y)).rgb);
                float c = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                              uv + float2( tx.x, -tx.y)).rgb);
                float d = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                              uv + float2(-tx.x,  tx.y)).rgb);
                float gx = a - b;
                float gy = c - d;
                return saturate(sqrt(gx * gx + gy * gy) * 4.0);
            }

            // ─── Sharpen — USM with 4 axis-aligned neighbors ──────────────────
            // Accepts already-processed center so CA/interlace work is preserved.
            float3 Sharpen(float2 uv, float3 center, float str)
            {
                float2 tx = _BlitTexture_TexelSize.xy;
                float3 n  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                                uv + float2( 0,    tx.y)).rgb;
                float3 s  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                                uv + float2( 0,   -tx.y)).rgb;
                float3 e  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                                uv + float2( tx.x, 0   )).rgb;
                float3 w  = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                                uv + float2(-tx.x, 0   )).rgb;
                return saturate(center + (center - (n + s + e + w) * 0.25) * str);
            }

            // ─── Sensor Artifacts ─────────────────────────────────────────────

            // Odd/even row interlaced shimmer — 1 extra sample
            float3 InterlaceGhost(float2 uv, float3 col, float str)
            {
                float dir    = (fmod(floor(uv.y * _BlitTexture_TexelSize.w), 2.0) > 0.5)
                             ? 1.0 : -1.0;
                float3 ghost = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                                   uv + float2(0, dir * _BlitTexture_TexelSize.y * 0.5)).rgb;
                return lerp(col, ghost, str * 0.30);
            }

            // CMOS horizontal gradient banding — no texture samples
            float DigitalBanding(float2 uv, float time)
            {
                float band = frac(uv.y * 8.7 + time * 0.019);
                return smoothstep(0.0, 0.18, band) * smoothstep(1.0, 0.82, band) * 0.015;
            }

            // Drifting scanlines with occasional roll — no texture samples
            float Scanlines(float2 uv, float density, float intensity,
                            float driftSpeed, float time)
            {
                float drift   = time * driftSpeed * 0.05;
                float rollPh  = frac(time * 0.125);
                float rollOff = step(0.97, rollPh) * frac(time * 5.0) * 0.10;
                float sl      = sin((uv.y + drift + rollOff) * density) * 0.5 + 0.5;
                return 1.0 - (1.0 - sl) * intensity;
            }

            // Glitch tear — single shifted sample per burst (1 extra sample)
            float3 GlitchTears(float2 uv, float3 col, float intensity, float seed)
            {
                float rowY  = frac(seed * 73.19 + 0.10);
                float rowH  = _BlitTexture_TexelSize.y * 4.0;
                float inRow = smoothstep(rowH, 0.0, abs(uv.y - rowY));
                float shift = (frac(seed * 91.37) * 2.0 - 1.0) * 0.055 * intensity;
                float3 torn = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                                  saturate(float2(uv.x + shift, uv.y))).rgb;
                return lerp(col, torn, inRow * intensity);
            }

            // Signal dropout — procedural static band, no texture samples
            float3 SignalDropout(float3 col, float2 uv, float intensity, float seed)
            {
                float bandY  = frac(seed * 0.618 + 0.23);
                float inBand = smoothstep(0.011 * intensity, 0.0, abs(uv.y - bandY));
                float2 gp    = float2(uv.x * 420.0 + seed * 1000.0,
                                      uv.y * 280.0 + seed * 500.0);
                float3 noise = float3(hash2(gp),
                                      hash2(gp + 77.3),
                                      hash2(gp + 143.7));
                return lerp(col, noise, inBand * intensity * 0.65);
            }

            // ─── HUD Geometry ─────────────────────────────────────────────────

            // Macros kept as macros (not inlined functions) to reduce call depth
            #define LINE_H(uvY, refY, hw)  smoothstep((hw), (hw)*0.4, abs((uvY)-(refY)))
            #define LINE_V(uvX, refX, hw)  smoothstep((hw), (hw)*0.4, abs((uvX)-(refX)))
            #define BETWEEN(v, lo, hi)     (step((lo),(v)) * step((v),(hi)))

            float CornerBracket(float2 uv, float cx, float cy,
                                float sx, float sy, float arm, float lineW)
            {
                float h = LINE_H(uv.y, cy, lineW) * BETWEEN(uv.x, cx, cx + arm * sx);
                float v = LINE_V(uv.x, cx, lineW) * BETWEEN(uv.y, cy, cy + arm * sy);
                return saturate(h + v);
            }

            // Scan sweep — single glow line top to bottom every 3.5 s
            float ScanSweep(float2 uv, float time)
            {
                float t = frac(time * 0.2857);
                return smoothstep(_BlitTexture_TexelSize.y * 10.0, 0.0, abs(uv.y - t))
                     * (1.0 - t * 0.5);
            }

            // Target lock box — corner-only rectangle with pulse
            float TargetBox(float2 uv, float time)
            {
                float2 d  = abs(uv - 0.5);
                float  bW = 0.175;
                float  bH = 0.115;
                float  lW = _BlitTexture_TexelSize.y * 1.8;
                float  onX = step(d.y, bH) * smoothstep(lW * 2.5, 0.0, abs(d.x - bW));
                float  onY = step(d.x, bW) * smoothstep(lW * 2.5, 0.0, abs(d.y - bH));
                float  cx  = BETWEEN(d.x, bW - 0.032, bW + lW * 4.0);
                float  cy  = BETWEEN(d.y, bH - 0.020, bH + lW * 4.0);
                return saturate(onX * cx + onY * cy) * (sin(time * 2.3) * 0.28 + 0.72);
            }

            // Focus arc — animated semicircle below crosshair
            float FocusArc(float2 uv, float time)
            {
                float2 d    = uv - 0.5;
                float  r    = length(d);
                float  ring = smoothstep(_BlitTexture_TexelSize.y * 2.2, 0.0, abs(r - 0.031));
                float  fill = sin(time * 0.93) * 0.38 + 0.62;
                float  ax   = (r > 0.0001) ? (d.x / r) : 0.0;
                return ring * step(0.0, d.y) * BETWEEN(ax, -fill * 0.60, fill * 0.60);
            }

            // Full reticle composite
            float3 CameraReticle(float3 col, float2 uv, float opacity,
                                 float4 rcol, float time)
            {
                float lineW = _BlitTexture_TexelSize.y * 1.8;
                float inset = 0.05;
                float arm   = 0.042;
                float mark  = 0.0;

                mark = max(mark, CornerBracket(uv, inset,       inset,        1,  1, arm, lineW));
                mark = max(mark, CornerBracket(uv, 1.0 - inset, inset,       -1,  1, arm, lineW));
                mark = max(mark, CornerBracket(uv, inset,       1.0 - inset,  1, -1, arm, lineW));
                mark = max(mark, CornerBracket(uv, 1.0 - inset, 1.0 - inset, -1, -1, arm, lineW));

                // Centre crosshair
                float2 d   = uv - 0.5;
                float  dxA = abs(d.x);
                float  dyA = abs(d.y);
                mark = max(mark, LINE_H(uv.y, 0.5, lineW) * BETWEEN(dxA, 0.008, 0.030));
                mark = max(mark, LINE_V(uv.x, 0.5, lineW) * BETWEEN(dyA, 0.008, 0.030));
                mark = max(mark, smoothstep(lineW * 2.0, lineW * 0.8, length(d)) * 0.55);

                // Animated geometry
                mark = max(mark, FocusArc(uv, time) * 0.80);
                mark = max(mark, TargetBox(uv, time) * 0.55);

                // Scan sweep (additive glow)
                col += rcol.rgb * ScanSweep(uv, time) * opacity * 0.28;

                // Composite
                col  = lerp(col, rcol.rgb, mark * opacity);
                col += rcol.rgb * mark * opacity * 0.18;

                // REC blink dot
                float  blink  = step(0.5, frac(time * 0.75));
                float2 dotPos = float2(inset + arm * 0.55, inset - arm * 0.55);
                float  dotR   = _BlitTexture_TexelSize.y * 5.0;
                float  recDot = smoothstep(dotR * 1.8, dotR * 0.5, length(uv - dotPos)) * blink;
                col = lerp(col, float3(1.0, 0.10, 0.07), recDot * opacity);

                return col;
            }

            // Top / bottom data bars
            float3 DataBars(float3 col, float2 uv, float opacity,
                            float4 barCol, float time)
            {
                float barH = 0.026;
                float topM = 1.0 - smoothstep(0.0, barH, uv.y);
                float botM = smoothstep(1.0 - barH, 1.0, uv.y);
                float mask = saturate(topM + botM);

                col = lerp(col, col * 0.10 + barCol.rgb * 0.06, mask * opacity);

                float tA = step(0.88, frac(uv.x * 90.0));
                float tB = step(0.92, frac(uv.x * 22.0));
                col += (tA * 0.35 + tB * 0.55) * barCol.rgb * mask * opacity;

                float s1 = step(0.80, frac(uv.x * 58.0 - time * 0.28)) * botM;
                float s2 = step(0.85, frac(uv.x * 38.0 + time * 0.19)) * botM;
                float s3 = step(0.83, frac(uv.x * 44.0 - time * 0.12)) * topM;
                col += (s1 * 0.50 + s2 * 0.33 + s3 * 0.38) * barCol.rgb * opacity;

                float dash = step(0.50, frac(uv.x * 14.0 - time * 0.07))
                           * step(frac(uv.x * 14.0 - time * 0.07), 0.90);
                col += dash * barCol.rgb * mask * opacity * 0.20;

                float2 tx  = _BlitTexture_TexelSize.xy;
                float  teA = smoothstep(tx.y * 2.5, 0.0, abs(uv.y - barH)) * topM;
                float  beA = smoothstep(tx.y * 2.5, 0.0, abs(uv.y - (1.0 - barH))) * botM;
                col += (teA + beA) * barCol.rgb * opacity * 0.65;

                return col;
            }

            // ─── Fragment ──────────────────────────────────────────────────────
            // Total texture sample budget:
            //   Roberts Cross  :  4
            //   Chromatic abr. :  3
            //   Interlace      :  1
            //   Glitch tear    :  1
            //   Sharpen USM    :  4
            //   ─────────────────
            //   TOTAL          : 13   (safely within hardware limits)
            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.texcoord;
                float  time = _Time.y;

                // ── 0. UV transforms (zero texture samples) ──────────────────
                float2 uvW = uv;
                if (_FocusBreathAmt > 0.0001)
                    uvW = FocusBreathUV(uvW, _FocusBreathAmt, time);
                if (_HeatHazeStrength > 0.001)
                    uvW = HeatHazeUV(uvW, _HeatHazeStrength, time);
                if (_BarrelDistort > 0.001)
                    uvW = BarrelUV(uvW, _BarrelDistort);

                // Lens corners pushed outside [0,1] → black
                if (uvW.x < 0.0 || uvW.x > 1.0 || uvW.y < 0.0 || uvW.y > 1.0)
                    return float4(0, 0, 0, 1);

                // ── 1. Roberts Cross edge detect — 4 samples (original UV) ───
                float edgeStr = 0.0;
                if (_OutlineIntensity > 0.001)
                    edgeStr = EdgeDetect(uv);

                // ── 2. Chromatic aberration — 3 samples ──────────────────────
                float caOsc  = 1.0 + sin(time * 2.71) * _CaOscillation * 0.40;
                float2 caVec = (uvW - 0.5) * _ChromaticStrength * caOsc * 0.013;
                caVec.x     += _GlitchIntensity * (frac(_GlitchSeed * 41.3) - 0.5) * 0.02;
                float  r     = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvW + caVec).r;
                float  g     = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvW        ).g;
                float  b     = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvW - caVec).b;
                float3 col   = float3(r, g, b);

                // ── 3. Interlace shimmer — 1 sample ──────────────────────────
                if (_InterlaceStrength > 0.001)
                    col = InterlaceGhost(uvW, col, _InterlaceStrength);

                // ── 4. Glitch tear — 1 sample ────────────────────────────────
                if (_GlitchIntensity > 0.001)
                    col = GlitchTears(uvW, col, _GlitchIntensity, _GlitchSeed);

                // ── 5. Sharpen USM — 4 samples ───────────────────────────────
                if (_Sharpness > 0.001)
                    col = Sharpen(uvW, col, _Sharpness);

                // ── 6. Exposure ──────────────────────────────────────────────
                col *= _Exposure;

                // ── 7. S-curve contrast ──────────────────────────────────────
                col = SCurve(saturate(col), _Contrast);

                // ── 8. Shadow lift — additive fill into dark areas ───────────
                //    Applied before black crush so the floor survives crushing.
                col += _ShadowLift * (1.0 - smoothstep(0.0, 0.30, Luma(col)));

                // ── 9. Black crush (softer coefficient) ──────────────────────
                col = max(0.0, col - _BlackCrush * 0.018);

                // ── 10. Luminance floor — prevents scene going unreadable ─────
                {
                    float deficit = max(0.0, _MinLuminance - Luma(col));
                    col += deficit * 0.80;
                }

                // ── 11. Phosphor green-cyan grade ────────────────────────────
                col = PhosphorGrade(col, _TealShadows);

                // ── 12. Neon highlight push (with flicker) ────────────────────
                col = Neonize(col, _NeonThreshold, _NeonBloom,
                              _NeonSaturation, _NeonHuePush,
                              _NeonFlickerAmt, time);

                // ── 13. Horizon haze ─────────────────────────────────────────
                if (_FogDensity > 0.001)
                    col = NeonFog(col, uv, _FogDensity);

                col = saturate(col);

                // ── 14. Neon outline ─────────────────────────────────────────
                if (_OutlineIntensity > 0.001 && edgeStr > 0.08)
                {
                    float e = smoothstep(0.08, 0.55, edgeStr) * _OutlineIntensity;
                    col     = lerp(col, _OutlineColor.rgb, e * 0.50);
                    col    += _OutlineColor.rgb
                            * smoothstep(0.05, 0.30, edgeStr)
                            * _OutlineIntensity * 0.20;
                }

                // ── 15. Signal dropout (procedural, no extra samples) ─────────
                if (_GlitchIntensity > 0.001)
                    col = SignalDropout(col, uv, _GlitchIntensity * 0.60, _GlitchSeed + 0.5);

                // ── 16. Digital grain ────────────────────────────────────────
                if (_GrainIntensity > 0.001)
                {
                    float  blkSz = _GrainSize * 1.6;
                    float2 gp    = floor(uv / (blkSz * _BlitTexture_TexelSize.x));
                    float  seed  = frac(time * 0.07193);
                    float  nr    = hash2(gp + seed * 317.5) * 2.0 - 1.0;
                    float  lG    = Luma(col);
                    float  nMsk  = smoothstep(0.0, 0.14, lG)
                                 * (1.0 - smoothstep(0.82, 1.0, lG));
                    col         += nr * _GrainIntensity * nMsk;
                    float2 gp2   = floor(uv * _BlitTexture_TexelSize.zw);
                    col         += (hash2(gp2 + seed * 631.3) * 2.0 - 1.0)
                                 * _GrainIntensity * 0.22 * nMsk;
                }

                // ── 17. CMOS banding ─────────────────────────────────────────
                col += DigitalBanding(uv, time);

                // ── 18. Animated drifting scanlines ──────────────────────────
                if (_ScanlineIntensity > 0.001)
                    col *= Scanlines(uv, _ScanlineDensity, _ScanlineIntensity,
                                     _ScanDriftSpeed, time);

                // ── 19. Breathing vignette ───────────────────────────────────
                if (_VignetteIntensity > 0.001)
                {
                    float breath = sin(time * 0.83) * 0.07 + 0.93;
                    float2 uvC   = uv - 0.5;
                    uvC.x       *= 0.72;
                    float vig    = 1.0 - smoothstep(0.25, 1.5, dot(uvC, uvC) * 4.0)
                                 * _VignetteIntensity;
                    // Anti-crush: vignette can darken by at most 65% of the setting
                    vig  = max(vig, 1.0 - _VignetteIntensity * 0.65);
                    col *= vig * breath;
                }

                // ── 20. Camera reticle ───────────────────────────────────────
                if (_ReticleOpacity > 0.001)
                    col = CameraReticle(col, uv, _ReticleOpacity, _ReticleColor, time);

                // ── 21. Top / bottom data bars ───────────────────────────────
                if (_DataBarOpacity > 0.001)
                    col = DataBars(col, uv, _DataBarOpacity, _DataBarColor, time);

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
