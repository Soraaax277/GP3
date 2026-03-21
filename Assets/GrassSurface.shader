Shader "Custom/URP/GrassSurface"
{
    Properties
    {
        [Header(Grass Colors)]
        _ColorA         ("Grass Color A",       Color)           = (0.28, 0.58, 0.18, 1)
        _ColorB         ("Grass Color B",       Color)           = (0.38, 0.72, 0.22, 1)
        _ColorC         ("Grass Color C",       Color)           = (0.20, 0.45, 0.14, 1)
        _DirtColor      ("Dirt / Gap Color",    Color)           = (0.28, 0.22, 0.12, 1)

        [Header(Texture Detail)]
        _NoiseScale     ("Noise Scale",         Range(0.5, 8.0)) = 2.8
        _BladeScale     ("Blade Detail Scale",  Range(1.0, 20.0))= 8.0
        _Coverage       ("Grass Coverage",      Range(0.0, 1.0)) = 0.72
        _Sharpness      ("Edge Sharpness",      Range(1.0, 16.0))= 6.0

        [Header(Wind)]
        _WindStrength   ("Wind Strength",       Range(0.0, 0.3)) = 0.04
        _WindSpeed      ("Wind Speed",          Range(0.0, 4.0)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry+1"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _ColorC;
                float4 _DirtColor;
                float  _NoiseScale;
                float  _BladeScale;
                float  _Coverage;
                float  _Sharpness;
                float  _WindStrength;
                float  _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;  // world-space XZ baked by GrassRenderer
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 worldXZ     : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ------------------------------------------------------------------
            // Noise functions
            // ------------------------------------------------------------------
            float hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i),               hash(i + float2(1,0)), u.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), u.x),
                    u.y);
            }

            // 3-octave FBM for large colour variation patches
            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                float2x2 rot = float2x2(1.6, 1.2, -1.2, 1.6);
                v += a * vnoise(p); p = mul(rot, p); a *= 0.5;
                v += a * vnoise(p); p = mul(rot, p); a *= 0.5;
                v += a * vnoise(p);
                return v;
            }

            // ------------------------------------------------------------------
            // Vertex
            // ------------------------------------------------------------------
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Subtle wind offset on XZ using world position
                float2 wXZ   = IN.uv;
                float  t     = _Time.y * _WindSpeed;
                float  wind  = sin(wXZ.x * 0.4 + t) * 0.5 + sin(wXZ.y * 0.3 + t * 0.7) * 0.5;
                float4 pos   = IN.positionOS;
                pos.x       += wind * _WindStrength;
                pos.z       += wind * _WindStrength * 0.6;

                OUT.positionHCS = TransformObjectToHClip(pos.xyz);
                OUT.worldXZ     = wXZ;
                OUT.normalWS    = float3(0, 1, 0);
                return OUT;
            }

            // ------------------------------------------------------------------
            // Fragment — full procedural grass texture
            // ------------------------------------------------------------------
            float4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.worldXZ;

                // --- Large patch variation (FBM) ---
                float largeNoise = fbm(p * _NoiseScale * 0.25);

                // --- Medium clump layer ---
                float2 warp      = float2(fbm(p * _NoiseScale * 0.4 + float2(3.1, 1.7)),
                                          fbm(p * _NoiseScale * 0.4 + float2(8.3, 5.2)));
                float clumpNoise = fbm(p * _NoiseScale + warp * 0.6);

                // --- Fine blade detail layer ---
                // Uses two overlapping value noises at high frequency
                // to create fine grass-like filament texture
                float2 bladeUV  = p * _BladeScale;
                float  blade1   = vnoise(bladeUV + float2(0.0, 0.0));
                float  blade2   = vnoise(bladeUV * 1.3 + float2(2.7, 5.1));
                float  blade3   = vnoise(bladeUV * 0.7 + float2(7.3, 2.9));
                float  blades   = blade1 * 0.5 + blade2 * 0.3 + blade3 * 0.2;

                // Sharpen blade edges so individual blades read as thin strands
                float bladeMask = saturate((blades - (1.0 - _Coverage)) * _Sharpness);

                // --- Combine into grass mask ---
                float grassMask = bladeMask * saturate(clumpNoise * 1.4);

                // --- Color selection ---
                // Pick between 3 grass colors based on large patch noise
                float3 grassCol;
                if (largeNoise < 0.35)
                    grassCol = lerp(_ColorC.rgb, _ColorA.rgb, largeNoise / 0.35);
                else if (largeNoise < 0.65)
                    grassCol = lerp(_ColorA.rgb, _ColorB.rgb, (largeNoise - 0.35) / 0.30);
                else
                    grassCol = lerp(_ColorB.rgb, _ColorC.rgb, (largeNoise - 0.65) / 0.35);

                // Per-blade tint: slightly lighter at fine blade peaks
                grassCol = lerp(grassCol * 0.75, grassCol * 1.1, blades);

                // Blend grass over dirt in gaps
                float3 col = lerp(_DirtColor.rgb, grassCol, grassMask);

                // Simple diffuse lighting
                Light  mainLight = GetMainLight();
                float  ndotl     = saturate(dot(float3(0,1,0), mainLight.direction)) * 0.5 + 0.5;
                col *= mainLight.color.rgb * ndotl;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
