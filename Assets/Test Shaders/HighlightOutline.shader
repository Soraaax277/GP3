Shader "Custom/HighlightOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.2, 0.6, 1, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.15)) = 0.05
    }
    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry+100"
        }

        Pass
        {
            Name "Outline"
            Cull   Front
            ZWrite On
            ZTest  LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS    : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 posHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // World-space extrusion — simple and guaranteed to compile
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 worldPos = TransformObjectToWorld(IN.posOS.xyz) + normalWS * _OutlineWidth;
                OUT.posHCS      = TransformWorldToHClip(worldPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
