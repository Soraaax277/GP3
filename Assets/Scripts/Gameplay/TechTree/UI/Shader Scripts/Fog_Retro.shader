Shader "TechTree/Fog_Retro"
{
    Properties
    {
        [MainTexture] _MainTex     ("Texture",         2D)          = "white" {}
        _ColorDark   ("Dark Color",    Color)       = (0.08, 0.08, 0.08, 1)
        _ColorLight  ("Light Color",   Color)       = (0.90, 0.90, 0.90, 1)
        _Dissolve    ("Dissolve",      Range(0, 1)) = 0
        _StaticSpeed ("Static Speed",  Float)       = 40.0
        _ScanFreq    ("Scanline Freq", Float)       = 110.0
        _ScanAmp     ("Scanline Amp",  Range(0, 1)) = 0.28
        _NoiseScale  ("Noise Scale",   Float)       = 160.0
        _Glitch      ("Glitch",        Range(0, 1)) = 0.07
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _UI_UnscaledTime;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ColorDark, _ColorLight;
                float  _Dissolve, _StaticSpeed, _ScanFreq, _ScanAmp, _NoiseScale, _Glitch;
            CBUFFER_END

            float nrand(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
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
                float t = _UI_UnscaledTime;

                // Horizontal glitch — randomly shifts rows
                float row    = floor(IN.uv.y * 40.0);
                float rng    = frac(sin(row * 91.3 + floor(t * 8.0) * 17.7) * 43758.5);
                float shift  = (rng - 0.5) * _Glitch *
                               step(0.93, frac(sin(row * 13.7 + floor(t * 3.0)) * 9301.0));
                float2 uv    = IN.uv + float2(shift, 0.0);

                // White noise (fast frame-by-frame flicker)
                float noise  = nrand(uv * _NoiseScale + floor(t * _StaticSpeed));

                // Scanlines
                float scan   = sin(uv.y * _ScanFreq * 3.14159) * 0.5 + 0.5;
                scan         = lerp(1.0, scan, _ScanAmp);

                // Dissolve: stable per-pixel threshold
                float stable = nrand(uv * _NoiseScale);
                float edge   = smoothstep(_Dissolve - 0.05, _Dissolve + 0.05, stable);

                float combined = noise * scan * edge;
                float3 col     = lerp(_ColorDark.rgb, _ColorLight.rgb, combined);

                // Overall alpha fades as dissolve increases
                float alpha = (1.0 - _Dissolve) * 0.92 * _ColorDark.a;
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
