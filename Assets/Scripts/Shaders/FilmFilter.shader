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

            // New parameters
            float  _Saturation;
            float  _OverlayStrength;     // overall aged photo overlay intensity
            float  _SepiaStrength;       // push toward sepia tone
            float  _EdgeBurn;            // dark burned border like old photo edges
            float  _CreaseIntensity;     // horizontal/vertical fold crease lines
            float  _DustIntensity;       // dust spots and speckles
            float  _FadeStrength;        // fades image like old bleached photo
            float  _LetterboxAmount;     // black bars top/bottom

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

                // A few fixed horizontal crease lines
                float t = floor(time * 0.1); // creases are nearly static
                for (int i = 0; i < 3; i++)
                {
                    float fi    = float(i);
                    float yPos  = hash1(fi * 13.7 + t * 0.3) * 0.7 + 0.15;
                    float w     = hash1(fi * 7.1) * 0.004 + 0.001;
                    float val   = smoothstep(w, 0.0, abs(uv.y - yPos));
                    // Creases are bright (light scatter) with dark edges
                    result += val * (hash1(fi * 3.3) > 0.5 ? 1.0 : -0.4);
                }

                // One vertical crease
                float vPos = hash1(t * 0.7 + 91.3) * 0.6 + 0.2;
                float vw   = 0.002;
                result += smoothstep(vw, 0.0, abs(uv.x - vPos)) * 0.6;

                return result * intensity;
            }

            // Dust and damage speckles
            float dust(float2 uv, float intensity)
            {
                if (intensity <= 0.001) return 0.0;
                // Large damage blobs
                float2 cell = floor(uv * 80.0);
                float  seed = hash2(cell);
                float  blob = 0.0;
                if (seed < intensity * 0.4)
                {
                    float2 cellUV = frac(uv * 80.0) - 0.5;
                    float  r      = hash2(cell + 13.7) * 0.3 + 0.1;
                    blob = smoothstep(r, r * 0.5, length(cellUV)) * hash2(cell + 7.1);
                }

                // Fine dust specks
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
                // Add some irregular edge noise
                float  noise = hash2(floor(uv * 15.0)) * 0.15;
                burn = saturate(burn - noise * intensity);
                return (1.0 - burn) * intensity;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv   = IN.texcoord;
                float  time = _Time.y;

                // Letterbox
                float halfBar = _LetterboxAmount * 0.5;
                if (uv.y < halfBar || uv.y > (1.0 - halfBar))
                    return float4(0.0, 0.0, 0.0, 1.0);

                // Chromatic aberration
                float2 ca  = float2(_ChromaticStrength * _BlitTexture_TexelSize.x, 0);
                float  r   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + ca).r;
                float  g   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                float  b   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - ca).b;
                float4 col = float4(r, g, b, 1.0);

                // Saturation
                float luma = Luma(col.rgb);
                col.rgb = lerp(float3(luma, luma, luma), col.rgb, _Saturation);

                // Color tint
                col.rgb = lerp(col.rgb, col.rgb * _ColorTint.rgb * 1.5, _TintStrength);

                // Sepia tone
                if (_SepiaStrength > 0.001)
                {
                    float3 sepia;
                    float  gl   = Luma(col.rgb);
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
                    // Crease lines
                    if (_CreaseIntensity > 0.001)
                        col.rgb += creases(uv, time, _CreaseIntensity) * _OverlayStrength;

                    // Dust and damage
                    if (_DustIntensity > 0.001)
                    {
                        float d = dust(uv, _DustIntensity);
                        // Dust is bright (white speck) with dark shadow underneath
                        col.rgb = lerp(col.rgb, float3(0.9, 0.87, 0.75), d * _OverlayStrength * 0.8);
                    }

                    // Edge burn
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

                return float4(saturate(col.rgb), 1.0);
            }
            ENDHLSL
        }
    }
}
