Shader "Custom/FresnelGlow"
{
    Properties
    {
        _GlowColor     ("Glow Color",    Color)         = (1, 1, 1, 1)
        _GlowPower     ("Fresnel Power", Range(0.5, 8)) = 3.0
        _GlowIntensity ("Intensity",     Range(0, 4))   = 1.8
    }
    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FresnelGlow"

            // Additive blend — glow brightens whatever is behind it
            Blend  SrcAlpha One
            ZWrite Off
            Cull   Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float  _GlowPower;
                float  _GlowIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS    : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 posHCS    : SV_POSITION;
                float3 normalWS  : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posHCS    = TransformObjectToHClip(IN.posOS.xyz);
                OUT.normalWS  = TransformObjectToWorldNormal(IN.normalOS);
                float3 wp     = TransformObjectToWorld(IN.posOS.xyz);
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(wp);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float rim  = 1.0 - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float glow = pow(rim, _GlowPower) * _GlowIntensity;
                return half4(_GlowColor.rgb, glow);
            }
            ENDHLSL
        }
    }
}
