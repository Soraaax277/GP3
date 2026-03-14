Shader "Custom/UIProceduralDustFog"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}

        _BaseColor ("Base Color", Color) = (0.76, 0.60, 0.35, 1.0)
        _BaseOpacity ("Base Opacity", Range(0, 1)) = 0.45

        _FogColor ("Dust Color", Color) = (0.89, 0.72, 0.42, 1.0)
        _FogOpacity ("Dust Opacity", Range(0, 1)) = 0.8

        _GrainColor ("Grain Color", Color) = (0.95, 0.85, 0.55, 1.0)
        _GrainOpacity ("Grain Opacity", Range(0, 1)) = 0.35
        _GrainScale ("Grain Scale", Range(10, 100)) = 45.0
        _GrainSpeed ("Grain Speed", Range(0, 2)) = 0.8

        _Density ("Dust Density", Range(0, 1)) = 0.75
        _Scale ("Noise Scale", Range(1, 20)) = 6.0
        _Speed ("Drift Speed", Range(0, 2)) = 0.4
        _Turbulence ("Turbulence", Range(0, 2)) = 1.2

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
                half4 _BaseColor;
                half _BaseOpacity;
                half4 _FogColor;
                half _FogOpacity;
                half4 _GrainColor;
                half _GrainOpacity;
                float _GrainScale;
                float _GrainSpeed;
                half _Density;
                float _Scale;
                float _Speed;
                float _Turbulence;
                float _FadeLeft;
                float _FadeRight;
                float _FadeTop;
                float _FadeBottom;
                float _ManualTime;
            CBUFFER_END

            float hash(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float hashGrain(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 74.51);
                return frac(p.x * p.y);
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

            float turbulentFbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * abs(valueNoise(p * frequency) - 0.5);
                    amplitude *= 0.5;
                    frequency *= 2.3;
                }
                return value;
            }

            float grainNoise(float2 p, float time)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float grain = 0.0;
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        float2 neighbor = cell + float2(x, y);
                        float2 grainPos = float2(
                            hashGrain(neighbor + float2(0.1, 0.9)),
                            hashGrain(neighbor + float2(0.7, 0.3))
                        );
                        grainPos.x += time * 0.08;
                        grainPos = frac(grainPos);
                        float dist = length(local - float2(x, y) - grainPos);
                        float particle = 1.0 - smoothstep(0.0, 0.18, dist);
                        float brightness = hashGrain(neighbor + float2(time * 0.01, 0.0));
                        grain += particle * brightness;
                    }
                }
                return saturate(grain);
            }

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
                float2 uv = IN.uv * _Scale;
                float time = _ManualTime * _Speed;

                // Edge fade applied to everything including the base wash
                float edgeFade = smoothstep(0.0, _FadeLeft,   IN.uv.x)
                               * smoothstep(0.0, _FadeBottom,  IN.uv.y)
                               * smoothstep(0.0, _FadeRight,   1.0 - IN.uv.x)
                               * smoothstep(0.0, _FadeTop,     1.0 - IN.uv.y);

                float layer1 = fbm(uv + float2(time * 0.5, time * 0.2));
                float layer2 = turbulentFbm(uv * _Turbulence + float2(-time * 0.35, time * 0.15) + float2(3.7, 1.9));

                float fog = (layer1 * 0.35 + layer2 * 0.65);
                fog = pow(fog, 0.5);
                fog = smoothstep(0.2 - _Density * 0.3, 1.0, fog);
                fog *= edgeFade;

                float2 grainUV = IN.uv * _GrainScale;
                float grain = grainNoise(grainUV, _ManualTime * _GrainSpeed);
                grain *= edgeFade;

                // Base wash now also fades at edges
                half4 base = _BaseColor;
                base.a = _BaseOpacity * edgeFade * IN.color.a;

                half4 dustLayer = _FogColor;
                dustLayer.a = fog * _FogOpacity * IN.color.a;

                half4 grainLayer = _GrainColor;
                grainLayer.a = grain * _GrainOpacity * IN.color.a;

                half4 result;
                result.rgb = lerp(base.rgb, dustLayer.rgb, dustLayer.a);
                result.rgb = lerp(result.rgb, grainLayer.rgb, grainLayer.a);
                result.a = clamp(base.a + dustLayer.a + grainLayer.a * 0.5, 0, 1);

                return result;
            }
            ENDHLSL
        }
    }
}