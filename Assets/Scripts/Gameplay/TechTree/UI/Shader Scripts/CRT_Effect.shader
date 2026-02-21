Shader "Custom/CRT_Effect"
{
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        
        [Header(Color and Brightness)]
        _Tint ("Global Tint", Color) = (1, 1, 1, 1)
        _ScanlineColor ("Scanline Grid Color", Color) = (0, 0, 0, 1)
        _Brightness ("Brightness Boost", Range(1, 5)) = 1.8

        [Header(The Glitch)]
        _NoiseStrength ("Glitch White Noise", Range(0, 1)) = 0.5
        _GlitchTearing ("Glitch Tearing Strength", Range(0, 0.1)) = 0.02
        _GlitchFrequency ("Glitch Frequency", Range(0, 10)) = 3.0
        _GlitchWobble ("Glitch Waviness", Range(0, 1)) = 0.1

        [Header(Scanlines and Roll)]
        _ScanlineCount ("Line Count", Float) = 400
        _ScanlineOpacity ("Line Visibility", Range(0, 1)) = 0.4
        _ScanlineSpeed ("Line Jitter", Float) = 10.0
        _RollSpeed ("Roll Speed", Float) = -0.5
        _RollStrength ("Roll Darkness", Range(0, 0.5)) = 0.15

        [Header(Screen Shape)]
        _CurveX ("Curve Horizontal", Range(0, 0.5)) = 0.1
        _CurveY ("Curve Vertical", Range(-0.5, 0.5)) = 0.1 
        _Zoom ("Zoom", Range(0.5, 1.5)) = 0.95
        _Vignette ("Vignette", Range(0, 2)) = 0.4
        _Aberration ("Chromatic Aberration", Range(0, 0.1)) = 0.01 
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline" = "UniversalPipeline" "Queue"="Transparent" }
        LOD 100
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

            // --- DEFINE CUSTOM TIME VARIABLE ---
            float _UI_UnscaledTime; 

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float4 _ScanlineColor;
                float _Brightness;
                float _NoiseStrength;
                float _GlitchTearing;
                float _GlitchFrequency;
                float _GlitchWobble;
                float _ScanlineCount;
                float _ScanlineOpacity;
                float _ScanlineSpeed;
                float _RollSpeed;
                float _RollStrength;
                float _CurveX;
                float _CurveY;
                float _Zoom;
                float _Aberration;
                float _Vignette;
            CBUFFER_END

            float nrand(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float2 CurveUV(float2 uv)
            {
                float2 centered = uv * 2.0 - 1.0;
                float2 offset = centered.yx; 
                centered.x += centered.x * (offset.x * offset.x) * _CurveX;
                centered.y += centered.y * (offset.y * offset.y) * _CurveY;
                centered *= _Zoom;
                return centered * 0.5 + 0.5;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = CurveUV(IN.uv);

                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    return half4(0, 0, 0, 0);

                // --- INTELLIGENT GLITCH LOGIC ---
                // FIX: Using custom variable _UI_UnscaledTime
                
                float glitchTime = _UI_UnscaledTime * _GlitchFrequency;
                float glitchTrigger = nrand(float2(floor(glitchTime), 0)); 
                float isGlitching = step(0.9, glitchTrigger); 

                float barY = nrand(float2(glitchTime, 1.0)); 
                float wavyY = uv.y + sin(uv.x * 20.0 + _UI_UnscaledTime * 10.0) * _GlitchWobble * 0.05;
                float inBar = step(abs(wavyY - barY), 0.06) * isGlitching;

                float tear = nrand(float2(_UI_UnscaledTime, uv.y)) - 0.5;
                uv.x -= inBar * tear * _GlitchTearing * 5.0;

                float glitchSplit = 0.0;
                if (inBar > 0.5)
                {
                    glitchSplit = (nrand(uv * _UI_UnscaledTime * 5.0) - 0.5) * _GlitchTearing * 20.0;
                }

                float3 color;
                color.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(uv.x - _Aberration + glitchSplit, uv.y)).r;
                color.g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).g;
                color.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(uv.x + _Aberration + glitchSplit * 1.5, uv.y)).b;
                
                // Scanlines
                float scan = sin(uv.y * _ScanlineCount * 3.14 + (_UI_UnscaledTime * _ScanlineSpeed));
                float scanMask = scan * 0.5 + 0.5;
                
                // Rolling Bar
                float roll = sin(uv.y * 3.0 + (_UI_UnscaledTime * _RollSpeed));
                float rollMask = roll * 0.5 + 0.5;

                color *= _Tint.rgb;
                color = lerp(color, _ScanlineColor.rgb, scanMask * _ScanlineOpacity);
                color *= lerp(1.0, 1.0 - _RollStrength, rollMask);

                if (inBar > 0.5)
                {
                    float noise = nrand(uv * _UI_UnscaledTime * 100.0);
                    color += noise * _NoiseStrength;
                }

                float2 vUV = uv * (1.0 - uv.yx);
                float vig = pow(vUV.x * vUV.y * 15.0, _Vignette);
                color *= vig;
                color *= _Brightness;

                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}