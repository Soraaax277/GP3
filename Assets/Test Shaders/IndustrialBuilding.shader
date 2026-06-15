Shader "Custom/IndustrialBuilding"
{
    Properties
    {
        // ── Brick colours ──────────────────────────────────────────────────────
        _BrickColorA     ("Brick Color A",           Color)         = (0.52, 0.20, 0.10, 1)
        _BrickColorB     ("Brick Color B",           Color)         = (0.40, 0.15, 0.08, 1)
        _MortarColor     ("Mortar Color",            Color)         = (0.22, 0.21, 0.20, 1)

        // ── Surface zones ──────────────────────────────────────────────────────
        _RoofColor       ("Roof Color",              Color)         = (0.12, 0.13, 0.14, 1)
        _MetalTrimColor  ("Metal Trim Color",        Color)         = (0.18, 0.20, 0.22, 1)

        // ── Brick geometry (in object/local space units) ───────────────────────
        _BrickWidth      ("Brick Width  (OS)",       Float)         = 0.25
        _BrickHeight     ("Brick Height (OS)",       Float)         = 0.10
        _MortarThick     ("Mortar Thickness (OS)",   Range(0.001, 0.06)) = 0.012
        _BrickVariation  ("Brick Color Variation",   Range(0.0, 1.0))    = 0.40

        // ── Metal trim band (horizontal stripe, e.g. at floor level) ──────────
        // Set _TrimBandY to the Y height in object space where you want the stripe.
        // Set _TrimBandHeight to its vertical thickness.
        _TrimBandY       ("Trim Band Center Y (OS)", Float)         = 0.0
        _TrimBandHeight  ("Trim Band Height  (OS)",  Float)         = 0.08

        // ── Top-face detection ─────────────────────────────────────────────────
        _TopThreshold    ("Top Face Threshold",      Range(0.1, 1.0)) = 0.70

        // ── Cell-shading thresholds ────────────────────────────────────────────
        _ShadeThreshLow  ("Shade Threshold Low",     Range(0.0, 1.0)) = 0.00
        _ShadeThreshMid  ("Shade Threshold Mid",     Range(0.0, 1.0)) = 0.45
        _ShadeLevelDark  ("Shade Level Dark",        Range(0.0, 1.0)) = 0.20
        _ShadeLevelMid   ("Shade Level Mid",         Range(0.0, 1.0)) = 0.55
        // Full bright = 1.0

        // ── Shadow snap ────────────────────────────────────────────────────────
        _ShadowThresh    ("Shadow Snap Threshold",   Range(0.0, 1.0)) = 0.50
        _ShadowDark      ("Shadow Dark Level",       Range(0.0, 1.0)) = 0.30

        // ── Rim light (industrial edge glow) ──────────────────────────────────
        _RimColor        ("Rim Color",               Color)         = (0.85, 0.45, 0.10, 1)
        _RimPower        ("Rim Power",               Range(0.5, 10.0)) = 4.0
        _RimStrength     ("Rim Strength",            Range(0.0, 1.0))  = 0.25

        // ── Ambient ────────────────────────────────────────────────────────────
        _AmbientStrength ("Ambient Strength",        Range(0.0, 1.0)) = 0.30
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ─────────────────────────────────────────────────────────────────────
        // PASS 1 — Forward Lit
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── SRP Batcher constant buffer ───────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                half4  _BrickColorA;
                half4  _BrickColorB;
                half4  _MortarColor;
                half4  _RoofColor;
                half4  _MetalTrimColor;
                float  _BrickWidth;
                float  _BrickHeight;
                float  _MortarThick;
                half   _BrickVariation;
                float  _TrimBandY;
                float  _TrimBandHeight;
                half   _TopThreshold;
                half   _ShadeThreshLow;
                half   _ShadeThreshMid;
                half   _ShadeLevelDark;
                half   _ShadeLevelMid;
                half   _ShadowThresh;
                half   _ShadowDark;
                half4  _RimColor;
                half   _RimPower;
                half   _RimStrength;
                half   _AmbientStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 positionOS  : TEXCOORD1;    // object-space pos for brick UV
                float3 normalWS    : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float  fogFactor   : TEXCOORD5;
            };

            // ── Helpers ───────────────────────────────────────────────────────

            // Fast, deterministic 2-D → 1-D hash, returns [0,1)
            float Hash21(float2 p)
            {
                p  = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            // Discrete 3-step cell shade identical to the reference tile shaders,
            // but also snaps the received shadow so it stays low-poly / flat.
            half CelShade(float NdotL, float shadowAtten,
                          half threshLow, half threshMid,
                          half levelDark, half levelMid,
                          half shadowThresh, half shadowDark)
            {
                // Snap shadow to a binary step → avoids gradient bleeding
                half snapShadow = step(shadowThresh, shadowAtten);
                half shadowMult = lerp(shadowDark, 1.0h, snapShadow);

                float lit = NdotL * (float)snapShadow;   // killed in shadow
                half shade = lit < threshLow ? levelDark :
                             lit < threshMid ? levelMid  : 1.0h;

                // In-shadow pixels use shadowDark as ceiling
                shade *= shadowMult;
                return shade;
            }

            // ── Vertex shader ─────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.normalWS    = vni.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(vpi.positionWS);
                OUT.shadowCoord = GetShadowCoord(vpi);
                OUT.fogFactor   = ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            // ── Fragment shader ───────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                float3 nWS    = normalize(IN.normalWS);
                float3 viewWS = normalize(IN.viewDirWS);
                float3 posOS  = IN.positionOS;

                // ── Face classification ───────────────────────────────────────
                float upDot = dot(nWS, float3(0, 1, 0));
                half  isTop = step(_TopThreshold, upDot);

                // ── Brick UV (object-space, wraps all four vertical walls) ─────
                // Determine which horizontal axis to use based on the face normal.
                // A face whose normal is more X-aligned → sample along Z (and v.v.)
                // so the pattern is always "across" the wall, never "into" it.
                float absNX    = abs(nWS.x);
                float absNZ    = abs(nWS.z);
                float brickH   = posOS.x;              // horizontal axis candidate A
                float brickH2  = posOS.z;              // horizontal axis candidate B
                float horizPos = (absNX >= absNZ) ? brickH2 : brickH;
                //   X-facing wall → use Z as horizontal  ✓
                //   Z-facing wall → use X as horizontal  ✓

                // Staggered row offset (every other row shifts half a brick width)
                float row        = floor(posOS.y / _BrickHeight);
                float stagger    = (fmod(abs(row), 2.0) < 1.0) ? 0.5 : 0.0;
                float uCoord     = horizPos / _BrickWidth + stagger;
                float brickCol   = floor(uCoord);
                float brickUFrac = frac(uCoord);
                float brickVFrac = frac(posOS.y / _BrickHeight);

                // Mortar lines — thin UV-space band at the brick edges
                float mU = _MortarThick / _BrickWidth;
                float mV = _MortarThick / _BrickHeight;
                half  isMortar = saturate(
                    step(1.0 - mU, brickUFrac) +   // right mortar seam
                    step(brickVFrac, mV)             // bottom mortar seam
                );

                // Per-brick colour variation via noise hash on (column, row)
                float  hashVal  = Hash21(float2(brickCol, row));
                half3  brickCol3 = lerp(
                    _BrickColorA.rgb,
                    _BrickColorB.rgb,
                    hashVal * _BrickVariation
                );

                // ── Metal trim band (horizontal stripe at _TrimBandY) ─────────
                float trimDist = abs(posOS.y - _TrimBandY);
                half  isTrim   = step(trimDist, _TrimBandHeight * 0.5);

                // ── Colour composition (priority: top > trim > mortar > brick) ─
                half3 col = lerp(brickCol3,          _MortarColor.rgb,    isMortar);
                col        = lerp(col,               _MetalTrimColor.rgb, isTrim * (1.0h - isTop));
                col        = lerp(col,               _RoofColor.rgb,      isTop);

                // ── Cell-shaded lighting ──────────────────────────────────────
                Light mainLight = GetMainLight(IN.shadowCoord);
                float NdotL     = dot(nWS, mainLight.direction);
                half  shade     = CelShade(
                    NdotL,
                    mainLight.shadowAttenuation,
                    _ShadeThreshLow, _ShadeThreshMid,
                    _ShadeLevelDark, _ShadeLevelMid,
                    _ShadowThresh,   _ShadowDark
                );

                // ── Rim light — industrial orange-ember fringe on silhouettes ──
                // Only on vertical faces; roof doesn't need it.
                float NdotV  = saturate(dot(nWS, viewWS));
                float rim    = pow(1.0 - NdotV, _RimPower);
                half3 rimCol = _RimColor.rgb * (rim * _RimStrength * (1.0h - isTop));

                // ── Final colour ──────────────────────────────────────────────
                half3 ambient  = SampleSH(nWS) * col * _AmbientStrength;
                half3 diffuse  = mainLight.color * col * shade;
                half3 finalCol = ambient + diffuse + rimCol;

                finalCol = MixFog(finalCol, IN.fogFactor);
                return half4(finalCol, 1.0);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────
        // PASS 2 — Shadow Caster
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BrickColorA;    half4  _BrickColorB;    half4  _MortarColor;
                half4  _RoofColor;      half4  _MetalTrimColor;
                float  _BrickWidth;     float  _BrickHeight;    float  _MortarThick;
                half   _BrickVariation; float  _TrimBandY;      float  _TrimBandHeight;
                half   _TopThreshold;
                half   _ShadeThreshLow; half   _ShadeThreshMid;
                half   _ShadeLevelDark; half   _ShadeLevelMid;
                half   _ShadowThresh;   half   _ShadowDark;
                half4  _RimColor;       half   _RimPower;       half   _RimStrength;
                half   _AmbientStrength;
            CBUFFER_END

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings shadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(posWS, normWS, _LightDirection));
                return OUT;
            }
            half4 shadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────
        // PASS 3 — Depth Only
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BrickColorA;    half4  _BrickColorB;    half4  _MortarColor;
                half4  _RoofColor;      half4  _MetalTrimColor;
                float  _BrickWidth;     float  _BrickHeight;    float  _MortarThick;
                half   _BrickVariation; float  _TrimBandY;      float  _TrimBandHeight;
                half   _TopThreshold;
                half   _ShadeThreshLow; half   _ShadeThreshMid;
                half   _ShadeLevelDark; half   _ShadeLevelMid;
                half   _ShadowThresh;   half   _ShadowDark;
                half4  _RimColor;       half   _RimPower;       half   _RimStrength;
                half   _AmbientStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings depthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half4 depthFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
