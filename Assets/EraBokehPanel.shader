Shader "Custom/URP/EraBokehPanel"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurSize       ("Blur Size",    Range(0.0, 8.0))  = 0.0
        _Darkness       ("Darkness",     Range(0.0, 1.0))  = 0.0
        _TintColor      ("Tint Color",   Color)             = (0,0,0,0)
        _TintStrength   ("Tint Strength",Range(0.0, 1.0))  = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Overlay"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);
            float4 _CameraOpaqueTexture_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _BlurSize;
                float  _Darkness;
                float4 _TintColor;
                float  _TintStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float4 screenPos:TEXCOORD0; float4 color:COLOR; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos   = ComputeScreenPos(OUT.positionHCS);
                OUT.color       = IN.color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / IN.screenPos.w;

                float4 col = float4(0,0,0,0);

                if (_BlurSize <= 0.001)
                {
                    col = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
                }
                else
                {
                    // 8 fixed offsets in a circle — cheap, no loop branching
                    float2 ts = _CameraOpaqueTexture_TexelSize.xy * _BlurSize * 3.0;

                    col  = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
                    col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( ts.x,  0));
                    col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(-ts.x,  0));
                    col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( 0,  ts.y));
                    col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( 0, -ts.y));
                    col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( ts.x * 0.707,  ts.y * 0.707));
                    col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(-ts.x * 0.707,  ts.y * 0.707));
                    col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( ts.x * 0.707, -ts.y * 0.707));
                    col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(-ts.x * 0.707, -ts.y * 0.707));
                    col /= 9.0;
                }

                // Darken
                col.rgb *= (1.0 - _Darkness);

                // Per-era tint
                col.rgb = lerp(col.rgb, _TintColor.rgb, _TintStrength);

                return float4(col.rgb, IN.color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
