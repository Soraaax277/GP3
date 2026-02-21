Shader "Custom/ProceduralHexGrid"
{
    Properties
    {
        [Header(Unity UI Requirement)]
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}

        [Header(Interaction)]
        _MouseUV ("Mouse UV Position", Vector) = (-1,-1,0,0)
        _HighlightStrength ("Highlight Brightness", Range(0, 2)) = 0.5
        _HighlightGrow ("Highlight Growth", Range(0, 0.2)) = 0.08

        [Header(Background Texture Pattern)]
        _TexColorA ("Texture Color A (Base)", Color) = (0.05, 0.08, 0.15, 1)
        _TexColorB ("Texture Color B (Splotch)", Color) = (0.02, 0.05, 0.1, 1)
        _TexScale ("Texture Scale (Size)", Float) = 12.0
        _TexPower ("Texture Blend Sharpness", Range(0.1, 5.0)) = 1.0
        _TexDistortion ("Texture Distortion", Range(0, 2)) = 0.5
        _TexScrollSpeed ("Texture Scroll Speed", Float) = 0.02

        [Header(Background Hex Grid)]
        _Scale ("Hex Grid Scale", Float) = 8.0
        _BgSpeed ("Hex Pulse Speed", Float) = 0.5
        _BgSparsity ("Hex Sparsity", Range(0, 1)) = 0.2

        [Header(Background Dynamic Sparsity)]
        [Toggle] _UseDynamicSparsity ("Enable Random Sparsity", Float) = 1
        _SparsityMin ("Sparsity Min", Range(0, 1)) = 0.1
        _SparsityMax ("Sparsity Max", Range(0, 1)) = 0.6
        _SparsityFreq ("Sparsity Change Speed", Float) = 0.2

        [Header(Background Piston Animation)]
        _PistonSpeed ("Piston Speed", Float) = 1.5
        _PistonDepth ("Piston Depth (Shrink Amount)", Range(0, 0.3)) = 0.15
        _PistonShade ("Piston Shadow Strength", Range(0, 1)) = 0.4

        [Header(Foreground Floating Grid)]
        _FloatScale ("Float Scale", Float) = 2.5
        _FloatSpeedX ("Drift Speed X", Range(-0.5, 0.5)) = 0.05
        _FloatSpeedY ("Drift Speed Y", Range(-0.5, 0.5)) = 0.02
        _FloatSparsity ("Float Sparsity", Range(0, 1)) = 0.75

        [Header(Style)]
        _AspectRatio ("Aspect Ratio", Float) = 1.77
        _LineWidth ("Line Width", Range(0.0, 0.2)) = 0.05
        _Smoothness ("Anti-Alias Softness", Range(0.001, 0.1)) = 0.02
        _DrawSpeed ("Draw Animation Speed", Range(0.1, 10)) = 3.0
        
        [Header(Hex Colors)]
        // Note: Background Colors are now controlled by Texture Color A/B above
        _ColorCyan ("Cyan (Active)", Color) = (0.0, 0.9, 1.0, 1)
        _ColorBlue ("Blue (Passive)", Color) = (0.0, 0.3, 0.7, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
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
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            sampler2D _MainTex;
            
            // --- VARIABLES ---
            // DEFINE CUSTOM TIME VARIABLE
            float _UI_UnscaledTime;

            float4 _MouseUV;
            float _HighlightStrength;
            float _HighlightGrow;

            float4 _TexColorA;
            float4 _TexColorB;
            float _TexScale;
            float _TexPower;
            float _TexDistortion;
            float _TexScrollSpeed;

            float _Scale;
            float _BgSpeed;
            float _BgSparsity;

            float _UseDynamicSparsity;
            float _SparsityMin;
            float _SparsityMax;
            float _SparsityFreq;
            
            float _PistonSpeed;
            float _PistonDepth;
            float _PistonShade;
            
            float _FloatScale;
            float _FloatSpeedX;
            float _FloatSpeedY;
            float _FloatSparsity;
            
            float _AspectRatio;
            float _LineWidth;
            float _Smoothness;
            float _DrawSpeed;
            
            float4 _ColorCyan;
            float4 _ColorBlue;

            // --- NOISE FUNCTIONS ---

            float hash(float2 p) {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 st) {
                float2 i = floor(st);
                float2 f = frac(st);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // --- HEX FUNCTIONS ---

            float4 GetHexUV(float2 uv) {
                float2 r = float2(1, 1.73);
                float2 h = r * 0.5;
                float2 a = uv / r;
                float2 b = (uv - h) / r;
                float2 id_a = floor(a);
                float2 id_b = floor(b);
                float2 center_a = (id_a + 0.5) * r;
                float2 center_b = (id_b + 0.5) * r + h;
                float2 gv_a = uv - center_a;
                float2 gv_b = uv - center_b;
                float dist_a = dot(gv_a, gv_a);
                float dist_b = dot(gv_b, gv_b);
                bool useA = dist_a < dist_b;
                float2 gv = useA ? gv_a : gv_b;
                float2 id = useA ? id_a : id_b;
                float trueID = id.x + id.y * 100.0;
                return float4(gv.x, gv.y, 0, trueID);
            }

            float2 GetHexMetrics(float2 gv) {
                float x = abs(gv.x);
                float y = abs(gv.y);
                float dist = max(x, x * 0.5 + y * 0.866025); 
                float angle = atan2(gv.x, gv.y); 
                angle = (angle / 6.28318) + 0.5; 
                return float2(dist, angle);
            }

            // Added interactionOut to pass the glow strength back to the fragment function
            float4 RenderHexGrid(float2 uv, float scale, float sparsity, float speed, float isForeground, float2 mousePos, out float pistonOut, out float interactionOut) {
                
                float4 hexData = GetHexUV(uv * scale);
                float2 gv = hexData.xy;
                float id = hexData.w;
                float2 metrics = GetHexMetrics(gv);
                float dist = metrics.x; 
                float angle = metrics.y;

                // --- INTERACTION LOGIC ---
                float2 hexCenter = (uv * scale) - gv; 
                float distToMouse = distance(hexCenter, mousePos);

                float interact = smoothstep(0.6, 0.0, distToMouse);
                interact *= step(distToMouse, 0.55);
                
                interactionOut = interact; 
                // ------------------------------

                float seed = hash(float2(id, id * 0.5));
                
                // FIX: Use _UI_UnscaledTime instead of _Time.y
                float timeVar = _UI_UnscaledTime * speed + seed * 100.0;
                float lifeCycle = frac(timeVar); 

                float visible = step(sparsity, seed);
                float fillThreshold = isForeground > 0.5 ? 0.8 : 0.6; 
                float isFilled = step(fillThreshold, hash(float2(id, seed * 2.0))); 

                float opacity = 0;
                float isHighlight = 0;
                
                float pistonWave = 1.0;
                if (isForeground < 0.5) {
                    // FIX: Use _UI_UnscaledTime
                    pistonWave = sin(_UI_UnscaledTime * _PistonSpeed + (seed * 15.0)) * 0.5 + 0.5;
                }
                pistonOut = pistonWave; 

                if (visible > 0.5) {
                    float alpha = smoothstep(0.0, 0.15, lifeCycle) * smoothstep(1.0, 0.85, lifeCycle);

                    if (isFilled > 0.5) {
                        float growOffset = interact * _HighlightGrow;
                        
                        float currentRadius = 0.49 - ((1.0 - pistonWave) * _PistonDepth);
                        currentRadius += growOffset;

                        float body = 1.0 - smoothstep(currentRadius - _Smoothness, currentRadius + _Smoothness, dist);
                        opacity = body * alpha * 0.6;
                        isHighlight = step(0.9, seed);
                    } else {
                        float distFromEdge = abs(dist - 0.5);
                        float targetWidth = isForeground > 0.5 ? _LineWidth * 1.5 : _LineWidth;
                        float halfWidth = targetWidth * 0.5; 
                        float border = 1.0 - smoothstep(halfWidth - _Smoothness, halfWidth + _Smoothness, distFromEdge);

                        float animSpeed = isForeground > 0.5 ? _DrawSpeed : _DrawSpeed * 0.5;
                        float drawProgress = lifeCycle * animSpeed;
                        float drawMask = smoothstep(angle, angle - 0.1, drawProgress);

                        opacity = border * drawMask * alpha;
                        isHighlight = step(0.6, seed);
                    }
                }
                return float4(opacity, isHighlight, 0, 0);
            }

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                uv.x *= _AspectRatio;

                float2 mouseGridPos = _MouseUV.xy;
                mouseGridPos.x *= _AspectRatio;
                mouseGridPos *= _Scale;

                // --- 1. GENERATE BACKGROUND TEXTURE (Splotches) ---
                float2 noiseUV = uv * _TexScale;
                // FIX: Use _UI_UnscaledTime
                noiseUV += _UI_UnscaledTime * _TexScrollSpeed;
                float2 distortion = float2(noise(noiseUV + 10.0), noise(noiseUV + 20.0)) * _TexDistortion;
                float n = noise(noiseUV + distortion);
                n = pow(n, _TexPower);
                float4 texturedBg = lerp(_TexColorA, _TexColorB, n);

                // --- 2. DYNAMIC SPARSITY ---
                float currentSparsity = _BgSparsity;
                if (_UseDynamicSparsity > 0.5) {
                    // FIX: Use _UI_UnscaledTime
                    float wave = sin(_UI_UnscaledTime * _SparsityFreq) * 0.5 + 0.5;
                    currentSparsity = lerp(_SparsityMin, _SparsityMax, wave);
                }

                // --- 3. RENDER LAYERS ---
                float pistonHeight;
                float interactBg;
                float4 bgLayer = RenderHexGrid(uv, _Scale, currentSparsity, _BgSpeed, 0.0, mouseGridPos, pistonHeight, interactBg);

                float dummyHeight;
                float dummyInteract;
                float2 floatUV = uv;
                // FIX: Use _UI_UnscaledTime
                floatUV.x += _UI_UnscaledTime * _FloatSpeedX;
                floatUV.y += _UI_UnscaledTime * _FloatSpeedY;
                
                float4 floatLayer = RenderHexGrid(floatUV, _FloatScale, _FloatSparsity, _BgSpeed * 0.8, 1.0, float2(999,999), dummyHeight, dummyInteract);

                // --- 4. COMPOSITE ---
                float4 finalColor = texturedBg;

                float4 bgHexColor = lerp(_ColorBlue, _ColorCyan, bgLayer.y);
                float shadeFactor = lerp(1.0 - _PistonShade, 1.0, pistonHeight);
                bgHexColor.rgb *= shadeFactor;
                
                bgHexColor.rgb += interactBg * _HighlightStrength;

                finalColor = lerp(finalColor, bgHexColor, bgLayer.x);

                float4 floatHexColor = lerp(_ColorBlue, _ColorCyan, floatLayer.y);
                finalColor = max(finalColor, floatHexColor * floatLayer.x); 

                return finalColor;
            }
            ENDHLSL
        }
    }
}