Shader "Custom/IndustrialGlass"
{
    Properties
    {
        // ── Glass base colours ─────────────────────────────────────────────────
        _InteriorColor   ("Interior Color",          Color)         = (0.04, 0.05, 0.06, 1)
        _GlassColor      ("Glass Tint",              Color)         = (0.12, 0.20, 0.22, 1)
        _ReflectColor    ("Reflection Color",        Color)         = (0.35, 0.50, 0.60, 1)

        // ── Grime / dirt ───────────────────────────────────────────────────────
        _GrimeColor      ("Grime Color",             Color)         = (0.18, 0.16, 0.12, 1)
        _GrimeAmount     ("Grime Amount",            Range(0.0, 1.0)) = 0.55
        _GrimeVariation  ("Grime Variation per Pane",Range(0.0, 1.0)) = 0.45

        // ── Window frame ───────────────────────────────────────────────────────
        _FrameColor      ("Frame Color",             Color)         = (0.14, 0.15, 0.16, 1)
        _PaneWidth       ("Pane Width  (OS)",        Float)         = 0.28
        _PaneHeight      ("Pane Height (OS)",        Float)         = 0.32
        _FrameThick      ("Frame Thickness (OS)",    Range(0.002, 0.08)) = 0.022

        // ── Sub-divide: inner cross-bar (one horizontal bar per pane) ─────────
        // Set _CrossBarV to 0 to disable (below mortar range).
        _CrossBarV       ("Cross-bar V position",    Range(0.0, 1.0)) = 0.50
        _CrossBarThick   ("Cross-bar Thickness (UV)",Range(0.0, 0.15)) = 0.04

        // ── Cel-stepped Fresnel reflection ────────────────────────────────────
        _FresnelPower    ("Fresnel Power",           Range(0.5, 8.0))  = 2.5
        _FresnelThresh   ("Fresnel Step Threshold",  Range(0.0, 1.0))  = 0.40
        _ReflectStrength ("Reflection Strength",     Range(0.0, 1.0))  = 0.55

        // ── Toon specular blob ─────────────────────────────────────────────────
        _SpecColor       ("Specular Color",          Color)         = (0.65, 0.78, 0.90, 1)
        _SpecPower       ("Specular Hardness",       Range(2.0, 64.0)) = 10.0
        _SpecThresh      ("Specular Threshold",      Range(0.0, 1.0))  = 0.70
        _SpecStrength    ("Specular Strength",       Range(0.0, 1.0))  = 0.55

        // ── Lighting ───────────────────────────────────────────────────────────
        _TopThreshold    ("Top Face Threshold",      Range(0.1, 1.0))  = 0.70
        _AmbientStrength ("Ambient Strength",        Range(0.0, 1.0))  = 0.20
        _ShadowDark      ("Shadow Dark Level",       Range(0.0, 1.0))  = 0.25
        _ShadowThresh    ("Shadow Snap Threshold",   Range(0.0, 1.0))  = 0.50
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
                half4  _InteriorColor;
                half4  _GlassColor;
                half4  _ReflectColor;
                half4  _GrimeColor;
                half   _GrimeAmount;
                half   _GrimeVariation;
                half4  _FrameColor;
                float  _PaneWidth;
                float  _PaneHeight;
                float  _FrameThick;
                float  _CrossBarV;
                float  _CrossBarThick;
                half   _FresnelPower;
                half   _FresnelThresh;
                half   _ReflectStrength;
                half4  _SpecColor;
                half   _SpecPower;
                half   _SpecThresh;
                half   _SpecStrength;
                half   _TopThreshold;
                half   _AmbientStrength;
                half   _ShadowDark;
                half   _ShadowThresh;
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

            // Same hash used in IndustrialBuilding — deterministic per-cell noise
            float Hash21(float2 p)
            {
                p  = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
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

                // ── Pane grid UV (same axis-selection logic as IndustrialBuilding)
                float absNX    = abs(nWS.x);
                float absNZ    = abs(nWS.z);
                float horizPos = (absNX >= absNZ) ? posOS.z : posOS.x;

                float uCoord    = horizPos  / _PaneWidth;
                float vCoord    = posOS.y   / _PaneHeight;
                float paneCol   = floor(uCoord);
                float paneRow   = floor(vCoord);
                float paneUFrac = frac(uCoord);
                float paneVFrac = frac(vCoord);

                // ── Frame mask (outer border of each pane) ────────────────────
                float fU = _FrameThick / _PaneWidth;
                float fV = _FrameThick / _PaneHeight;
                half  isFrame = saturate(
                    step(1.0 - fU, paneUFrac) +    // right rail
                    step(paneUFrac, fU)         +    // left rail
                    step(1.0 - fV, paneVFrac)  +    // top rail
                    step(paneVFrac, fV)              // bottom rail
                );

                // ── Optional horizontal cross-bar per pane ────────────────────
                // Gives that classic multi-pane industrial window look.
                float barDist = abs(paneVFrac - _CrossBarV);
                half  isCross = step(barDist, _CrossBarThick * 0.5);

                half isAnyFrame = saturate(isFrame + isCross);

                // ── Per-pane grime level (each pane is uniquely filthy) ────────
                float2 paneID   = float2(paneCol, paneRow);
                float  paneHash = Hash21(paneID);
                half   grime    = saturate(_GrimeAmount + (paneHash - 0.5h) * _GrimeVariation);

                // A second hash drives streaks — vertical smear within a pane.
                // Mix the streak hash along V so it varies top-to-bottom per pane.
                float streakHash = Hash21(float2(paneCol + 0.5, paneRow + 7.3));
                float streakV    = abs(paneVFrac - 0.5) * 2.0;   // 0 at centre → 1 at edge
                half  streak     = (half)(streakHash * streakV * grime * 0.5);

                half totalGrime = saturate(grime + streak);

                // ── Cel-stepped Fresnel ───────────────────────────────────────
                // Grazing angle → show sky/environment reflection.
                // Head-on      → show dark interior through the dirty glass.
                float NdotV     = saturate(dot(nWS, viewWS));
                float fresnelRaw = pow(1.0 - NdotV, (float)_FresnelPower);
                half  celFresnel = step(_FresnelThresh, (half)fresnelRaw); // binary snap

                // ── Glass colour composition ──────────────────────────────────
                // 1. Start from dark interior
                half3 glassCol = _InteriorColor.rgb;
                // 2. Tint with glass colour (adds a coloured cast)
                glassCol = lerp(glassCol, _GlassColor.rgb, 0.45h);
                // 3. At grazing angles, blend in reflection
                glassCol = lerp(glassCol, _ReflectColor.rgb, celFresnel * _ReflectStrength);
                // 4. Layer grime on top — dirty panes lose the glass colour,
                //    gain the grimy brown. Reflection punches through grime slightly.
                glassCol = lerp(glassCol, _GrimeColor.rgb,
                                totalGrime * (1.0h - celFresnel * 0.35h));

                // ── Toon specular blob ────────────────────────────────────────
                // Single hard-edged blob — broad and ugly, like light on filthy glass.
                Light  mainLight = GetMainLight(IN.shadowCoord);
                float3 halfWS    = normalize(mainLight.direction + viewWS);
                float  NdotH     = saturate(dot(nWS, halfWS));
                float  specRaw   = pow(NdotH, (float)_SpecPower);
                half   celSpec   = step(_SpecThresh, (half)specRaw);  // hard threshold
                half3  specCol   = _SpecColor.rgb * celSpec * _SpecStrength;
                // Grime dulls the specular highlight — dirty glass barely glints
                specCol *= (1.0h - totalGrime * 0.80h);

                // ── Shadow snap ───────────────────────────────────────────────
                half snapShadow = step(_ShadowThresh, mainLight.shadowAttenuation);
                half shadowMult = lerp(_ShadowDark, 1.0h, snapShadow);

                // ── Diffuse (flat, same 3-step logic as IndustrialBuilding) ───
                float NdotL   = dot(nWS, mainLight.direction);
                float litFact = NdotL * (float)snapShadow;
                half  stepped = litFact < 0.0   ? 0.15h :
                                litFact < 0.45  ? 0.50h : 1.00h;

                // ── Final colour ──────────────────────────────────────────────
                half3 ambient  = SampleSH(nWS) * glassCol * _AmbientStrength;
                half3 diffuse  = mainLight.color * glassCol * stepped;
                half3 finalCol = ambient + diffuse + specCol;

                // Frames are metal, not glass — swap the glass result out
                finalCol = lerp(finalCol, _FrameColor.rgb * (0.5h + stepped * 0.5h), isAnyFrame);

                // Top faces (e.g. window sill geometry) follow the frame colour
                finalCol = lerp(finalCol, _FrameColor.rgb * stepped, isTop);

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
                half4  _InteriorColor;  half4  _GlassColor;     half4  _ReflectColor;
                half4  _GrimeColor;     half   _GrimeAmount;     half   _GrimeVariation;
                half4  _FrameColor;     float  _PaneWidth;       float  _PaneHeight;
                float  _FrameThick;     float  _CrossBarV;       float  _CrossBarThick;
                half   _FresnelPower;   half   _FresnelThresh;   half   _ReflectStrength;
                half4  _SpecColor;      half   _SpecPower;       half   _SpecThresh;
                half   _SpecStrength;   half   _TopThreshold;
                half   _AmbientStrength; half  _ShadowDark;      half   _ShadowThresh;
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
                half4  _InteriorColor;  half4  _GlassColor;     half4  _ReflectColor;
                half4  _GrimeColor;     half   _GrimeAmount;     half   _GrimeVariation;
                half4  _FrameColor;     float  _PaneWidth;       float  _PaneHeight;
                float  _FrameThick;     float  _CrossBarV;       float  _CrossBarThick;
                half   _FresnelPower;   half   _FresnelThresh;   half   _ReflectStrength;
                half4  _SpecColor;      half   _SpecPower;       half   _SpecThresh;
                half   _SpecStrength;   half   _TopThreshold;
                half   _AmbientStrength; half  _ShadowDark;      half   _ShadowThresh;
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
