Shader "Custom/UICyberFog"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}

        _ColorA ("Color A (Deep)", Color) = (0.0, 0.02, 0.15, 1.0)
        _ColorB ("Color B (Mid)", Color) = (0.0, 0.4, 0.8, 1.0)
        _ColorC ("Color C (Highlight)", Color) = (0.0, 1.0, 0.9, 1.0)
        _BaseOpacity ("Base Opacity", Range(0, 1)) = 0.5

        _FogOpacity ("Fog Opacity", Range(0, 1)) = 0.85
        _Density ("Fog Density", Range(0, 1)) = 0.7
        _Scale ("Noise Scale", Range(1, 20)) = 5.0
        _Speed ("Drift Speed", Range(0, 2)) = 0.3

        // Hex grid
        _HexColor ("Hex Grid Color", Color) = (0.0, 0.8, 1.0, 1.0)
        _HexActiveColor ("Hex Active Color", Color) = (0.0, 1.0, 0.9, 1.0)
        _HexOpacity ("Hex Opacity", Range(0, 1)) = 0.35
        _HexScale ("Hex Scale", Range(1, 40)) = 12.0
        _HexPulseSpeed ("Hex Pulse Speed", Range(0, 4)) = 1.5
        _HexPistonDepth ("Hex Piston Depth", Range(0, 0.45)) = 0.2
        _HexSparsity ("Hex Sparsity", Range(0, 1)) = 0.3
        _HexSmoothness ("Hex Smoothness", Range(0.001, 0.05)) = 0.01
        _HexLineWidth ("Hex Line Width", Range(0.001, 0.1)) = 0.03

        // Data stream lines
        _StreamColor ("Data Stream Color", Color) = (0.0, 1.0, 0.8, 1.0)
        _StreamOpacity ("Stream Opacity", Range(0, 1)) = 0.6
        _StreamCount ("Stream Count", Range(1, 20)) = 8.0
        _StreamSpeed ("Stream Speed", Range(0, 4)) = 1.5
        _StreamThickness ("Stream Thickness", Range(0.001, 0.02)) = 0.003

        // Glitch bars
        _GlitchColor ("Glitch Color", Color) = (0.0, 0.9, 1.0, 1.0)
        _GlitchOpacity ("Glitch Opacity", Range(0, 1)) = 0.4
        _GlitchSpeed ("Glitch Speed", Range(0, 4)) = 1.2
        _GlitchFrequency ("Glitch Frequency", Range(0, 1)) = 0.3

        // Scan pulse
        _ScanColor ("Scan Pulse Color", Color) = (0.0, 0.6, 1.0, 1.0)
        _ScanOpacity ("Scan Pulse Opacity", Range(0, 1)) = 0.25
        _ScanSpeed ("Scan Pulse Speed", Range(0, 2)) = 0.6

        // Circuit traces
        _CircuitColor ("Circuit Color", Color) = (0.0, 1.0, 0.7, 1.0)
        _CircuitOpacity ("Circuit Opacity", Range(0, 1)) = 0.45
        _CircuitScale ("Circuit Scale", Range(1, 20)) = 6.0
        _CircuitSpeed ("Circuit Speed", Range(0, 2)) = 0.4

        _FadeLeft   ("Fade Left",   Range(0, 1)) = 0.05
        _FadeRight  ("Fade Right",  Range(0, 1)) = 0.05
        _FadeTop    ("Fade Top",    Range(0, 1)) = 0.05
        _FadeBottom ("Fade Bottom", Range(0, 1)) = 0.05

        _ManualTime ("Manual Time", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half4 _ColorC;
                half _BaseOpacity;
                half _FogOpacity;
                half _Density;
                float _Scale;
                float _Speed;
                half4 _HexColor;
                half4 _HexActiveColor;
                half _HexOpacity;
                float _HexScale;
                float _HexPulseSpeed;
                float _HexPistonDepth;
                float _HexSparsity;
                float _HexSmoothness;
                float _HexLineWidth;
                half4 _StreamColor;
                half _StreamOpacity;
                float _StreamCount;
                float _StreamSpeed;
                float _StreamThickness;
                half4 _GlitchColor;
                half _GlitchOpacity;
                float _GlitchSpeed;
                float _GlitchFrequency;
                half4 _ScanColor;
                half _ScanOpacity;
                float _ScanSpeed;
                half4 _CircuitColor;
                half _CircuitOpacity;
                float _CircuitScale;
                float _CircuitSpeed;
                float _FadeLeft;
                float _FadeRight;
                float _FadeTop;
                float _FadeBottom;
                float _ManualTime;
            CBUFFER_END

            // -------------------------------------------------------
            // Helpers
            // -------------------------------------------------------

            float hash(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float hash1(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * valueNoise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.1;
                }
                return value;
            }

            float gaussian(float dist, float sigma)
            {
                return exp(-(dist * dist) / (2.0 * sigma * sigma));
            }

            half3 cyberGradient(float t)
            {
                half3 colAB = lerp(_ColorA.rgb, _ColorB.rgb, saturate(t * 2.0));
                half3 colBC = lerp(_ColorB.rgb, _ColorC.rgb, saturate(t * 2.0 - 1.0));
                return lerp(colAB, colBC, step(0.5, t));
            }

            // -------------------------------------------------------
            // Hex Grid with Piston Pulse
            //
            // Each hex cell has a unique seed that drives:
            //   - Whether it is visible (sparsity)
            //   - Whether it renders as a filled body or border only
            //   - Its piston phase (sin wave offset per cell)
            //
            // The piston shrinks the filled hex radius in and out
            // using the same approach as ProceduralHexGrid:
            //   currentRadius = 0.49 - ((1 - pistonWave) * depth)
            //
            // Border-only hexes animate their draw progress around
            // the hex edge using the angle metric.
            // -------------------------------------------------------

            // Returns cell-local UV and a unique cell ID
            float4 getHexData(float2 uv)
            {
                float2 r = float2(1.0, 1.73);
                float2 h = r * 0.5;
                float2 a = uv / r;
                float2 b = (uv - h) / r;
                float2 id_a = floor(a);
                float2 id_b = floor(b);
                float2 center_a = (id_a + 0.5) * r;
                float2 center_b = (id_b + 0.5) * r + h;
                float2 gv_a = uv - center_a;
                float2 gv_b = uv - center_b;
                bool useA = dot(gv_a, gv_a) < dot(gv_b, gv_b);
                float2 gv = useA ? gv_a : gv_b;
                float2 id = useA ? id_a : id_b;
                return float4(gv.x, gv.y, 0, id.x + id.y * 100.0);
            }

            // Distance from center of hex + angle around it
            float2 getHexMetrics(float2 gv)
            {
                float x = abs(gv.x);
                float y = abs(gv.y);
                float dist = max(x, x * 0.5 + y * 0.866025);
                float angle = (atan2(gv.x, gv.y) / 6.28318) + 0.5;
                return float2(dist, angle);
            }

            float3 hexGrid(float2 uv, float time)
            {
                float2 hUV = uv * _HexScale;
                float4 hexData = getHexData(hUV);
                float2 gv = hexData.xy;
                float id = hexData.w;
                float2 metrics = getHexMetrics(gv);
                float dist  = metrics.x;
                float angle = metrics.y;

                // Per-cell random seed
                float seed = hash(float2(id, id * 0.371 + 0.1));

                // Sparsity — skip some cells entirely
                float visible = step(_HexSparsity, seed);
                if (visible < 0.5) return 0.0;

                // Piston wave — each cell oscillates at its own phase
                // Directly ported from ProceduralHexGrid piston logic
                float pistonWave = sin(time * _HexPulseSpeed + seed * 15.0) * 0.5 + 0.5;

                // Lifecycle fade in/out so hexes don't pop
                float lifeCycle = frac(time * 0.3 * seed + seed * 100.0);
                float alpha = smoothstep(0.0, 0.15, lifeCycle) * smoothstep(1.0, 0.85, lifeCycle);

                // Decide filled body vs border outline per cell
                float isFilled = step(0.55, hash(float2(id, seed * 2.0)));

                float result = 0.0;
                float isActive = 0.0;

                if (isFilled > 0.5)
                {
                    // FILLED: radius shrinks and grows with piston
                    float currentRadius = 0.49 - ((1.0 - pistonWave) * _HexPistonDepth);
                    float body = 1.0 - smoothstep(
                        currentRadius - _HexSmoothness,
                        currentRadius + _HexSmoothness,
                        dist
                    );
                    result = body * alpha * 0.7;
                    isActive = step(0.8, seed);
                }
                else
                {
                    // BORDER: thin outline that pulses in thickness with piston
                    float pulsingWidth = _HexLineWidth * (0.5 + pistonWave * 0.5);
                    float distFromEdge = abs(dist - 0.5);
                    float halfWidth = pulsingWidth * 0.5;
                    float border = 1.0 - smoothstep(
                        halfWidth - _HexSmoothness,
                        halfWidth + _HexSmoothness,
                        distFromEdge
                    );
                    // Draw animation — traces around the hex edge
                    float drawProgress = frac(time * 0.5 * seed + seed) * 2.5;
                    float drawMask = smoothstep(angle, angle - 0.05, drawProgress);
                    result = border * drawMask * alpha;
                    isActive = step(0.6, seed);
                }

                // Color: active cells glow brighter
                float3 col = lerp(_HexColor.rgb, _HexActiveColor.rgb, isActive * pistonWave);
                return col * result;
            }

            // -------------------------------------------------------
            // Data Streams
            // -------------------------------------------------------
            float dataStreams(float2 uv, float time)
            {
                float result = 0.0;
                int count = (int)_StreamCount;
                for (int i = 0; i < count; i++)
                {
                    float fi = float(i);
                    float xPos = hash1(fi * 3.17 + 0.3);
                    float speed = (hash1(fi * 1.91 + 1.1) * 0.8 + 0.4) * _StreamSpeed;
                    float len = hash1(fi * 2.53 + 0.7) * 0.3 + 0.1;
                    float yHead = frac(time * speed + hash1(fi * 4.71));
                    float xDist = abs(uv.x - xPos);
                    float xMask = gaussian(xDist, _StreamThickness);
                    float yDist = uv.y - yHead;
                    float trail = smoothstep(0.0, len, -yDist) * smoothstep(len * 1.5, 0.0, -yDist);
                    float head  = gaussian(abs(yDist), 0.005) * 3.0;
                    result += xMask * (trail + head);
                }
                return saturate(result);
            }

            // -------------------------------------------------------
            // Glitch Bars
            // -------------------------------------------------------
            float glitchBars(float2 uv, float time)
            {
                float bandID = floor(uv.y * 30.0);
                float bandRand = hash(float2(bandID, floor(time * _GlitchSpeed)));
                float active = step(1.0 - _GlitchFrequency, bandRand);
                float flicker = hash(float2(bandID, floor(time * _GlitchSpeed * 3.0)));
                return active * flicker;
            }

            // -------------------------------------------------------
            // Scan Pulse
            // -------------------------------------------------------
            float scanPulse(float2 uv, float time)
            {
                float scanY = frac(time * _ScanSpeed);
                float dist = abs(uv.y - scanY);
                float leading = gaussian(dist, 0.008) * 3.0;
                float trail = smoothstep(0.0, 0.15, scanY - uv.y) *
                              smoothstep(0.15, 0.0, scanY - uv.y - 0.15);
                return saturate(leading + trail * 0.3);
            }

            // -------------------------------------------------------
            // Circuit Traces
            // -------------------------------------------------------
            float circuitTraces(float2 uv, float time)
            {
                float2 cUV = uv * _CircuitScale;
                float2 cell = floor(cUV);
                float2 local = frac(cUV);
                float result = 0.0;

                float hRand = hash(cell + float2(0.1, 0.2));
                float hActive = step(0.5, sin(time * _CircuitSpeed * hRand * 3.14 + hRand * 6.28) * 0.5 + 0.5);
                float hTrace = 1.0 - smoothstep(0.48, 0.5, abs(local.y - 0.5));
                hTrace *= step(0.1, local.x) * step(local.x, 0.9);
                result += hTrace * hActive * hRand;

                float vRand = hash(cell + float2(0.7, 0.3));
                float vActive = step(0.5, sin(time * _CircuitSpeed * vRand * 3.14 + vRand * 6.28 + 1.5) * 0.5 + 0.5);
                float vTrace = 1.0 - smoothstep(0.48, 0.5, abs(local.x - 0.5));
                vTrace *= step(0.1, local.y) * step(local.y, 0.9);
                result += vTrace * vActive * vRand;

                float nodeDist = length(local - float2(0.5, 0.5));
                float node = gaussian(nodeDist, 0.04) * hash(cell + float2(0.9, 0.1));
                result += node;

                return saturate(result);
            }

            // -------------------------------------------------------

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _ManualTime;

                float edgeFade = smoothstep(0.0, _FadeLeft,   IN.uv.x)
                               * smoothstep(0.0, _FadeBottom,  IN.uv.y)
                               * smoothstep(0.0, _FadeRight,   1.0 - IN.uv.x)
                               * smoothstep(0.0, _FadeTop,     1.0 - IN.uv.y);

                // Fog
                float2 fogUV = IN.uv * _Scale;
                float layer1 = fbm(fogUV + float2(time * _Speed * 0.5, time * _Speed * 0.2));
                float layer2 = fbm(fogUV + float2(-time * _Speed * 0.3, time * _Speed * 0.15) + float2(3.7, 1.9));
                float fogMask = pow((layer1 * 0.6 + layer2 * 0.4), 0.5);
                fogMask = smoothstep(0.2 - _Density * 0.3, 1.0, fogMask);
                fogMask *= edgeFade;
                half3 fogColor = cyberGradient(layer1 * 0.5 + layer2 * 0.5);

                // Hex grid with piston pulse
                float3 hexContrib = hexGrid(IN.uv, time) * _HexOpacity * edgeFade;

                // Data streams
                float streams = dataStreams(IN.uv, time);
                half3 streamContrib = _StreamColor.rgb * streams * _StreamOpacity * edgeFade;

                // Glitch bars
                float glitch = glitchBars(IN.uv, time);
                half3 glitchContrib = _GlitchColor.rgb * glitch * _GlitchOpacity * edgeFade;

                // Scan pulse
                float scan = scanPulse(IN.uv, time);
                half3 scanContrib = _ScanColor.rgb * scan * _ScanOpacity * edgeFade;

                // Circuit traces
                float circuit = circuitTraces(IN.uv, time);
                half3 circuitContrib = _CircuitColor.rgb * circuit * _CircuitOpacity * edgeFade;

                // Base wash
                half4 base = _ColorA;
                base.a = _BaseOpacity * edgeFade * IN.color.a;

                // Composite
                half4 result;
                result.rgb = lerp(base.rgb, fogColor, fogMask * _FogOpacity);
                result.rgb += hexContrib;
                result.rgb += streamContrib;
                result.rgb += glitchContrib;
                result.rgb += scanContrib;
                result.rgb += circuitContrib;
                result.rgb = saturate(result.rgb);
                result.a = clamp(base.a + fogMask * _FogOpacity, 0, 1);
                result.a *= IN.color.a;

                return result;
            }
            ENDHLSL
        }
    }
}