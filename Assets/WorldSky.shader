Shader "Custom/URP/WorldSky"
{
    Properties
    {
        [Header(Sky Gradient)]
        _SkyColorZenith     ("Sky Zenith Color",        Color) = (0.08, 0.30, 0.82, 1)
        _SkyColorMid        ("Sky Mid Color",           Color) = (0.22, 0.60, 0.96, 1)
        _SkyColorHorizon    ("Sky Horizon Color",       Color) = (0.58, 0.86, 1.00, 1)
        _SkyColorLow        ("Sky Low Color",           Color) = (0.80, 0.96, 1.00, 1)

        [Header(Sun Animation)]
        _SunSpeed           ("Sun Orbit Speed",         Range(0.0, 0.5))   = 0.018
        _SunOrbitTilt       ("Sun Arc Height",          Range(0.1, 0.9))   = 0.55
        _SunColor           ("Sun Color",               Color)  = (1.00, 0.98, 0.90, 1)
        _SunSize            ("Sun Size",                Range(0.001, 0.05)) = 0.014
        _SunGlowSize        ("Sun Glow Size",           Range(0.01,  0.50)) = 0.30
        _SunGlowIntensity   ("Sun Glow Intensity",      Range(0.0,   3.0))  = 1.2

        [Header(FBM Clouds)]
        _CloudColorLight    ("Cloud Light Color",       Color)  = (1.00, 1.00, 1.00, 1)
        _CloudColorMid      ("Cloud Mid Color",         Color)  = (0.85, 0.93, 1.00, 1)
        _CloudColorShadow   ("Cloud Shadow Color",      Color)  = (0.52, 0.68, 0.88, 1)
        _CloudSpeed         ("Cloud Drift Speed",       Range(0.0, 0.3))  = 0.018
        _CloudScale         ("Cloud Base Scale",        Range(0.5, 6.0))  = 2.2
        _CloudCoverage      ("Cloud Coverage",          Range(0.0, 1.0))  = 0.58
        _CloudSharpness     ("Cloud Edge Sharpness",    Range(2.0, 16.0)) = 9.0
        _CloudBrightness    ("Cloud Brightness",        Range(0.5, 3.0))  = 1.35
        _CloudMorphSpeed    ("Cloud Morph Speed",       Range(0.0, 1.0))  = 0.10
        _CloudWarpStrength  ("Cloud Warp Strength",     Range(0.0, 2.0))  = 0.85
        _CloudLayerOffset   ("Cloud Layer 2 Offset",    Vector) = (3.7, 0, 5.1, 0)

        [Header(Horizon Fog)]
        _FogColor           ("Fog Color",               Color)  = (0.78, 0.92, 1.00, 1)
        _FogStart           ("Fog Start",               Range(0.0, 0.3))  = 0.05
        _FogEnd             ("Fog End",                 Range(0.0, 0.6))  = 0.25
        _FogDensity         ("Fog Density",             Range(0.0, 1.0))  = 0.65

        [Header(Water)]
        _WaterDeepColor     ("Water Deep Color",        Color)  = (0.08, 0.28, 0.58, 1)
        _WaterShallowColor  ("Water Shallow Color",     Color)  = (0.25, 0.58, 0.82, 1)
        _ReflectBase        ("Reflection Base",         Range(0.0, 1.0))  = 0.55
        _ReflectGrazing     ("Reflection Grazing Boost",Range(0.0, 1.0))  = 0.90
        _WaterFresnel       ("Fresnel Sharpness",       Range(0.5, 5.0))  = 1.8
        _ReflectFade        ("Reflection Fade",         Range(0.0, 1.0))  = 0.30
        _ReflectBlurSize    ("Reflection Blur",         Range(0.0, 0.30)) = 0.10

        [Header(Water Surface)]
        _WaveSpeed          ("Wave Speed",              Range(0.0, 2.0))  = 0.40
        _WaveScale          ("Wave Scale",              Range(1.0, 30.0)) = 10.0
        _WaveHeight         ("Wave Normal Strength",    Range(0.0, 1.0))  = 0.45
        _RippleColor        ("Ripple Crest Color",      Color)  = (0.78, 0.94, 1.00, 1)
        _RippleIntensity    ("Ripple Visibility",       Range(0.0, 1.0))  = 0.28
        _SparkleIntensity   ("Sun Sparkle",             Range(0.0, 2.0))  = 1.0
        _WaterFogColor      ("Water Mist Color",        Color)  = (0.70, 0.88, 0.97, 1)
        _WaterFogDensity    ("Water Mist Density",      Range(0.0, 1.0))  = 0.30

        // ── Day/Night cycle override (driven by DayNightCycle.cs) ──────────
        [Header(Day Night Override)]
        // Set _UseSunOverride = 1 to replace the shader's built-in sun orbit
        // with the direction supplied by _SunDirOverride (world-space, toward sun).
        _UseSunOverride     ("Use External Sun Dir",    Range(0,1)) = 0
        _SunDirOverride     ("Sun Dir Override",        Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Background"
            "Queue"          = "Background"
            "PreviewType"    = "Skybox"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _SkyColorZenith;
                half4  _SkyColorMid;
                half4  _SkyColorHorizon;
                half4  _SkyColorLow;
                half   _SunSpeed;
                half   _SunOrbitTilt;
                half4  _SunColor;
                half   _SunSize;
                half   _SunGlowSize;
                half   _SunGlowIntensity;
                half4  _CloudColorLight;
                half4  _CloudColorMid;
                half4  _CloudColorShadow;
                half   _CloudSpeed;
                half   _CloudScale;
                half   _CloudCoverage;
                half   _CloudSharpness;
                half   _CloudBrightness;
                half   _CloudMorphSpeed;
                half   _CloudWarpStrength;
                float4 _CloudLayerOffset;
                half4  _FogColor;
                half   _FogStart;
                half   _FogEnd;
                half   _FogDensity;
                half4  _WaterDeepColor;
                half4  _WaterShallowColor;
                half   _ReflectBase;
                half   _ReflectGrazing;
                half   _WaterFresnel;
                half   _ReflectFade;
                half   _ReflectBlurSize;
                half   _WaveSpeed;
                half   _WaveScale;
                half   _WaveHeight;
                half4  _RippleColor;
                half   _RippleIntensity;
                half   _SparkleIntensity;
                half4  _WaterFogColor;
                half   _WaterFogDensity;
                // Day/Night override
                float  _UseSunOverride;
                float4 _SunDirOverride;     // xyz = world-space direction toward sun
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 rayDir      : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---------------------------------------------------------------
            // Noise
            // ---------------------------------------------------------------
            float hash(float2 p)
            {
                p  = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i),               hash(i + float2(1,0)), u.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), u.x),
                    u.y);
            }

            float fbmMorph(float2 p, float time)
            {
                float v = 0.0, amp = 0.5;
                float2 shift = float2(time * _CloudMorphSpeed, time * _CloudMorphSpeed * 0.7);
                UNITY_UNROLL
                for (int i = 0; i < 7; i++)
                {
                    v   += amp * valueNoise(p + shift * (float(i) * 0.3 + 0.5));
                    p   *= 2.07;
                    amp *= 0.49;
                    shift *= 1.3;
                }
                return v;
            }

            float cloudNoise(float2 p, float time)
            {
                float2 warp = float2(
                    fbmMorph(p + float2(1.7, 9.2), time * 0.4),
                    fbmMorph(p + float2(8.3, 2.8), time * 0.3)
                );
                return fbmMorph(p + _CloudWarpStrength * warp, time);
            }

            // ---------------------------------------------------------------
            // Sun direction
            // Normally driven by the shader's internal orbit (_SunSpeed).
            // When _UseSunOverride == 1, uses the direction fed from C# instead.
            // ---------------------------------------------------------------
            float3 GetSunDir(float time)
            {
                if (_UseSunOverride > 0.5)
                    return normalize(_SunDirOverride.xyz);

                float a = time * _SunSpeed;
                return normalize(float3(cos(a), _SunOrbitTilt + sin(a) * 0.25, sin(a)));
            }

            // ---------------------------------------------------------------
            // Sky gradient
            // ---------------------------------------------------------------
            half3 SkyGradient(float t)
            {
                if (t < 0.12)
                    return lerp(_SkyColorLow.rgb,     _SkyColorHorizon.rgb, t / 0.12);
                if (t < 0.40)
                    return lerp(_SkyColorHorizon.rgb, _SkyColorMid.rgb,     (t - 0.12) / 0.28);
                return     lerp(_SkyColorMid.rgb,     _SkyColorZenith.rgb,  (t - 0.40) / 0.60);
            }

            // ---------------------------------------------------------------
            // Sun
            // ---------------------------------------------------------------
            half3 SunContrib(float3 dir, float3 sunDir)
            {
                float d      = dot(normalize(dir), sunDir);
                float disc   = smoothstep(_SunSize + 0.001, _SunSize - 0.001, 1.0 - d);
                float haze   = pow(saturate(d), 10.0) * _SunGlowIntensity * 0.35;
                float scatter= pow(saturate((d - (1.0 - _SunGlowSize)) / max(_SunGlowSize,0.001)), 1.4) * 0.28;
                return _SunColor.rgb * (disc * 2.0 + haze + scatter);
            }

            // ---------------------------------------------------------------
            // Cloud layer
            // ---------------------------------------------------------------
            half4 CloudLayer(float2 uv, float elevation, float time)
            {
                float2 uv1 = uv * _CloudScale + float2(time * _CloudSpeed, time * _CloudSpeed * 0.25);
                float  n1  = cloudNoise(uv1, time);
                float2 uv2 = (uv + _CloudLayerOffset.xz) * _CloudScale * 1.7
                           + float2(time * _CloudSpeed * 1.4, -time * _CloudSpeed * 0.6);
                float  n2  = cloudNoise(uv2, time * 0.8);

                float threshold = 1.0 - _CloudCoverage;
                float c1    = saturate((n1 - threshold) * _CloudSharpness);
                float c2    = saturate((n2 - (threshold + 0.08)) * _CloudSharpness * 0.7);
                float cloud = saturate(c1 + c2 * (1.0 - c1));

                float shadowT    = saturate((n1 - threshold - 0.10) * _CloudSharpness * 0.9);
                float highlightT = saturate((n1 - threshold - 0.24) * _CloudSharpness * 1.3);
                half3 col        = lerp(_CloudColorShadow.rgb, _CloudColorMid.rgb,   shadowT);
                col              = lerp(col,                   _CloudColorLight.rgb,  highlightT);
                col             *= _CloudBrightness;

                return half4(col, cloud * smoothstep(0.0, 0.18, elevation));
            }

            // ---------------------------------------------------------------
            // Full sky evaluation
            // ---------------------------------------------------------------
            half3 EvaluateSky(float3 dir, float3 sunDir, float time)
            {
                dir = normalize(dir);
                float elev = dir.y;
                float t    = saturate(elev * 0.5 + 0.5);
                half3 col  = SkyGradient(t);

                if (elev > -0.02)
                {
                    float2 cloudUV = dir.xz / max(elev, 0.04);
                    half4  clouds  = CloudLayer(cloudUV, elev, time);
                    col = lerp(col, clouds.rgb, clouds.a);
                }

                col += SunContrib(dir, sunDir);

                float fogT = 1.0 - smoothstep(_FogStart, _FogEnd, elev);
                col = lerp(col, _FogColor.rgb, fogT * _FogDensity);

                return col;
            }

            // ---------------------------------------------------------------
            // Blurred reflection
            // ---------------------------------------------------------------
            half3 BlurredReflection(float3 reflDir, float3 sunDir, float time, float blur)
            {
                float3 up    = abs(reflDir.y) < 0.98 ? float3(0,1,0) : float3(1,0,0);
                float3 right = normalize(cross(up, reflDir));
                float3 fwd   = normalize(cross(reflDir, right));

                float3 d0 = reflDir;
                float3 d1 = normalize(reflDir + ( right              ) * blur);
                float3 d2 = normalize(reflDir + ( right * 0.31 + fwd * 0.95) * blur);
                float3 d3 = normalize(reflDir + (-right * 0.81 + fwd * 0.59) * blur);
                float3 d4 = normalize(reflDir + (-right * 0.81 - fwd * 0.59) * blur);
                float3 d5 = normalize(reflDir + ( right * 0.31 - fwd * 0.95) * blur);

                d0.y = max(d0.y, 0.001); d1.y = max(d1.y, 0.001);
                d2.y = max(d2.y, 0.001); d3.y = max(d3.y, 0.001);
                d4.y = max(d4.y, 0.001); d5.y = max(d5.y, 0.001);

                half3 col = EvaluateSky(d0, sunDir, time)
                          + EvaluateSky(d1, sunDir, time)
                          + EvaluateSky(d2, sunDir, time)
                          + EvaluateSky(d3, sunDir, time)
                          + EvaluateSky(d4, sunDir, time)
                          + EvaluateSky(d5, sunDir, time);
                return col / 6.0;
            }

            // ---------------------------------------------------------------
            // Wave normal
            // ---------------------------------------------------------------
            float3 WaveNormal(float2 xz, float time)
            {
                float2 uv1 = xz * _WaveScale       + float2( time * _WaveSpeed,        time * _WaveSpeed * 0.6);
                float2 uv2 = xz * _WaveScale * 1.4 + float2(-time * _WaveSpeed * 0.7,  time * _WaveSpeed * 1.1);
                float2 uv3 = xz * _WaveScale * 0.6 + float2( time * _WaveSpeed * 0.4, -time * _WaveSpeed * 0.8);
                float  eps = 0.05;
                float  hC  = valueNoise(uv1)                + valueNoise(uv2)                + valueNoise(uv3);
                float  hR  = valueNoise(uv1+float2(eps, 0)) + valueNoise(uv2+float2(eps, 0)) + valueNoise(uv3+float2(eps, 0));
                float  hU  = valueNoise(uv1+float2(0, eps)) + valueNoise(uv2+float2(0, eps)) + valueNoise(uv3+float2(0, eps));
                return normalize(float3((hC-hR)*_WaveHeight, 1.0, (hC-hU)*_WaveHeight));
            }

            float RippleMask(float2 xz, float time)
            {
                float t  = time * _WaveSpeed;
                float r1 = (sin(xz.x * _WaveScale        + t      ) + 1.0) * 0.5;
                float r2 = (sin(xz.y * _WaveScale * 0.85 + t * 0.8) + 1.0) * 0.5;
                float r3 = (sin((xz.x + xz.y) * _WaveScale * 0.6 - t * 0.6) + 1.0) * 0.5;
                return pow(r1 * r2 * r3, 3.5);
            }

            // ---------------------------------------------------------------
            // Vertex
            // ---------------------------------------------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.rayDir = TransformObjectToWorld(IN.positionOS.xyz) - GetCameraPositionWS();
                return OUT;
            }

            // ---------------------------------------------------------------
            // Fragment
            // ---------------------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                float  time   = _Time.y;
                float3 dir    = normalize(IN.rayDir);
                float3 sunDir = GetSunDir(time);

                float skyMask = smoothstep(-0.01, 0.025, dir.y);

                // ===== SKY =====
                half3 skyCol = EvaluateSky(dir, sunDir, time);

                // ===== WATER =====
                float  depth  = abs(dir.y);
                float2 surfXZ = dir.xz / max(depth, 0.01);

                float3 waveN = WaveNormal(surfXZ * 0.1, time);

                float3 pertN   = normalize(float3(waveN.x * _WaveHeight * 0.5, 1.0, waveN.z * _WaveHeight * 0.5));
                float3 reflDir = normalize(float3(dir.x + pertN.x * 0.06, abs(dir.y), dir.z + pertN.z * 0.06));

                float blurRadius = _ReflectBlurSize * (1.0 + (1.0 - saturate(depth * 2.0)) * 1.2);
                half3 blurRefl   = BlurredReflection(reflDir, sunDir, time, blurRadius);

                float reflLum  = dot(blurRefl, half3(0.299, 0.587, 0.114));
                half3 fadedRefl= lerp(blurRefl, reflLum * _WaterShallowColor.rgb * 1.15, _ReflectFade);

                float fresnel = pow(1.0 - saturate(depth), _WaterFresnel);

                float reflAmount = lerp(_ReflectBase, _ReflectGrazing, fresnel);

                half3 waterBase = lerp(_WaterDeepColor.rgb, _WaterShallowColor.rgb, fresnel * 0.75);
                half3 waterCol  = lerp(waterBase, fadedRefl, reflAmount);

                float ripFade = smoothstep(0.0, 0.06, depth) * (1.0 - smoothstep(0.40, 0.60, depth));
                float ripMask = RippleMask(surfXZ * 0.08, time);
                waterCol = lerp(waterCol, _RippleColor.rgb, ripMask * _RippleIntensity * ripFade);

                float3 sunRefl = reflect(-sunDir, waveN);
                float  sparkle = pow(saturate(dot(normalize(-dir), normalize(sunRefl))), 140.0);
                waterCol += _SunColor.rgb * sparkle * _SparkleIntensity * fresnel;

                float mistT = (1.0 - smoothstep(0.0, 0.35, depth)) * _WaterFogDensity;
                waterCol = lerp(waterCol, _WaterFogColor.rgb, mistT);

                waterCol = lerp(waterCol, waterCol * 0.5, saturate((depth - 0.25) * 2.0));

                // ===== COMBINE =====
                half3 final = lerp(waterCol, skyCol, skyMask);

                float horizFogT = 1.0 - smoothstep(0.0, _FogEnd * 0.5, abs(dir.y));
                final = lerp(final, _FogColor.rgb, horizFogT * _FogDensity * 0.5);

                return half4(final, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
