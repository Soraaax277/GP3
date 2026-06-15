Shader "Custom/URP/WorldWater"
{
    Properties
    {
        [Header(Water Colors)]
        _ShallowColor    ("Shallow Color",         Color)            = (0.10, 0.35, 0.65, 1.0)
        _DeepColor       ("Deep Color",            Color)            = (0.03, 0.10, 0.30, 1.0)
        _HorizonColor    ("Horizon Fade Color",    Color)            = (0.55, 0.78, 0.95, 0.0)

        [Header(Shoreline)]
        _ShoreColor      ("Shore Color",           Color)            = (0.28, 0.72, 0.88, 1.0)
        _ShoreIntensity  ("Shore Intensity",       Range(0.0, 1.0))  = 0.75

        [Header(Horizon Fade)]
        _FadeStart       ("Fade Start",            Range(0.3, 0.9))  = 0.55
        _FadeEnd         ("Fade End",              Range(0.3, 1.0))  = 0.88

        [Header(Ripples)]
        _RippleSpeed     ("Ripple Speed",          Range(0.0, 2.0))  = 0.5
        _RippleScale     ("Ripple Scale",          Range(0.1, 2.0))  = 0.5
        _RippleStrength  ("Ripple Strength",       Range(0.0, 6.0))  = 3.0
        _NoiseStrength   ("Noise Randomness",      Range(0.0, 1.0))  = 0.6

        [Header(Sky Reflection)]
        _SkyZenith       ("Sky Zenith Color",      Color)            = (0.08, 0.30, 0.82, 1)
        _SkyMid          ("Sky Mid Color",         Color)            = (0.22, 0.60, 0.96, 1)
        _SkyHorizon      ("Sky Horizon Color",     Color)            = (0.58, 0.86, 1.00, 1)
        _ReflectAmount   ("Reflect Amount",        Range(0.0, 1.0))  = 0.70
        _ReflectDistort  ("Reflection Distortion", Range(0.0, 2.0))  = 0.80
        _ReflectBlur     ("Reflection Blur",       Range(0.0, 1.0))  = 0.55

        [Header(Sun Sparkle)]
        _SparkleColor    ("Sparkle Color",         Color)            = (1.00, 0.97, 0.88, 1)
        _SparkleIntensity("Sparkle Intensity",     Range(0.0, 3.0))  = 1.6
        _SparklePow      ("Sparkle Tightness",     Range(8.0, 200.0))= 55.0

        [Header(Boat Wake)]
        _WakeStrength    ("Wake Strength",         Range(0.0, 3.0))  = 1.2
        _WakeFrequency   ("Wake Frequency",        Range(0.5, 8.0))  = 3.0
        _WakeSpeed       ("Wake Speed",            Range(0.0, 6.0))  = 2.5
        _WakeFalloff     ("Wake Falloff",          Range(0.5, 8.0))  = 2.5
        _WakeWidth       ("Wake Cone Width",       Range(0.0, 1.0))  = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent-10"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _HorizonColor;
                float4 _ShoreColor;
                float  _ShoreIntensity;
                float  _FadeStart;
                float  _FadeEnd;
                float  _RippleSpeed;
                float  _RippleScale;
                float  _RippleStrength;
                float  _NoiseStrength;
                float4 _SkyZenith;
                float4 _SkyMid;
                float4 _SkyHorizon;
                float  _ReflectAmount;
                float  _ReflectDistort;
                float  _ReflectBlur;
                float4 _SparkleColor;
                float  _SparkleIntensity;
                float  _SparklePow;
                float  _WakeStrength;
                float  _WakeFrequency;
                float  _WakeSpeed;
                float  _WakeFalloff;
                float  _WakeWidth;
            CBUFFER_END

            // Boat wake data — declared at global scope (NOT inside CBUFFER)
            // so BoatManager can push them via Material.SetVectorArray /
            // Material.SetInt each frame without being blocked by the SRP Batcher.
            //   _BoatPositions[i].xyz = world-space boat position
            //   _BoatForwards[i].xyz  = world-space normalised boat forward direction
            #define MAX_BOATS 16
            float4 _BoatPositions[MAX_BOATS];
            float4 _BoatForwards[MAX_BOATS];
            int    _BoatCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;       // R = shore proximity, A = mask feather
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float  radial      : TEXCOORD1;
                float3 worldPos    : TEXCOORD2;
                float  shore       : TEXCOORD3;  // vertex color R = shore proximity
                float  maskAlpha   : TEXCOORD4;  // vertex color A = island feather mask
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ----------------------------------------------------------
            // Noise
            // ----------------------------------------------------------
            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i,               float2(127.1,311.7))) * 43758.5);
                float b = frac(sin(dot(i + float2(1,0), float2(127.1,311.7))) * 43758.5);
                float c = frac(sin(dot(i + float2(0,1), float2(127.1,311.7))) * 43758.5);
                float d = frac(sin(dot(i + float2(1,1), float2(127.1,311.7))) * 43758.5);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, amp = 0.5;
                float2x2 rot = float2x2(1.6, 1.2, -1.2, 1.6);
                v += amp * vnoise(p); p = mul(rot, p); amp *= 0.5;
                v += amp * vnoise(p); p = mul(rot, p); amp *= 0.5;
                v += amp * vnoise(p);
                return v;
            }

            // ----------------------------------------------------------
            // Wave height — same good version, scale remapped so 0.5 = natural size
            // ----------------------------------------------------------
            float WaveHeight(float2 p, float t)
            {
                float s   = _RippleScale / 3.57;   // 0.5/3.57 ≈ 0.14 (the good original value)
                float2 u1 = p * s        + float2( t * 0.30,  t * 0.18);
                float2 u2 = p * s * 0.65 + float2(-t * 0.22,  t * 0.27);
                float2 u3 = p * s * 1.40 + float2( t * 0.15, -t * 0.35);
                float2 u4 = p * s * 0.40 + float2(-t * 0.08,  t * 0.12);

                float sineH =   sin(u1.x*6.2832)*cos(u1.y*6.2832)
                              + sin(u2.x*6.2832+1.30)*cos(u2.y*6.2832+0.70)
                              + sin(u3.x*6.2832+2.10)*cos(u3.y*6.2832+1.90)*0.50
                              + sin(u4.x*6.2832+3.50)*cos(u4.y*6.2832+2.40)*0.30;

                float2 noiseUV = p * s * 0.8 + float2(t * 0.07, t * 0.05);
                float2 warpUV  = p * s * 0.5 + float2(t * 0.04, t * 0.09);
                float2 warp    = float2(fbm(warpUV), fbm(warpUV + float2(5.2, 1.3)));
                float  noiseH  = fbm(noiseUV + warp * 0.8) * 2.0 - 1.0;

                return lerp(sineH, noiseH, _NoiseStrength);
            }

            float3 WaveNormal(float2 xz, float t)
            {
                float  e  = 0.22;
                float  hC = WaveHeight(xz,               t);
                float  hR = WaveHeight(xz + float2(e,0), t);
                float  hU = WaveHeight(xz + float2(0,e), t);
                return normalize(float3(
                    (hC - hR) * _RippleStrength,
                    1.0,
                    (hC - hU) * _RippleStrength));
            }

            float3 SampleSky(float3 dir, float3 waveN)
            {
                float3 d = dir;
                d.x += waveN.x * _ReflectDistort;
                d.z += waveN.z * _ReflectDistort;
                d = normalize(lerp(d, float3(0,1,0), _ReflectBlur * 0.6));
                float e = saturate(d.y);
                float3 sky;
                if (e < 0.35)
                    sky = lerp(_SkyHorizon.rgb, _SkyMid.rgb, e / 0.35);
                else
                    sky = lerp(_SkyMid.rgb, _SkyZenith.rgb, (e - 0.35) / 0.65);
                return sky;
            }

            // ----------------------------------------------------------
            // Boat Wake
            // Computes a V-shaped ripple normal contribution from all
            // active boats. Each boat generates rings that radiate outward
            // behind it in a cone, fading with distance.
            // ----------------------------------------------------------
            float3 WakeNormal(float2 xz)
            {
                float3 wakeN = float3(0, 0, 0);
                float  t     = _Time.y;

                for (int b = 0; b < _BoatCount && b < MAX_BOATS; b++)
                {
                    float2 boatXZ   = _BoatPositions[b].xz;
                    float2 fwdXZ    = _BoatForwards[b].xz;   // already normalised from C#

                    float2 toFrag   = xz - boatXZ;
                    float  dist     = length(toFrag);

                    // Skip fragments too far away — avoids divide-by-zero and
                    // keeps the loop cheap for distant water.
                    if (dist < 0.001 || dist > 30.0) continue;

                    float2 toFragN  = toFrag / dist;

                    // V-cone mask — dot with NEGATIVE forward (wake is behind the boat).
                    // _WakeWidth = 0 → narrow V, 1 → full circle.
                    float alignment = dot(toFragN, -fwdXZ);
                    float coneMask  = smoothstep(_WakeWidth - 0.35, _WakeWidth + 0.35, alignment);

                    // Radial sine ring that travels outward over time.
                    float ring = sin(dist * _WakeFrequency - t * _WakeSpeed);

                    // Distance falloff — ripple fades naturally as it spreads.
                    float falloff = exp(-dist / _WakeFalloff);

                    // Convert ring amplitude to a surface gradient (XZ slope).
                    // We nudge the sample point and finite-difference the ring.
                    float  e      = 0.15;
                    float2 dxDir  = float2(e, 0);
                    float2 dzDir  = float2(0, e);

                    float ringDX = sin(length(toFrag - dxDir) * _WakeFrequency - t * _WakeSpeed)
                                   * exp(-length(toFrag - dxDir) / _WakeFalloff);
                    float ringDZ = sin(length(toFrag - dzDir) * _WakeFrequency - t * _WakeSpeed)
                                   * exp(-length(toFrag - dzDir) / _WakeFalloff);

                    float slopeX = (ring - ringDX) / e;
                    float slopeZ = (ring - ringDZ) / e;

                    float strength = _WakeStrength * falloff * coneMask;
                    wakeN += float3(slopeX, 0, slopeZ) * strength;
                }

                return wakeN;
            }

            // ----------------------------------------------------------
            // Vertex
            // ----------------------------------------------------------
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.radial      = saturate(length(IN.uv * 2.0 - 1.0));
                OUT.shore       = IN.color.r;
                OUT.maskAlpha   = IN.color.a;
                return OUT;
            }

            // ----------------------------------------------------------
            // Fragment
            // ----------------------------------------------------------
            float4 frag(Varyings IN) : SV_Target
            {
                float  t       = _Time.y;
                float3 normal  = WaveNormal(IN.worldPos.xz, t);

                // Blend boat wake ripples on top of the base wave normal.
                // WakeNormal returns a world-space XZ gradient; we add it
                // directly and renormalise so it perturbs the existing waves
                // rather than replacing them.
                float3 wake = WakeNormal(IN.worldPos.xz);
                normal = normalize(normal + float3(wake.x, 0, wake.z));

                float3 viewDir = normalize(GetCameraPositionWS() - IN.worldPos);

                // Base water color (deep → shallow by normal angle)
                float  nDotUp  = saturate(dot(normal, float3(0,1,0)));
                float3 col     = lerp(_DeepColor.rgb, _ShallowColor.rgb, nDotUp * 0.6 + 0.15);

                // Shore color — blended in by the vertex-baked proximity value
                // Smooth the shore mask so it fades nicely rather than hard edge
                float shoreMask = smoothstep(0.0, 1.0, IN.shore);
                col = lerp(col, _ShoreColor.rgb, shoreMask * _ShoreIntensity);

                // Sky reflection
                float3 reflDir = reflect(-viewDir, normal);
                reflDir.y      = max(reflDir.y, 0.001);
                float3 skyCol  = SampleSky(reflDir, normal);
                float  fresnel = pow(1.0 - saturate(dot(viewDir, normal)), 2.0);
                col = lerp(col, skyCol, lerp(_ReflectAmount * 0.25, _ReflectAmount, fresnel));

                // Crest/trough contrast
                float crest = saturate(nDotUp * 1.5 - 0.2);
                col = lerp(col * 0.75, col * 1.15, crest);

                // Sun sparkle
                float3 sunDir  = normalize(float3(0.6, 0.6, 0.4));
                float3 sunRefl = reflect(-sunDir, normal);
                float  sparkle = pow(saturate(dot(sunRefl, viewDir)), _SparklePow);
                col += _SparkleColor.rgb * sparkle * _SparkleIntensity;

                // Horizon fade
                float fadeT = smoothstep(_FadeStart, _FadeEnd, IN.radial);
                col   = lerp(col, _HorizonColor.rgb, fadeT * 0.75);
                float alpha = (1.0 - fadeT) * _ShallowColor.a * IN.maskAlpha;

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
