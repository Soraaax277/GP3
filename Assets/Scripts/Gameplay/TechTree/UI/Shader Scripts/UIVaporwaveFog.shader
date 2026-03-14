Shader "Custom/UIVaporwaveFog"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}

        _ColorA ("Color A (Deep)", Color) = (0.42, 0.0, 0.58, 1.0)
        _ColorB ("Color B (Mid)", Color) = (0.85, 0.1, 0.55, 1.0)
        _ColorC ("Color C (Highlight)", Color) = (0.0, 0.95, 0.95, 1.0)
        _BaseOpacity ("Base Opacity", Range(0, 1)) = 0.45

        _FogOpacity ("Fog Opacity", Range(0, 1)) = 0.85
        _Density ("Fog Density", Range(0, 1)) = 0.75
        _Scale ("Noise Scale", Range(1, 20)) = 5.0
        _Speed ("Drift Speed", Range(0, 2)) = 0.4

        _GlowColor ("Glow Color", Color) = (0.9, 0.3, 1.0, 1.0)
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.8
        _GlowScale ("Glow Scale", Range(1, 20)) = 3.0
        _GlowSpeed ("Glow Speed", Range(0, 2)) = 0.6

        _ScanlineColor ("Scanline Color", Color) = (0.8, 0.0, 1.0, 1.0)
        _ScanlineOpacity ("Scanline Opacity", Range(0, 1)) = 0.12
        _ScanlineCount ("Scanline Count", Range(10, 200)) = 60.0
        _ScanlineSpeed ("Scanline Speed", Range(0, 2)) = 0.3

        // Geometric lines
        _LineColor ("Line Color", Color) = (0.0, 1.0, 1.0, 1.0)
        _LineOpacity ("Line Opacity", Range(0, 1)) = 0.6
        _LineCount ("Line Count", Range(1, 20)) = 6.0
        _LineThickness ("Line Thickness", Range(0.001, 0.02)) = 0.004
        _LineSpeed ("Line Speed", Range(0, 2)) = 0.2
        _LineFadeLength ("Line Fade Length", Range(0.01, 1.0)) = 0.4

        _FadeLeft   ("Fade Left",   Range(0, 1)) = 0.05
        _FadeRight  ("Fade Right",  Range(0, 1)) = 0.05
        _FadeTop    ("Fade Top",    Range(0, 1)) = 0.05
        _FadeBottom ("Fade Bottom", Range(0, 1)) = 0.05

        _ManualTime ("Manual Time", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half4 _ColorC;
                half _BaseOpacity;
                half _FogOpacity;
                half _Density;
                float _Scale;
                float _Speed;
                half4 _GlowColor;
                half _GlowIntensity;
                float _GlowScale;
                float _GlowSpeed;
                half4 _ScanlineColor;
                half _ScanlineOpacity;
                float _ScanlineCount;
                float _ScanlineSpeed;
                half4 _LineColor;
                half _LineOpacity;
                float _LineCount;
                float _LineThickness;
                float _LineSpeed;
                float _LineFadeLength;
                float _FadeLeft;
                float _FadeRight;
                float _FadeTop;
                float _FadeBottom;
                float _ManualTime;
            CBUFFER_END

            // -------------------------------------------------------
            // Helpers
            // -------------------------------------------------------

            float hash(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float hash1(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * valueNoise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.1;
                }
                return value;
            }

            half3 vaporwaveGradient(float t)
            {
                half3 colAB = lerp(_ColorA.rgb, _ColorB.rgb, saturate(t * 2.0));
                half3 colBC = lerp(_ColorB.rgb, _ColorC.rgb, saturate(t * 2.0 - 1.0));
                return lerp(colAB, colBC, step(0.5, t));
            }

            float gaussian(float dist, float sigma)
            {
                return exp(-(dist * dist) / (2.0 * sigma * sigma));
            }

            float3 glowLayer(float2 uv, float time)
            {
                float2 cell = floor(uv);
                float2 local = frac(uv);
                float3 glow = 0.0;

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        float2 neighbor = cell + float2(x, y);
                        float2 blobPos = float2(
                            hash(neighbor + float2(0.2, 0.8)),
                            hash(neighbor + float2(0.6, 0.4))
                        );
                        blobPos += float2(
                            sin(time * 0.4 + hash(neighbor) * 6.28) * 0.15,
                            cos(time * 0.3 + hash(neighbor + 1.0) * 6.28) * 0.15
                        );
                        blobPos = frac(blobPos);

                        float dist = length(local - float2(x, y) - blobPos);
                        float blob = gaussian(dist, 0.35);

                        float colorSeed = hash(neighbor + float2(0.5, 0.5));
                        float3 blobColor = vaporwaveGradient(colorSeed);
                        glow += blob * blobColor;
                    }
                }
                return saturate(glow);
            }

            float scanlines(float2 uv, float time)
            {
                float scroll = frac(time * _ScanlineSpeed);
                float scanVal = sin((uv.y - scroll) * _ScanlineCount * 3.14159);
                return smoothstep(0.6, 1.0, scanVal);
            }

            // -------------------------------------------------------
            // Geometric Lines
            //
            // Each line is defined by:
            //   - A random angle (snapped to 0, 45, 90 degrees for that
            //     geometric/grid vaporwave feel)
            //   - A random offset position on screen
            //   - A random speed and direction of travel
            //   - A random length (controlled by fade)
            //   - A color sampled from the vaporwave gradient
            //
            // Distance from a point to an infinite line is used,
            // then we clip the line to a finite segment using
            // projected T value along the line direction.
            // -------------------------------------------------------

            float sdSegment(float2 uv, float2 a, float2 b)
            {
                float2 pa = uv - a;
                float2 ba = b - a;
                float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
                return length(pa - ba * h);
            }

            float4 geometricLines(float2 uv, float time)
            {
                float4 result = float4(0, 0, 0, 0);

                int count = (int)_LineCount;
                for (int i = 0; i < count; i++)
                {
                    float fi = float(i);

                    // Random angle snapped to 0, 45, 90, 135 degrees
                    float angleRand = hash1(fi * 3.71 + 0.1);
                    float angleIndex = floor(angleRand * 4.0);
                    float angle = angleIndex * 3.14159 * 0.25;

                    float2 dir = float2(cos(angle), sin(angle));
                    float2 perp = float2(-dir.y, dir.x);

                    // Random starting offset across the screen
                    float offset = hash1(fi * 7.13 + 1.3);

                    // Each line travels perpendicular to itself over time
                    float speed = (hash1(fi * 2.57 + 4.1) * 0.8 + 0.2) * _LineSpeed;
                    float travel = frac(offset + time * speed);

                    // Position along perpendicular axis
                    float2 lineCenter = float2(0.5, 0.5) + perp * (travel - 0.5) * 2.0;

                    // Random line length via half-extent
                    float halfLen = hash1(fi * 5.39 + 2.7) * 0.3 + 0.1;
                    float2 a = lineCenter - dir * halfLen;
                    float2 b = lineCenter + dir * halfLen;

                    // Distance from pixel to segment
                    float dist = sdSegment(uv, a, b);

                    // Soft line using gaussian on distance
                    float lineMask = gaussian(dist, _LineThickness);

                    // Fade at the ends of the segment using projected T
                    float2 pa = uv - a;
                    float2 ba = b - a;
                    float projT = dot(pa, ba) / max(dot(ba, ba), 0.0001);
                    float endFade = smoothstep(0.0, _LineFadeLength, projT)
                                  * smoothstep(0.0, _LineFadeLength, 1.0 - projT);

                    lineMask *= endFade;

                    // Fade lines that are near screen edges (reuse travel for flicker)
                    float edgeFade = smoothstep(0.0, 0.1, travel) * smoothstep(0.0, 0.1, 1.0 - travel);
                    lineMask *= edgeFade;

                    // Color from gradient, each line gets its own hue
                    float colorT = hash1(fi * 1.91 + 0.5);
                    half3 lineCol = vaporwaveGradient(colorT);

                    result.rgb += lineCol * lineMask;
                    result.a = max(result.a, lineMask);
                }

                result.rgb = saturate(result.rgb);
                result.a = saturate(result.a);
                return result;
            }

            // -------------------------------------------------------

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _ManualTime;

                float edgeFade = smoothstep(0.0, _FadeLeft,   IN.uv.x)
                               * smoothstep(0.0, _FadeBottom,  IN.uv.y)
                               * smoothstep(0.0, _FadeRight,   1.0 - IN.uv.x)
                               * smoothstep(0.0, _FadeTop,     1.0 - IN.uv.y);

                // Fog
                float2 fogUV = IN.uv * _Scale;
                float layer1 = fbm(fogUV + float2(time * _Speed * 0.6, time * _Speed * 0.3));
                float layer2 = fbm(fogUV + float2(-time * _Speed * 0.4, time * _Speed * 0.2) + float2(3.7, 1.9));

                float fogMask = (layer1 * 0.6 + layer2 * 0.4);
                fogMask = pow(fogMask, 0.5);
                fogMask = smoothstep(0.2 - _Density * 0.3, 1.0, fogMask);
                fogMask *= edgeFade;

                float gradientT = (layer1 * 0.5 + layer2 * 0.5);
                half3 fogColor = vaporwaveGradient(gradientT);

                // Glow
                float2 glowUV = IN.uv * _GlowScale;
                float3 glow = glowLayer(glowUV, time * _GlowSpeed);
                glow *= _GlowColor.rgb * _GlowIntensity * edgeFade;

                // Scanlines
                float scan = scanlines(IN.uv, time);
                half3 scanColor = _ScanlineColor.rgb * scan * _ScanlineOpacity * edgeFade;

                // Geometric lines
                float4 lines = geometricLines(IN.uv, time * _LineSpeed);
                half3 lineContrib = lines.rgb * _LineColor.rgb * _LineOpacity * edgeFade;

                // Base
                half4 base = _ColorA;
                base.a = _BaseOpacity * edgeFade * IN.color.a;

                // Composite
                half4 result;
                result.rgb = lerp(base.rgb, fogColor, fogMask * _FogOpacity);
                result.rgb = lerp(result.rgb, result.rgb + glow, saturate(length(glow)));
                result.rgb += scanColor;
                result.rgb += lineContrib;
                result.rgb = saturate(result.rgb);
                result.a = clamp(base.a + fogMask * _FogOpacity, 0, 1);
                result.a *= IN.color.a;

                return result;
            }
            ENDHLSL
        }
    }
}