Shader "Custom/URP/EraBlur"
{
    Properties
    {
        _MainTex    ("Source", 2D)          = "white" {}
        _BlurSize   ("Blur Size",   Float)  = 3.0
        _Darkness   ("Darkness",    Range(0,1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        // Pass 0 — Horizontal blur
        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment fragH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float  _BlurSize;
            float  _Darkness;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 9-tap Gaussian weights
            static const float weights[5] = { 0.227027, 0.194595, 0.121622, 0.054054, 0.016216 };

            float4 fragH(Varyings IN) : SV_Target
            {
                float2 tex  = _MainTex_TexelSize.xy * _BlurSize;
                float4 col  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * weights[0];
                UNITY_UNROLL
                for (int i = 1; i < 5; i++)
                {
                    col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(tex.x * i, 0)) * weights[i];
                    col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(tex.x * i, 0)) * weights[i];
                }
                col.rgb *= (1.0 - _Darkness);
                return col;
            }
            ENDHLSL
        }

        // Pass 1 — Vertical blur
        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment fragV
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float  _BlurSize;
            float  _Darkness;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            static const float weights[5] = { 0.227027, 0.194595, 0.121622, 0.054054, 0.016216 };

            float4 fragV(Varyings IN) : SV_Target
            {
                float2 tex  = _MainTex_TexelSize.xy * _BlurSize;
                float4 col  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * weights[0];
                UNITY_UNROLL
                for (int i = 1; i < 5; i++)
                {
                    col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, tex.y * i)) * weights[i];
                    col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, tex.y * i)) * weights[i];
                }
                col.rgb *= (1.0 - _Darkness);
                return col;
            }
            ENDHLSL
        }
    }
}
