Shader "Custom/URP/SandBed"
{
    Properties
    {
        [MainColor] _BaseColor    ("Color",    Color) = (0.76, 0.65, 0.45, 1)
        [HDR] _EmissionColor      ("Emission", Color) = (0.1, 0.08, 0.05, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        // ForwardLit — flat color + slight emission so sand is visible through water
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float fogFactor : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.fogFactor  = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 col = _BaseColor.rgb + _EmissionColor.rgb;
                col = MixFog(col, IN.fogFactor);
                return float4(col, 1.0);
            }
            ENDHLSL
        }

        // NO ShadowCaster  — sand beds are under water, no shadows needed
        // NO DepthNormals  — sand beds must never produce outlines
    }

    FallBack Off
}
