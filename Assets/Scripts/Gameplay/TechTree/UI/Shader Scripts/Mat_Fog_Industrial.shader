Shader "TechTree/Fog_Industrial"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        _Color       ("Tint",          Color)       = (0.62, 0.48, 0.30, 1)
        _Dissolve    ("Dissolve",      Range(0, 1)) = 0
        _Speed       ("Scroll Speed",  Float)       = 0.04
        _Scale       ("Noise Scale",   Float)       = 3.5
        _Density     ("Smoke Density", Range(0, 2)) = 1.2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Transparent" 
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes 
            { 
                float4 positionOS : POSITION; 
                float2 uv : TEXCOORD0; 
            };
            
            struct Varyings   
            { 
                float4 positionHCS : SV_POSITION; 
                float2 uv : TEXCOORD0; 
            };

            TEXTURE2D(_MainTex); 
            SAMPLER(sampler_MainTex);

            // Shared global set by TechTreeWindowManager.Update() via
            // Shader.SetGlobalFloat("_UI_UnscaledTime", Time.unscaledTime)
            float _UI_UnscaledTime;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Dissolve;
                float  _Speed;
                float  _Scale;
                float  _Density;
            CBUFFER_END

            float hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float smoothNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i),               hash(i + float2(1, 0)), u.x),
                    lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0, a = 0.5;
                UNITY_UNROLL
                for (int i = 0; i < 5; i++)
                {
                    v += a * smoothNoise(p);
                    p  = p * 2.0 + float2(100, 100);
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t  = _UI_UnscaledTime * _Speed;

                float2 uv1 = IN.uv * _Scale + float2(t,       t * 0.6);
                float2 uv2 = IN.uv * _Scale + float2(-t * 0.7, t * 0.4);
                float smoke = saturate(fbm(uv1) * 0.6 + fbm(uv2) * 0.4);
                smoke = saturate(smoke * _Density);

                // Soft vertical fade at top/bottom edges
                float vignette = smoothstep(0.0, 0.12, IN.uv.y) *
                                 smoothstep(1.0, 0.88, IN.uv.y);

                // Dissolve: stable noise threshold — fog burns away from random points
                float dissolveNoise = fbm(IN.uv * 4.0 + 3.7);
                float edge = smoothstep(_Dissolve - 0.1, _Dissolve + 0.1, dissolveNoise);

                float alpha = smoke * vignette * edge * _Color.a;
                return half4(_Color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
}