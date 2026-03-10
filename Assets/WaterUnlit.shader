Shader "Custom/WaterUnlit"
{
    Properties
    {
        // ── Required by Unity material.color getter ───────────────────────
        _Color          ("Color",               Color)  = (1,1,1,1)

        // ── Depth Colors ──────────────────────────────────────────────────
        [Header(Colors and Depth)]
        _ShallowColor   ("Shallow Color",       Color)  = (0.18, 0.55, 0.75, 0.82)
        _DeepColor      ("Deep Color",          Color)  = (0.04, 0.14, 0.38, 0.96)
        _DepthMaxDist   ("Depth Max Distance",  Float)  = 2.5

        // ── Foam ──────────────────────────────────────────────────────────
        [Header(Surface Foam)]
        _FoamColor      ("Foam Color",          Color)  = (0.88, 0.94, 1.0,  1.0)
        _FoamThreshold  ("Foam Threshold",      Range(0,1)) = 0.28
        _FoamSpeed      ("Foam Scroll Speed",   Float)  = 0.12
        _FoamScale      ("Foam Noise Scale",    Float)  = 6.0

        // ── Surface Ripples ───────────────────────────────────────────────
        [Header(Ripples)]
        _RippleSpeed    ("Ripple Speed",        Float)  = 0.6
        _RippleScale    ("Ripple Scale",        Float)  = 3.2
        _RippleStrength ("Ripple Strength",     Range(0,1)) = 0.18

        // ── Caustics-style shimmer ────────────────────────────────────────
        [Header(Caustics)]
        _CausticScale   ("Caustic Scale",       Float)  = 8.0
        _CausticSpeed   ("Caustic Speed",       Float)  = 0.35
        _CausticStrength("Caustic Strength",    Range(0,0.6)) = 0.22

        // ── Specular highlight ────────────────────────────────────────────
        [Header(Lighting)]
        _SpecColor2     ("Spec Highlight Color",Color)  = (1.0, 1.0, 1.0, 1.0)
        _SpecPower      ("Spec Sharpness",      Range(1,256)) = 96
        _SpecStrength   ("Spec Strength",       Range(0,1))   = 0.45

        // ── Edge / Intersection Foam ──────────────────────────────────────
        [Header(Intersection)]
        [Toggle] _UseDepthIntersect("Use Depth Intersection Foam", Float) = 1
        _IntersectThreshold("Intersect Foam Width", Range(0.01, 1.5)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent-100"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "WaterUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            
            // URP Keywords
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ── Properties ────────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;          
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthMaxDist;
                float4 _FoamColor;
                float  _FoamThreshold;
                float  _FoamSpeed;
                float  _FoamScale;
                float  _RippleSpeed;
                float  _RippleScale;
                float  _RippleStrength;
                float  _CausticScale;
                float  _CausticSpeed;
                float  _CausticStrength;
                float4 _SpecColor2;
                float  _SpecPower;
                float  _SpecStrength;
                float  _UseDepthIntersect;
                float  _IntersectThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float4 screenPos    : TEXCOORD2;
                float  fogFactor    : TEXCOORD3;
            };

            // ── Procedural Noise Helpers ──────────────────────────────────

            float hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i + float2(0,0)), hash(i + float2(1,0)), u.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), u.x),
                    u.y);
            }

            float fbm(float2 p)
            {
                float v  = 0.0;
                float a  = 0.5;
                float2 s = float2(1.0, 1.0);
                
                // Unrolled loop for slightly better performance
                v += a * valueNoise(p * s);
                s *= 2.0; a *= 0.5;
                v += a * valueNoise(p * s);
                s *= 2.0; a *= 0.5;
                v += a * valueNoise(p * s);
                
                return v;
            }

            float caustic(float2 uv, float time)
            {
                float2 p1 = uv + float2( time * 0.4,  time * 0.23);
                float2 p2 = uv + float2(-time * 0.31, time * 0.47);
                float c = abs(fbm(p1) - fbm(p2));
                return pow(1.0 - c, 6.0);
            }

            // ── Vertex ────────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS     = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS  = TransformWorldToHClip(posWS);
                OUT.uv           = IN.uv;
                OUT.positionWS   = posWS;
                OUT.screenPos    = ComputeScreenPos(OUT.positionHCS);
                OUT.fogFactor    = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            // ── Fragment ──────────────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                float  t   = _Time.y;
                float2 wXZ = IN.positionWS.xz;

                // ── 1. DEPTH-BASED COLOR BLEND ─────────────────────────────
                float2 screenUV      = IN.screenPos.xy / IN.screenPos.w;
                float  sceneRawZ     = SampleSceneDepth(screenUV);
                float  sceneLinear   = LinearEyeDepth(sceneRawZ, _ZBufferParams);
                float  surfaceLinear = IN.screenPos.w;
                
                float  depthDiff = max(0.0, sceneLinear - surfaceLinear);
                float  depthT    = saturate(depthDiff / _DepthMaxDist);
                float4 waterColor = lerp(_ShallowColor, _DeepColor, depthT * depthT);

                // ── 2. RIPPLE NORMAL ───────────────────────────────────────
                float rTime = t * _RippleSpeed;
                float2 ripUV1 = wXZ * _RippleScale + float2(rTime, rTime * 0.6);
                float2 ripUV2 = wXZ * _RippleScale + float2(-rTime * 0.7, rTime * 0.9);
                
                float2 ripple = (float2(fbm(ripUV1), fbm(ripUV2)) - 0.5) * _RippleStrength;

                // ── 3. CAUSTICS ────────────────────────────────────────────
                float caus = caustic(wXZ * _CausticScale, t * _CausticSpeed);
                float shallowMask = 1.0 - depthT;
                waterColor.rgb += caus * _CausticStrength * shallowMask;

                // ── 4. FOAM ────────────────────────────────────────────────
                float2 foamUV = wXZ * _FoamScale + float2(t * _FoamSpeed, t * _FoamSpeed * 0.73);
                float  foamNoise = fbm(foamUV + ripple * 2.0);
                float  foamEdge  = 1.0 - saturate(depthT / _FoamThreshold);
                float  foam      = foamEdge * step(_FoamThreshold * 0.6, foamNoise);

                // ── 5. DEPTH INTERSECTION FOAM ─────────────────────────────
                float intersect = 0.0;
                if (_UseDepthIntersect > 0.5)
                {
                    intersect = 1.0 - saturate(depthDiff / _IntersectThreshold);
                    intersect = intersect * intersect * sqrt(intersect); // cheaper approx of pow(x, 2.5)
                    
                    float edgeNoise = fbm(wXZ * _FoamScale * 1.5 + ripple * 3.0 + t * _FoamSpeed);
                    intersect *= lerp(0.6, 1.0, edgeNoise);
                }

                // Blend Foam
                float totalFoam = saturate(foam + intersect);
                waterColor.rgb  = lerp(waterColor.rgb, _FoamColor.rgb, totalFoam);
                // Boost alpha where there is foam, otherwise keep depth alpha
                waterColor.a    = saturate(waterColor.a + (totalFoam * _FoamColor.a));

                // ── 6. URP SPECULAR HIGHLIGHT ──────────────────────────────
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float3 lightCol = mainLight.color;
                
                float3 viewDir   = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 normal    = normalize(float3(ripple.x, 1.0, ripple.y));
                float3 halfVec   = normalize(lightDir + viewDir);
                
                float  NdotH     = max(0.0, dot(normal, halfVec));
                float  spec      = pow(NdotH, _SpecPower);
                
                // Add specular tinted by actual scene light color
                waterColor.rgb  += _SpecColor2.rgb * lightCol * spec * _SpecStrength;

                // ── 7. FOG ─────────────────────────────────────────────────
                waterColor.rgb = MixFog(waterColor.rgb, IN.fogFactor);

                return waterColor;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}