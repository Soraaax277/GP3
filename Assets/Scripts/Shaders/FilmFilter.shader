Shader "Custom/URP/FilmFilter"
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

            float4 _ColorTint;
            float  _TintStrength;
            float  _ScanlineIntensity;
            float  _ScanlineSpacing;
            float  _ScanlineSpeed;
            float  _GrainIntensity;
            float  _GrainSize;
            float  _ScratchIntensity;
            float  _VignetteIntensity;
            float  _VignetteSmoothness;
            float  _ChromaticStrength;
            float  _FlickerIntensity;

            // Color grading
            float  _Saturation;
            float  _SepiaStrength;
            float  _FadeStrength;
            float  _Brightness;        // overall exposure multiplier
            float  _HighlightBoost;    // extra quadratic lift for whites

            // Overlay
            float  _OverlayStrength;
            float  _EdgeBurn;
            float  _CreaseIntensity;
            float  _DustIntensity;

            // Framing
            float  _LetterboxAmount;
            float  _SquareAmount;      // 0 = original aspect, 1 = scene warped to square

            // Zoetrope
            float  _ZoetropeStrength;
            float  _SlitCount;
            float  _SlitSpeed;
            float  _SlitWidth;
            float  _CylinderCurve;

            float hash1(float n) { return frac(sin(n) * 43758.5453); }
            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            float grain(float2 uv, float time, float size)
            {
                float2 p = floor(uv / size) + float2(time * 7.3, time * 3.7);
                return hash2(p) * 2.0 - 1.0;
            }

            float scratch(float2 uv, float time, float intensity)
            {
                if (intensity <= 0.001) return 0.0;
                float result = 0.0;
                float t = floor(time * 12.0);
                for (int i = 0; i < 4; i++)
                {
                    float fi     = float(i);
                    float seed   = hash1(t * 31.7 + fi * 17.3);
                    if (seed > intensity) continue;
                    float xPos   = hash1(t * 13.1 + fi * 7.9);
                    float w      = hash1(t * 5.3  + fi * 2.1) * 0.003 + 0.0005;
                    float bright = hash1(t * 9.7  + fi * 4.3) * 2.0 - 0.5;
                    result      += bright * smoothstep(w, 0.0, abs(uv.x - xPos));
                }
                return result;
            }

            // Crease lines - simulates folded/creased photo paper
            float creases(float2 uv, float time, float intensity)
            {
                if (intensity <= 0.001) return 0.0;
                float result = 0.0;

                float t = floor(time * 0.1);
                for (int i = 0; i < 3; i++)
                {
                    float fi    = float(i);
                    float yPos  = hash1(fi * 13.7 + t * 0.3) * 0.7 + 0.15;
                    float w     = hash1(fi * 7.1) * 0.004 + 0.001;
                    float val   = smoothstep(w, 0.0, abs(uv.y - yPos));
                    result += val * (hash1(fi * 3.3) > 0.5 ? 1.0 : -0.4);
                }

                float vPos = hash1(t * 0.7 + 91.3) * 0.6 + 0.2;
                float vw   = 0.002;
                result += smoothstep(vw, 0.0, abs(uv.x - vPos)) * 0.6;

                return result * intensity;
            }

            // Dust and damage speckles
            float dust(float2 uv, float intensity)
            {
                if (intensity <= 0.001) return 0.0;
                float2 cell = floor(uv * 80.0);
                float  seed = hash2(cell);
                float  blob = 0.0;
                if (seed < intensity * 0.4)
                {
                    float2 cellUV = frac(uv * 80.0) - 0.5;
                    float  r      = hash2(cell + 13.7) * 0.3 + 0.1;
                    blob = smoothstep(r, r * 0.5, length(cellUV)) * hash2(cell + 7.1);
                }

                float2 cell2 = floor(uv * 300.0);
                float  seed2 = hash2(cell2 + 99.1);
                float  speck = (seed2 < intensity * 0.15) ? hash2(cell2 + 3.3) : 0.0;

                return saturate(blob + speck);
            }

            // Burned/dark edges like old photo paper
            float edgeBurn(float2 uv, float intensity)
            {
                float2 e    = uv * (1.0 - uv.yx);
                float  burn = pow(saturate(e.x * e.y * 6.0), 0.3);
                float  noise = hash2(floor(uv * 15.0)) * 0.15;
                burn = saturate(burn - noise * intensity);
                return (1.0 - burn) * intensity;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.texcoord;
                float  time = _Time.y;

                // Letterbox — early-out for black bars (pure overlay, no warp)
                float halfBar = _LetterboxAmount * 0.5;
                if (uv.y < halfBar || uv.y > (1.0 - halfBar))
                    return float4(0.0, 0.0, 0.0, 1.0);

                // Square warp — remaps UVs so the scene itself is squished toward 1:1.
                // Uses the texture's actual pixel dimensions to compute the true aspect ratio.
                // At _SquareAmount = 1 the horizontal axis is compressed by 1/aspect so the
                // rendered content fills a square region. No black bars are added; the image
                // fills the whole screen but appears geometrically square.
                if (_SquareAmount > 0.001)
                {
                    float aspect   = _BlitTexture_TexelSize.z / _BlitTexture_TexelSize.w; // width / height
                    float squishX  = lerp(1.0, 1.0 / aspect, _SquareAmount);
                    uv.x           = 0.5 + (uv.x - 0.5) * squishX;
                }

                // Zoetrope - cylindrical warp (applied before sampling so the image itself bends)
                if (_ZoetropeStrength > 0.001 && _CylinderCurve > 0.001)
                {
                    float cx = uv.x - 0.5;
                    uv.y     = saturate(0.5 + (uv.y - 0.5) * (1.0 + _CylinderCurve * cx * cx * 4.0));
                }

                // Chromatic aberration
                float2 ca  = float2(_ChromaticStrength * _BlitTexture_TexelSize.x, 0);
                float  r   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + ca).r;
                float  g   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                float  b   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - ca).b;
                float4 col = float4(r, g, b, 1.0);

                // Brightness — simple exposure multiplier applied first so everything scales
                col.rgb *= _Brightness;

                // Highlight boost — quadratic lift: bright pixels get more than dark ones.
                // col^2 is near 0 for darks and near 1 for whites, so this pushes whites up
                // without crushing the shadows.
                col.rgb += _HighlightBoost * col.rgb * col.rgb;

                // Saturation
                float luma = Luma(col.rgb);
                col.rgb = lerp(float3(luma, luma, luma), col.rgb, _Saturation);

                // Color tint
                col.rgb = lerp(col.rgb, col.rgb * _ColorTint.rgb * 1.5, _TintStrength);

                // Sepia tone
                if (_SepiaStrength > 0.001)
                {
                    float  gl   = Luma(col.rgb);
                    float3 sepia;
                    sepia.r = gl * 1.08;
                    sepia.g = gl * 0.88;
                    sepia.b = gl * 0.62;
                    col.rgb = lerp(col.rgb, sepia, _SepiaStrength);
                }

                // Fade / bleach
                if (_FadeStrength > 0.001)
                    col.rgb = lerp(col.rgb, float3(0.82f, 0.76f, 0.62f), _FadeStrength * 0.5);

                // Scanlines
                if (_ScanlineIntensity > 0.001)
                {
                    float scanPos = fmod(uv.y * _BlitTexture_TexelSize.w + time * _ScanlineSpeed,
                                         _ScanlineSpacing) / _ScanlineSpacing;
                    float scanVal = smoothstep(0.0, 0.35, scanPos)
                                  * smoothstep(1.0, 0.65, scanPos);
                    col.rgb *= lerp(1.0, scanVal, _ScanlineIntensity * 0.5 + 0.5);
                }

                // Film grain
                if (_GrainIntensity > 0.001)
                    col.rgb += grain(uv, time, _GrainSize * _BlitTexture_TexelSize.x) * _GrainIntensity;

                // Scratch lines
                col.rgb += scratch(uv, time, _ScratchIntensity) * _ScratchIntensity;

                // === OVERLAY EFFECTS (gated by _OverlayStrength) ===
                if (_OverlayStrength > 0.001)
                {
                    if (_CreaseIntensity > 0.001)
                        col.rgb += creases(uv, time, _CreaseIntensity) * _OverlayStrength;

                    if (_DustIntensity > 0.001)
                    {
                        float d = dust(uv, _DustIntensity);
                        col.rgb = lerp(col.rgb, float3(0.9, 0.87, 0.75), d * _OverlayStrength * 0.8);
                    }

                    if (_EdgeBurn > 0.001)
                    {
                        float burn = edgeBurn(uv, _EdgeBurn);
                        col.rgb *= (1.0 - burn * _OverlayStrength);
                    }
                }

                // Vignette
                if (_VignetteIntensity > 0.001)
                {
                    float2 vig  = uv * (1.0 - uv.yx);
                    float  vPow = pow(saturate(vig.x * vig.y * 15.0), _VignetteSmoothness);
                    col.rgb    *= lerp(1.0 - _VignetteIntensity, 1.0, vPow);
                }

                // Flicker
                if (_FlickerIntensity > 0.001)
                    col.rgb *= 1.0 + (hash1(floor(time * 20.0)) * 2.0 - 1.0) * _FlickerIntensity * 0.08;

                // Zoetrope - rotating slit mask + side vignette (applied in screen space)
                if (_ZoetropeStrength > 0.001)
                {
                    float screenX      = IN.texcoord.x;
                    float drumPos      = frac(screenX + frac(time * _SlitSpeed * 0.12));
                    float withinPeriod = frac(drumPos * _SlitCount);
                    float softEdge     = 0.025;
                    float inSlit       = smoothstep(0.0, softEdge, withinPeriod)
                                       * (1.0 - smoothstep(_SlitWidth - softEdge, _SlitWidth, withinPeriod));
                    col.rgb *= lerp(1.0, inSlit, _ZoetropeStrength);

                    float sideVig = smoothstep(0.0, 0.10, screenX) * smoothstep(1.0, 0.90, screenX);
                    col.rgb      *= lerp(1.0, sideVig, _ZoetropeStrength * 0.80);
                }

                return float4(saturate(col.rgb), 1.0);
            }
            ENDHLSL
        }
    }
}
