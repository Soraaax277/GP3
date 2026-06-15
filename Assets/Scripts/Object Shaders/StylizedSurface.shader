// =============================================================================
//  StylizedSurface.shader  —  Flat Kit-inspired stylized surface for URP
//
//  FEATURES:
//    · Cel Shading — None / Single / Steps modes
//    · Extra Cel Layer — a second independent shadow band
//    · Specular — hard toon highlight
//    · Rim — fresnel edge glow / wrap light
//    · Height Gradient — world-space color overlay top-to-bottom
//    · Light Color Contribution — how much scene light tints the surface
//    · Inverted Hull Outline — per-object outline baked into this shader,
//      no renderer feature required. Works independently of CelShadeFilter.
//    · DepthNormals pass — so CelShadeFilter's global outline still works too
//
//  USE ON:  Explored / revealed tiles (pairs with ToonLit_Hidden on hidden tiles)
// =============================================================================

Shader "Custom/URP/StylizedSurface"
{
    Properties
    {
        // ── Base ──────────────────────────────────────────────────────────────
        [MainTexture] _BaseMap   ("Albedo Texture", 2D)      = "white" {}
        [MainColor]   _BaseColor ("Color",          Color)   = (1,1,1,1)

        // ── Cel Shading ───────────────────────────────────────────────────────
        [Header(Cel Shading)]
        [Enum(None,0,Single,1,Steps,2)]
        _CelMode          ("Mode  (None / Single / Steps)", Float) = 1

        // Shared by Single + Steps
        _ColorShaded      ("Color Shaded",          Color)   = (0.35,0.40,0.50,1)

        // Single mode
        _SelfShadingSize  ("Shadow Size  [Single]", Range(0,1))   = 0.5
        _ShadowEdgeSize   ("Shadow Edge  [Single]", Range(0,0.5)) = 0.05

        // Steps mode
        _StepCount        ("Step Count   [Steps]",  Range(1,8))   = 3

        // ── Extra Cel Layer ───────────────────────────────────────────────────
        [Header(Extra Cel Layer)]
        [Toggle(_EXTRA_CEL)] _EnableExtraCel ("Enable",    Float) = 0
        _ExtraCelColor    ("Color",     Color)   = (0.2,0.22,0.30,1)
        _ExtraCelSize     ("Size",      Range(0,1))   = 0.3
        _ExtraCelEdge     ("Edge",      Range(0,0.5)) = 0.03

        // ── Specular ──────────────────────────────────────────────────────────
        [Header(Specular)]
        [Toggle(_SPECULAR)] _EnableSpecular ("Enable",         Float) = 0
        _SpecColor        ("Specular Color",    Color)   = (1,1,1,1)
        _SpecSize         ("Specular Size",     Range(0,1))   = 0.2
        _SpecEdge         ("Specular Edge",     Range(0,0.5)) = 0.02

        // ── Rim ───────────────────────────────────────────────────────────────
        [Header(Rim)]
        [Toggle(_RIM)] _EnableRim ("Enable",          Float) = 0
        _RimColor         ("Rim Color",         Color)   = (0.8,0.9,1.0,1)
        _RimSize          ("Rim Size",          Range(0,1))   = 0.4
        _RimEdge          ("Rim Edge",          Range(0,0.5)) = 0.1
        _RimLightAlign    ("Light Align",       Range(0,1))   = 0.0

        // ── Height Gradient ───────────────────────────────────────────────────
        [Header(Height Gradient)]
        [Toggle(_HEIGHT_GRAD)] _EnableHeightGrad ("Enable",   Float) = 0
        _GradientColor    ("Gradient Color",    Color)   = (0.1,0.15,0.25,1)
        _GradientCenter   ("Center Y (world)",  Float)   = 0.0
        _GradientSize     ("Size",              Float)   = 2.0

        // ── Advanced Lighting ─────────────────────────────────────────────────
        [Header(Advanced Lighting)]
        [Range(0,1)] _LightColorContrib ("Light Color Contribution", Float) = 0.0

        // ── Per-Object Outline (Inverted Hull) ────────────────────────────────
        [Header(Per Object Outline)]
        [Toggle(_OUTLINE)] _EnableOutline ("Enable",         Float) = 0
        _OutlineColor     ("Color",       Color)   = (0.1,0.1,0.12,1)
        _OutlineWidth     ("Width",       Range(0,10))  = 2.0
        _OutlineDepthOffset ("Depth Offset", Range(-1,1)) = 0.0

        // ── Emission ──────────────────────────────────────────────────────────
        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)

        // ── Render State ──────────────────────────────────────────────────────
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        // =====================================================================
        //  PASS 1 — Inverted Hull Outline
        //  Renders the mesh slightly enlarged, front-faces culled, in a flat
        //  outline color. The result peeks out behind the main mesh as an outline.
        //  This runs FIRST so the main surface draws on top of it.
        // =====================================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   outlineVert
            #pragma fragment outlineFrag
            #pragma target   3.5
            #pragma shader_feature_local _OUTLINE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ColorShaded;
                float4 _ExtraCelColor;
                float4 _SpecColor;
                float4 _RimColor;
                float4 _GradientColor;
                float4 _EmissionColor;
                float4 _OutlineColor;
                float  _CelMode;
                float  _SelfShadingSize;
                float  _ShadowEdgeSize;
                float  _StepCount;
                float  _EnableExtraCel;
                float  _ExtraCelSize;
                float  _ExtraCelEdge;
                float  _EnableSpecular;
                float  _SpecSize;
                float  _SpecEdge;
                float  _EnableRim;
                float  _RimSize;
                float  _RimEdge;
                float  _RimLightAlign;
                float  _EnableHeightGrad;
                float  _GradientCenter;
                float  _GradientSize;
                float  _LightColorContrib;
                float  _EnableOutline;
                float  _OutlineWidth;
                float  _OutlineDepthOffset;
            CBUFFER_END

            struct OAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct OVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            OVaryings outlineVert(OAttributes IN)
            {
                OVaryings OUT;

                #ifdef _OUTLINE
                    // Expand the mesh outward along the view-space normal so it
                    // peeks out evenly around the silhouette regardless of rotation.
                    // Scaling by positionCS.w keeps the width screen-space consistent
                    // (outline stays the same pixel thickness at any camera distance).
                    float4 posCS   = TransformObjectToHClip(IN.positionOS.xyz);

                    // Normal → clip space (just direction, not position)
                    float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                    float4 normalCS = mul(UNITY_MATRIX_VP, float4(normalWS, 0.0));

                    // Offset along screen-space normal, scaled by clip w for consistency
                    float2 offset = normalize(normalCS.xy) * (_OutlineWidth * 0.001) * posCS.w;
                    posCS.xy += offset;

                    // Depth offset — pushes outline behind/in-front of surface
                    posCS.z += _OutlineDepthOffset * 0.01 * posCS.w;

                    OUT.positionCS = posCS;
                #else
                    // Outline disabled — collapse to degenerate so nothing draws
                    OUT.positionCS = float4(0,0,0,0);
                #endif

                return OUT;
            }

            float4 outlineFrag(OVaryings IN) : SV_Target
            {
                #ifdef _OUTLINE
                    return _OutlineColor;
                #else
                    return float4(0,0,0,0);
                #endif
            }

            ENDHLSL
        }

        // =====================================================================
        //  PASS 2 — ForwardLit  (main surface shading)
        // =====================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #pragma shader_feature_local _EXTRA_CEL
            #pragma shader_feature_local _SPECULAR
            #pragma shader_feature_local _RIM
            #pragma shader_feature_local _HEIGHT_GRAD
            #pragma shader_feature_local _OUTLINE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ColorShaded;
                float4 _ExtraCelColor;
                float4 _SpecColor;
                float4 _RimColor;
                float4 _GradientColor;
                float4 _EmissionColor;
                float4 _OutlineColor;
                float  _CelMode;
                float  _SelfShadingSize;
                float  _ShadowEdgeSize;
                float  _StepCount;
                float  _EnableExtraCel;
                float  _ExtraCelSize;
                float  _ExtraCelEdge;
                float  _EnableSpecular;
                float  _SpecSize;
                float  _SpecEdge;
                float  _EnableRim;
                float  _RimSize;
                float  _RimEdge;
                float  _RimLightAlign;
                float  _EnableHeightGrad;
                float  _GradientCenter;
                float  _GradientSize;
                float  _LightColorContrib;
                float  _EnableOutline;
                float  _OutlineWidth;
                float  _OutlineDepthOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Cel shading helpers ───────────────────────────────────────────

            // Applies one shadow band.
            //   attenuation : raw NdotL * shadow map attenuation (0-1)
            //   size        : where the shadow threshold sits (higher = more shadow)
            //   edge        : feather width around the threshold
            // Returns 0 (in shadow) or 1 (in light) with a tiny smoothstep feather.
            float CelBand(float attenuation, float size, float edge)
            {
                float lo = saturate(size - edge);
                float hi = saturate(size + edge);
                return smoothstep(lo, hi, attenuation);
            }

            // Evaluate the chosen cel mode and return a 0-1 lit factor.
            // 0 = fully in shadow color, 1 = fully in base color.
            float EvalCelMode(float NdotL)
            {
                float atten = saturate(NdotL);

                // None mode — return 1 (full base color, no shadow)
                if (_CelMode < 0.5) return 1.0;

                // Single mode — one band at _SelfShadingSize
                if (_CelMode < 1.5)
                    return CelBand(atten, 1.0 - _SelfShadingSize, _ShadowEdgeSize);

                // Steps mode — snap to N discrete bands
                float stepped = floor(atten * _StepCount) / _StepCount;
                return stepped;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = nrmInputs.normalWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // ── Albedo ────────────────────────────────────────────────────
                float4 texSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 albedo    = texSample.rgb * _BaseColor.rgb;

                // ── Vectors ───────────────────────────────────────────────────
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);

                // ── Main light ────────────────────────────────────────────────
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light  mainLight   = GetMainLight(shadowCoord);
                float3 L           = normalize(mainLight.direction);

                float NdotL = dot(N, L);
                float atten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float lit   = EvalCelMode(NdotL * atten);

                // Blend light color contribution — 0 = ignore scene light color,
                // 1 = fully multiply surface by scene light color.
                float3 lightTint = lerp(float3(1,1,1), mainLight.color, _LightColorContrib);

                // Lerp from shadow color to base color based on lit factor
                float3 color = lerp(_ColorShaded.rgb, albedo, lit) * lightTint;

                // ── Extra Cel Layer ───────────────────────────────────────────
                #ifdef _EXTRA_CEL
                {
                    float extraLit = CelBand(saturate(NdotL * atten),
                                             1.0 - _ExtraCelSize, _ExtraCelEdge);
                    // Only darkens further — doesn't override the base shading
                    color = lerp(_ExtraCelColor.rgb, color, extraLit);
                }
                #endif

                // ── Additional lights ─────────────────────────────────────────
                #ifdef _ADDITIONAL_LIGHTS
                {
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < lightCount; ++i)
                    {
                        Light  al      = GetAdditionalLight(i, IN.positionWS);
                        float3 alL     = normalize(al.direction);
                        float  alNdotL = dot(N, alL);
                        float  alAtten = al.shadowAttenuation * al.distanceAttenuation;
                        float  alLit   = EvalCelMode(alNdotL * alAtten);
                        float3 alTint  = lerp(float3(1,1,1), al.color, _LightColorContrib);
                        // Additive contribution — only adds light, doesn't darken
                        color += albedo * alTint * saturate(alLit - (1.0 - lit));
                    }
                }
                #endif

                // ── Specular ──────────────────────────────────────────────────
                #ifdef _SPECULAR
                {
                    float3 H      = normalize(L + V);
                    float  NdotH  = saturate(dot(N, H));
                    // Map _SpecSize to a shininess exponent — smaller size = sharper
                    float  power  = max(1.0, (1.0 - _SpecSize) * 128.0);
                    float  spec   = pow(NdotH, power);
                    float  mask   = CelBand(spec, 1.0 - _SpecEdge, _SpecEdge * 0.5);
                    color += _SpecColor.rgb * mask * atten;
                }
                #endif

                // ── Rim ───────────────────────────────────────────────────────
                #ifdef _RIM
                {
                    float NdotV = saturate(dot(N, V));
                    float rim   = 1.0 - NdotV;

                    // Light Align: 0 = view-only rim (both sides),
                    //              1 = only shows on lit side
                    float litMask = lerp(1.0, smoothstep(0.0, 0.5, NdotL), _RimLightAlign);

                    float rimMask = CelBand(rim, 1.0 - _RimSize, _RimEdge);
                    color += _RimColor.rgb * rimMask * litMask;
                }
                #endif

                // ── Height Gradient ───────────────────────────────────────────
                #ifdef _HEIGHT_GRAD
                {
                    // World-space Y gradient — fades from gradient color at
                    // _GradientCenter to transparent further away.
                    float gradT = saturate(
                        (IN.positionWS.y - _GradientCenter) / max(0.001, _GradientSize)
                    );
                    // gradT = 0 at/below center, 1 above — invert so color sits at bottom
                    gradT = 1.0 - gradT;
                    color = lerp(color, _GradientColor.rgb, gradT * _GradientColor.a);
                }
                #endif

                // ── Ambient / GI ──────────────────────────────────────────────
                float3 ambient = SampleSH(N);
                color += albedo * ambient * 0.1; // subtle — prevents pure black shadows

                // ── Emission ──────────────────────────────────────────────────
                color += _EmissionColor.rgb;

                // ── Fog ───────────────────────────────────────────────────────
                color = MixFog(color, IN.fogFactor);

                return float4(color, texSample.a * _BaseColor.a);
            }

            ENDHLSL
        }

        // =====================================================================
        //  PASS 3 — ShadowCaster  (boilerplate)
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #pragma target   3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor; float4 _ColorShaded; float4 _ExtraCelColor;
                float4 _SpecColor; float4 _RimColor; float4 _GradientColor;
                float4 _EmissionColor; float4 _OutlineColor;
                float  _CelMode; float _SelfShadingSize; float _ShadowEdgeSize;
                float  _StepCount; float _EnableExtraCel; float _ExtraCelSize; float _ExtraCelEdge;
                float  _EnableSpecular; float _SpecSize; float _SpecEdge;
                float  _EnableRim; float _RimSize; float _RimEdge; float _RimLightAlign;
                float  _EnableHeightGrad; float _GradientCenter; float _GradientSize;
                float  _LightColorContrib; float _EnableOutline; float _OutlineWidth; float _OutlineDepthOffset;
            CBUFFER_END

            struct SA { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct SV { float4 positionCS:SV_POSITION; };

            SV shadowVert(SA IN)
            {
                SV OUT;
                float3 pw = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(pw, nw, normalize(_LightPosition - pw)));
                #else
                    OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(pw, nw, _LightDirection));
                #endif
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return OUT;
            }

            float4 shadowFrag(SV IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // =====================================================================
        //  PASS 4 — DepthNormals
        //  Keeps this shader visible to CelShadeFilter's screen-space
        //  outline pass. Both outline systems work simultaneously —
        //  the inverted hull gives a tight per-object border, CelShadeFilter
        //  gives softer edges between adjacent objects.
        // =====================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   dnVert
            #pragma fragment dnFrag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor; float4 _ColorShaded; float4 _ExtraCelColor;
                float4 _SpecColor; float4 _RimColor; float4 _GradientColor;
                float4 _EmissionColor; float4 _OutlineColor;
                float  _CelMode; float _SelfShadingSize; float _ShadowEdgeSize;
                float  _StepCount; float _EnableExtraCel; float _ExtraCelSize; float _ExtraCelEdge;
                float  _EnableSpecular; float _SpecSize; float _SpecEdge;
                float  _EnableRim; float _RimSize; float _RimEdge; float _RimLightAlign;
                float  _EnableHeightGrad; float _GradientCenter; float _GradientSize;
                float  _LightColorContrib; float _EnableOutline; float _OutlineWidth; float _OutlineDepthOffset;
            CBUFFER_END

            struct DNA { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct DNV { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };

            DNV dnVert(DNA IN)
            {
                DNV OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float4 dnFrag(DNV IN) : SV_Target
            {
                return float4(normalize(IN.normalWS) * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
