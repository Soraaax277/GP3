Shader "Custom/IndustrialMetal"
{
    Properties
    {
        // ── Base metal colours ─────────────────────────────────────────────────
        _MetalColorA     ("Metal Color A",           Color)         = (0.28, 0.28, 0.30, 1)
        _MetalColorB     ("Metal Color B",           Color)         = (0.20, 0.21, 0.22, 1)
        _MetalVariation  ("Metal Color Variation",   Range(0.0, 1.0)) = 0.35

        // ── Rust / oxidation ───────────────────────────────────────────────────
        _RustColorA      ("Rust Color A",            Color)         = (0.55, 0.20, 0.05, 1)
        _RustColorB      ("Rust Color B",            Color)         = (0.40, 0.13, 0.03, 1)
        _RustAmount      ("Rust Amount",             Range(0.0, 1.0)) = 0.45
        _RustVariation   ("Rust Variation per Panel",Range(0.0, 1.0)) = 0.50
        _RustStreakStr   ("Rust Streak Strength",    Range(0.0, 1.0)) = 0.40

        // ── Panel seams ────────────────────────────────────────────────────────
        _SeamColor       ("Seam / Groove Color",     Color)         = (0.08, 0.08, 0.09, 1)
        _PanelWidth      ("Panel Width  (OS)",       Float)         = 0.50
        _PanelHeight     ("Panel Height (OS)",       Float)         = 0.60
        _SeamThick       ("Seam Thickness (OS)",     Range(0.002, 0.06)) = 0.018

        // ── Rivets ─────────────────────────────────────────────────────────────
        // Rivets appear at every panel corner.
        // _RivetInset controls how far in from the corner (OS).
        _RivetColor      ("Rivet Color",             Color)         = (0.35, 0.35, 0.38, 1)
        _RivetRadius     ("Rivet Radius (OS)",       Range(0.002, 0.06)) = 0.020
        _RivetInset      ("Rivet Inset from Corner (OS)", Range(0.01, 0.15)) = 0.055

        // ── Corrugation (horizontal ridges, fake depth via brightness bands) ───
        // _CorrugPitch: spacing between ridges in object-space Y units.
        // Set to a very large number (e.g. 999) to disable.
        _CorrugPitch     ("Corrugation Pitch (OS)",  Float)         = 0.06
        _CorrugStrength  ("Corrugation Strength",    Range(0.0, 1.0)) = 0.12

        // ── Specular (rough metallic blob) ─────────────────────────────────────
        _SpecColor       ("Specular Color",          Color)         = (0.70, 0.72, 0.75, 1)
        _SpecPower       ("Specular Hardness",       Range(1.0, 64.0)) = 8.0
        _SpecThresh      ("Specular Threshold",      Range(0.0, 1.0)) = 0.75
        _SpecStrength    ("Specular Strength",       Range(0.0, 1.0)) = 0.50

        // ── Rim light ──────────────────────────────────────────────────────────
        _RimColor        ("Rim Color",               Color)         = (0.50, 0.55, 0.60, 1)
        _RimPower        ("Rim Power",               Range(0.5, 10.0)) = 3.5
        _RimStrength     ("Rim Strength",            Range(0.0, 1.0))  = 0.20

        // ── Lighting & shadow ──────────────────────────────────────────────────
        _TopColor        ("Top / Roof Face Color",   Color)         = (0.18, 0.18, 0.19, 1)
        _TopThreshold    ("Top Face Threshold",      Range(0.1, 1.0)) = 0.70
        _AmbientStrength ("Ambient Strength",        Range(0.0, 1.0)) = 0.28
        _ShadowThresh    ("Shadow Snap Threshold",   Range(0.0, 1.0)) = 0.50
        _ShadowDark      ("Shadow Dark Level",       Range(0.0, 1.0)) = 0.22
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

            CBUFFER_START(UnityPerMaterial)
                half4  _MetalColorA;
                half4  _MetalColorB;
                half   _MetalVariation;
                half4  _RustColorA;
                half4  _RustColorB;
                half   _RustAmount;
                half   _RustVariation;
                half   _RustStreakStr;
                half4  _SeamColor;
                float  _PanelWidth;
                float  _PanelHeight;
                float  _SeamThick;
                half4  _RivetColor;
                float  _RivetRadius;
                float  _RivetInset;
                float  _CorrugPitch;
                half   _CorrugStrength;
                half4  _SpecColor;
                half   _SpecPower;
                half   _SpecThresh;
                half   _SpecStrength;
                half4  _RimColor;
                half   _RimPower;
                half   _RimStrength;
                half4  _TopColor;
                half   _TopThreshold;
                half   _AmbientStrength;
                half   _ShadowThresh;
                half   _ShadowDark;
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
                float3 positionOS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float  fogFactor   : TEXCOORD5;
            };

            // ── Helpers ───────────────────────────────────────────────────────

            float Hash21(float2 p)
            {
                p  = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            // Second hash with different constants for independent variation
            float Hash21b(float2 p)
            {
                p  = frac(p * float2(269.5, 183.3));
                p += dot(p, p + 47.53);
                return frac(p.x * p.y);
            }

            // ── Vertex ────────────────────────────────────────────────────────
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

            // ── Fragment ──────────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                float3 nWS    = normalize(IN.normalWS);
                float3 viewWS = normalize(IN.viewDirWS);
                float3 posOS  = IN.positionOS;

                // ── Face classification ───────────────────────────────────────
                float upDot = dot(nWS, float3(0, 1, 0));
                half  isTop = step(_TopThreshold, upDot);

                // ── Panel UV (same wall-axis logic as the other two shaders) ──
                float absNX    = abs(nWS.x);
                float absNZ    = abs(nWS.z);
                float horizPos = (absNX >= absNZ) ? posOS.z : posOS.x;

                float uCoord     = horizPos  / _PanelWidth;
                float vCoord     = posOS.y   / _PanelHeight;
                float panelCol   = floor(uCoord);
                float panelRow   = floor(vCoord);
                float panelUFrac = frac(uCoord);
                float panelVFrac = frac(vCoord);

                float2 panelID = float2(panelCol, panelRow);

                // ── Panel seam mask ───────────────────────────────────────────
                float sU = _SeamThick / _PanelWidth;
                float sV = _SeamThick / _PanelHeight;
                half  isSeam = saturate(
                    step(1.0 - sU, panelUFrac) +
                    step(panelUFrac, sU)        +
                    step(1.0 - sV, panelVFrac) +
                    step(panelVFrac, sV)
                );

                // ── Rivet mask ────────────────────────────────────────────────
                // Four rivets per panel, one near each corner.
                // We fold the UV so (0.5,0.5) is always the "corner" direction,
                // then measure actual OS distance from that folded corner point.
                float rivetUFrac = (panelUFrac < 0.5) ? panelUFrac : (1.0 - panelUFrac);
                float rivetVFrac = (panelVFrac < 0.5) ? panelVFrac : (1.0 - panelVFrac);

                // Convert fraction to OS distance from the edge, then subtract inset
                float distFromEdgeU = rivetUFrac * _PanelWidth;
                float distFromEdgeV = rivetVFrac * _PanelHeight;
                float rivetDistOS   = length(float2(
                    distFromEdgeU - _RivetInset,
                    distFromEdgeV - _RivetInset
                ));
                half  isRivet = step(rivetDistOS, _RivetRadius);

                // ── Per-panel base metal colour ────────────────────────────────
                float metalHash  = Hash21(panelID);
                half3 metalCol   = lerp(
                    _MetalColorA.rgb,
                    _MetalColorB.rgb,
                    metalHash * _MetalVariation
                );

                // ── Per-panel rust ─────────────────────────────────────────────
                // Base rust level varies per panel
                float rustHash  = Hash21b(panelID);
                half  rustLevel = saturate(_RustAmount + (rustHash - 0.5h) * _RustVariation);

                // Rust colour also varies (A = bright orange, B = dark brown)
                float rustColHash = Hash21(panelID + float2(3.7, 9.1));
                half3 rustCol     = lerp(_RustColorA.rgb, _RustColorB.rgb, rustColHash);

                // Vertical rust streaks — heavier near the bottom of each panel
                // (water pools and drips down, leaving trails)
                float streakColHash = Hash21b(float2(panelCol + 0.5, panelRow + 13.0));
                float streakFade    = pow(1.0 - panelVFrac, 2.0);  // strongest at bottom
                half  streakRust    = (half)(streakColHash * streakFade * _RustStreakStr);

                half totalRust = saturate(rustLevel + streakRust);

                // ── Corrugation (horizontal brightness bands) ─────────────────
                // Simulates ridged/corrugated sheet metal without geometry.
                // Alternating slightly lighter / darker rows snap to 2 shades.
                float corrugRow   = floor(posOS.y / _CorrugPitch);
                half  corrugShade = (fmod(abs(corrugRow), 2.0) < 1.0)
                    ? (1.0h - _CorrugStrength)
                    : 1.0h;

                // ── Colour composition ─────────────────────────────────────────
                // Priority (high→low): top face > rivet > seam > rust > metal
                half3 surfaceCol = lerp(metalCol, rustCol, totalRust);
                surfaceCol      *= corrugShade;                           // corrugation tint
                surfaceCol       = lerp(surfaceCol, _SeamColor.rgb,  isSeam  * (1.0h - isTop));
                surfaceCol       = lerp(surfaceCol, _RivetColor.rgb, isRivet * (1.0h - isTop));
                surfaceCol       = lerp(surfaceCol, _TopColor.rgb,   isTop);

                // ── Lighting ──────────────────────────────────────────────────
                Light mainLight = GetMainLight(IN.shadowCoord);

                // Shadow snap — binary, keeps the flat low-poly look
                half snapShadow = step(_ShadowThresh, mainLight.shadowAttenuation);
                half shadowMult = lerp(_ShadowDark, 1.0h, snapShadow);

                // 3-step cel diffuse, same as the brick & glass shaders
                float NdotL   = dot(nWS, mainLight.direction);
                float litFact = NdotL * (float)snapShadow;
                half  stepped = litFact < 0.0   ? 0.20h :
                                litFact < 0.45  ? 0.55h : 1.00h;

                // ── Toon specular ─────────────────────────────────────────────
                // Rough metal = broad, dim, hard-edged blob (not a mirror shine).
                // Rust patches kill the specular — corroded surfaces don't reflect.
                float3 halfWS   = normalize(mainLight.direction + viewWS);
                float  NdotH    = saturate(dot(nWS, halfWS));
                float  specRaw  = pow(NdotH, (float)_SpecPower);
                half   celSpec  = step(_SpecThresh, (half)specRaw);
                half3  specCol  = _SpecColor.rgb * celSpec * _SpecStrength;
                specCol        *= (1.0h - totalRust * 0.90h);  // rust kills shine
                specCol        *= (1.0h - isTop * 0.50h);      // duller on flat roof

                // ── Rim light (cold steel edge) ───────────────────────────────
                float NdotV  = saturate(dot(nWS, viewWS));
                float rim    = pow(1.0 - NdotV, _RimPower);
                half3 rimCol = _RimColor.rgb * (rim * _RimStrength * (1.0h - isTop));
                rimCol      *= (1.0h - totalRust * 0.60h);  // rust softens the rim too

                // ── Final colour ──────────────────────────────────────────────
                half3 ambient  = SampleSH(nWS) * surfaceCol * _AmbientStrength;
                half3 diffuse  = mainLight.color * surfaceCol * stepped;
                half3 finalCol = ambient + diffuse + specCol + rimCol;

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
                half4  _MetalColorA;    half4  _MetalColorB;    half   _MetalVariation;
                half4  _RustColorA;     half4  _RustColorB;     half   _RustAmount;
                half   _RustVariation;  half   _RustStreakStr;
                half4  _SeamColor;      float  _PanelWidth;     float  _PanelHeight;
                float  _SeamThick;      half4  _RivetColor;     float  _RivetRadius;
                float  _RivetInset;     float  _CorrugPitch;    half   _CorrugStrength;
                half4  _SpecColor;      half   _SpecPower;      half   _SpecThresh;
                half   _SpecStrength;   half4  _RimColor;       half   _RimPower;
                half   _RimStrength;    half4  _TopColor;       half   _TopThreshold;
                half   _AmbientStrength; half  _ShadowThresh;   half   _ShadowDark;
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
                half4  _MetalColorA;    half4  _MetalColorB;    half   _MetalVariation;
                half4  _RustColorA;     half4  _RustColorB;     half   _RustAmount;
                half   _RustVariation;  half   _RustStreakStr;
                half4  _SeamColor;      float  _PanelWidth;     float  _PanelHeight;
                float  _SeamThick;      half4  _RivetColor;     float  _RivetRadius;
                float  _RivetInset;     float  _CorrugPitch;    half   _CorrugStrength;
                half4  _SpecColor;      half   _SpecPower;      half   _SpecThresh;
                half   _SpecStrength;   half4  _RimColor;       half   _RimPower;
                half   _RimStrength;    half4  _TopColor;       half   _TopThreshold;
                half   _AmbientStrength; half  _ShadowThresh;   half   _ShadowDark;
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
