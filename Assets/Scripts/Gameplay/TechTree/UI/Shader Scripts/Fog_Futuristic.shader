Shader "TechTree/Fog_Futuristic"
{
    Properties
    {
        [MainTexture] _MainTex  ("Texture",           2D)          = "white" {}
        _ColorA      ("Primary Color",   Color)       = (0.00, 0.85, 1.00, 1)
        _ColorB      ("Secondary Color", Color)       = (0.55, 0.00, 1.00, 1)
        _ColorBg     ("Background",      Color)       = (0.02, 0.04, 0.12, 1)
        _Dissolve    ("Dissolve",        Range(0, 1)) = 0
        _GridSpeed   ("Grid Speed",      Float)       = 0.10
        _GridScale   ("Grid Scale",      Float)       = 7.0
        _HexScale    ("Hex Scale",       Float)       = 5.0
        _GlowPulse   ("Glow Pulse",      Float)       = 1.6
        _LineWidth   ("Line Width",      Range(0.01, 0.15)) = 0.045
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
                float4 _ColorA, _ColorB, _ColorBg;
                float  _Dissolve, _GridSpeed, _GridScale, _HexScale, _GlowPulse, _LineWidth;
            CBUFFER_END

            float hash21(float2 p) { p=frac(p*float2(127.1,311.7)); p+=dot(p,p+19.19); return frac(p.x*p.y); }

            float hexDist(float2 p) { p=abs(p); return max(dot(p,normalize(float2(1,1.732))),p.x); }
            float2 hexCoords(float2 uv)
            {
                float2 r=float2(1,1.732),h=r*0.5;
                float2 a=fmod(uv,r)-h, b=fmod(uv-h,r)-h;
                return dot(a,a)<dot(b,b)?a:b;
            }

            float gridLines(float2 uv, float t)
            {
                float2 g = frac((uv+float2(t,-t*0.5)) * _GridScale);
                return 1.0 - smoothstep(0, _LineWidth, min(min(g.x,1-g.x), min(g.y,1-g.y)));
            }

            float hexGrid(float2 uv, float t)
            {
                float2 hc    = hexCoords(uv * _HexScale);
                float  d     = hexDist(hc);
                float  pulse = sin(t * _GlowPulse + hash21(floor(uv*_HexScale-hc)) * 6.28) * 0.5 + 0.5;
                return smoothstep(0.45, 0.50, d) * pulse;
            }

            float scanBeam(float2 uv, float t)
            {
                float b = frac((uv.x+uv.y)*0.5 - t*_GridSpeed);
                return smoothstep(0.98,1,b) + smoothstep(0.02,0,b);
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
                float t    = _UI_UnscaledTime;
                float grid = gridLines(IN.uv, t * _GridSpeed);
                float hex  = hexGrid(IN.uv, t);
                float beam = scanBeam(IN.uv, t);

                // Dissolve: cells vanish cell-by-cell using stable per-cell hash
                float cellRng = hash21(floor(IN.uv * _HexScale * 0.5));
                float edge    = smoothstep(_Dissolve - 0.08, _Dissolve + 0.08, cellRng);

                float  combined = (grid * 0.6 + hex * 0.5 + beam * 0.3) * edge;
                float3 col      = lerp(_ColorBg.rgb, lerp(_ColorA.rgb,_ColorB.rgb,hex), combined);
                col *= sin(t * _GlowPulse) * 0.08 + 0.92; // subtle pulse

                float alpha = saturate(combined * 1.5 + 0.4 * edge) *
                              (1.0 - _Dissolve) * _ColorA.a;
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
