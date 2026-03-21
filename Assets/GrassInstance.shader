Shader "Custom/URP/GrassInstance"
{
    Properties
    {
        [Header(Grass Colors)]
        _BottomColor    ("Bottom Color",        Color)           = (0.10, 0.30, 0.10, 1)
        _TopColor       ("Top Color",           Color)           = (0.40, 0.80, 0.30, 1)
        _ColorVariation ("Color Variation",     Range(0.0, 0.4)) = 0.12

        [Header(Wind)]
        _WindStrength   ("Wind Strength",       Range(0.0, 0.5)) = 0.08
        _WindSpeed      ("Wind Speed",          Range(0.0, 5.0)) = 1.8
        _WindScale      ("Wind Scale",          Range(0.1, 5.0)) = 1.2

        [Header(Shape)]
        _Cutoff         ("Alpha Cutoff",        Range(0.0, 1.0)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BottomColor;
                float4 _TopColor;
                float  _ColorVariation;
                float  _WindStrength;
                float  _WindSpeed;
                float  _WindScale;
                float  _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash1(float n) { return frac(sin(n) * 43758.5453); }

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float  tipMask    = IN.uv.y;
                float3 worldPivot = UNITY_MATRIX_M._m03_m13_m23;

                float  t    = _Time.y * _WindSpeed;
                float2 wPos = worldPivot.xz * _WindScale;
                float  wind = sin(wPos.x * 1.0 + t * 1.00) * 0.5
                            + sin(wPos.y * 0.8 + t * 0.73) * 0.5;
                wind *= _WindStrength * tipMask;

                float4 pos  = IN.positionOS;
                pos.x      += wind;
                pos.z      += wind * 0.4;

                float3 worldPos = TransformObjectToWorld(pos.xyz);
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.worldPos    = worldPos;
                OUT.uv          = IN.uv;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float edgeFade = smoothstep(0.0, 0.18, IN.uv.x)
                               * smoothstep(1.0, 0.82, IN.uv.x);
                clip(edgeFade - _Cutoff);

                float3 col = lerp(_BottomColor.rgb, _TopColor.rgb, IN.uv.y);

                float3 pivot = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float  seed  = dot(floor(pivot * 3.1), float3(1, 57, 113));
                float  vary  = hash1(seed) * 2.0 - 1.0;
                col += vary * _ColorVariation;
                col  = saturate(col);

                Light  mainLight = GetMainLight();
                float3 normal    = normalize(IN.normalWS);
                float  ndotl     = saturate(dot(normal, mainLight.direction)) * 0.6 + 0.4;
                col *= mainLight.color.rgb * ndotl;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}