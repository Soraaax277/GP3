// ─────────────────────────────────────────────────────────────────────────────
//  ToonLit.shader  —  Per-material cel shade for URP
//
//  HOW IT WORKS (plain English):
//    1. For each pixel we calculate the classic diffuse dot product:
//         NdotL = dot(surface normal, light direction)
//       This gives a 0-1 float: 1 = fully lit, 0 = fully in shadow.
//
//    2. Instead of using that float directly (smooth shading), we snap it
//       into N discrete bands using floor(). That's your cel shade.
//
//    3. We add an optional rim light — the bright edge you see on curved
//       objects facing away from the camera. Common in anime/toon rendering.
//
//    4. Additional URP lights (point lights, spot lights) are accumulated
//       on top using the same stepped model.
//
//  RENDER PIPELINE:  URP 14+ (Unity 2022.3 LTS and above)
// ─────────────────────────────────────────────────────────────────────────────

Shader "Custom/URP/ToonLit"
{
    Properties
    {
        // ── Base ──────────────────────────────────────────────────────────────
        [MainTexture] _BaseMap   ("Albedo (Texture)", 2D)       = "white" {}
        [MainColor]   _BaseColor ("Albedo (Color)",   Color)    = (1,1,1,1)

        // ── Toon Shading ──────────────────────────────────────────────────────
        [Header(Toon Shading)]
        _ShadowSteps      ("Shadow Steps",          Range(1, 8))  = 3
        // The cutoff below which a pixel is considered fully in shadow.
        // Raise it to push the shadow edge further into the lit area.
        _ShadowThreshold  ("Shadow Threshold",      Range(0, 1))  = 0.1

        // How much the shadow darkens the albedo. 0 = no shadow, 1 = fully black.
        _ShadowStrength   ("Shadow Strength",       Range(0, 1))  = 0.6

        // A tiny feather on the shadow edge. 0 = razor hard, 0.05 = just soft
        // enough to avoid pixel-crawl on low-poly surfaces (like the reference).
        _ShadowSoftness   ("Shadow Edge Softness",  Range(0, 0.2)) = 0.02

        // ── Rim Light ─────────────────────────────────────────────────────────
        [Header(Rim Light)]
        [Toggle] _UseRim  ("Enable Rim Light",      Float)        = 1
        _RimColor         ("Rim Color",             Color)        = (1,1,1,1)
        _RimThreshold     ("Rim Threshold",         Range(0, 1))  = 0.5
        _RimStrength      ("Rim Strength",          Range(0, 1))  = 0.4
        _RimSoftness      ("Rim Softness",          Range(0, 0.2)) = 0.02

        // ── Specular Highlight ────────────────────────────────────────────────
        [Header(Specular)]
        [Toggle] _UseSpec ("Enable Specular",       Float)        = 0
        _SpecColor        ("Specular Color",        Color)        = (1,1,1,1)
        _SpecThreshold    ("Specular Threshold",    Range(0, 1))  = 0.8
        _SpecSoftness     ("Specular Softness",     Range(0, 0.1)) = 0.01
        _Shininess        ("Shininess",             Range(1, 512)) = 64

        // ── Emission ──────────────────────────────────────────────────────────
        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color",    Color)        = (0,0,0,1)

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

        // ─────────────────────────────────────────────────────────────────────
        //  PASS 1 — ForwardLit
        //  Handles the main directional light + all additional lights.
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            // URP keywords — these enable shadow and light loop support
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Uniforms ─────────────────────────────────────────────────────

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _RimColor;
                float4 _SpecColor;

                float  _ShadowSteps;
                float  _ShadowThreshold;
                float  _ShadowStrength;
                float  _ShadowSoftness;

                float  _UseRim;
                float  _RimThreshold;
                float  _RimStrength;
                float  _RimSoftness;

                float  _UseSpec;
                float  _SpecThreshold;
                float  _SpecSoftness;
                float  _Shininess;
            CBUFFER_END

            // ── Structs ──────────────────────────────────────────────────────

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Toon Lighting Helper ─────────────────────────────────────────
            //
            //  Given a raw NdotL (0-1), this function:
            //    1. Snaps it into _ShadowSteps discrete bands
            //    2. Applies the shadow strength so dark bands darken the albedo
            //
            //  Returns a 0-1 multiplier you apply to the surface color.
            //
            float ToonDiffuse(float NdotL)
            {
                // Clamp so we never go negative
                NdotL = saturate(NdotL);

                // Snap into bands
                //   e.g. steps=3: NdotL 0.0-0.33 → band 0, 0.33-0.66 → band 1, etc.
                float banded = floor(NdotL * _ShadowSteps) / _ShadowSteps;

                // Smooth the very edge of the shadow threshold slightly to avoid
                // crawling pixels on low-poly models — this is the "soft cel" look
                // you see in the reference. At _ShadowSoftness=0 it's razor hard.
                float edge = smoothstep(
                    _ShadowThreshold - _ShadowSoftness,
                    _ShadowThreshold + _ShadowSoftness,
                    banded
                );

                // Lerp between shadow strength and full brightness
                return lerp(1.0 - _ShadowStrength, 1.0, edge);
            }

            // ── Vertex ───────────────────────────────────────────────────────

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

            // ── Fragment ─────────────────────────────────────────────────────

            float4 frag(Varyings IN) : SV_Target
            {
                // ── 1. Base color ─────────────────────────────────────────────
                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 albedo   = texColor.rgb * _BaseColor.rgb;

                // ── 2. Setup vectors ──────────────────────────────────────────
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);

                // ── 3. Main directional light ─────────────────────────────────
                float4  shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light   mainLight   = GetMainLight(shadowCoord);

                float3  L     = normalize(mainLight.direction);
                float   NdotL = dot(N, L);

                // Shadow map attenuation (0=shadowed, 1=lit) already includes
                // cascade and distance fade from URP. We multiply it into NdotL
                // so occluded geometry goes dark regardless of light angle.
                float   atten   = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float   diffuse = ToonDiffuse(NdotL * atten);

                float3  color   = albedo * mainLight.color * diffuse;

                // ── 4. Specular (optional) ────────────────────────────────────
                //  Uses Blinn-Phong half-vector so it works with multiple lights.
                //  Snapped to a single hard highlight (no bands needed here).
                if (_UseSpec > 0.5)
                {
                    float3 H       = normalize(L + V);
                    float  NdotH   = saturate(dot(N, H));
                    float  spec    = pow(NdotH, _Shininess);

                    // Step to a hard highlight edge
                    float  specMask = smoothstep(
                        _SpecThreshold - _SpecSoftness,
                        _SpecThreshold + _SpecSoftness,
                        spec
                    );
                    color += _SpecColor.rgb * specMask * atten;
                }

                // ── 5. Additional lights (point/spot) ─────────────────────────
                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light   addLight    = GetAdditionalLight(i, IN.positionWS);
                    float3  addL        = normalize(addLight.direction);
                    float   addNdotL    = dot(N, addL);
                    float   addAtten    = addLight.shadowAttenuation * addLight.distanceAttenuation;
                    float   addDiffuse  = ToonDiffuse(addNdotL * addAtten);

                    // Additive, but only the lit contribution so dark sides stay dark
                    color += albedo * addLight.color * (addDiffuse - (1.0 - _ShadowStrength));
                }
                #endif

                // ── 6. Rim light ──────────────────────────────────────────────
                //  NdotV near 0 means the surface is edge-on to the camera — that's
                //  where the rim highlight appears. We invert and step it.
                if (_UseRim > 0.5)
                {
                    float  NdotV    = saturate(dot(N, V));
                    float  rim      = 1.0 - NdotV;
                    float  rimMask  = smoothstep(
                        _RimThreshold - _RimSoftness,
                        _RimThreshold + _RimSoftness,
                        rim
                    );
                    // Only show rim on lit side (multiply by a softened NdotL)
                    float  litMask  = smoothstep(0.0, 0.3, NdotL);
                    color += _RimColor.rgb * rimMask * litMask * _RimStrength;
                }

                // ── 7. Emission ───────────────────────────────────────────────
                color += _EmissionColor.rgb;

                // ── 8. Ambient / GI ───────────────────────────────────────────
                //  SampleSH gives you Unity's baked spherical harmonics (skybox
                //  ambient, light probes). Important — without this, shadowed areas
                //  go pure black instead of picking up ambient bounce.
                float3 ambient = SampleSH(N);
                color += albedo * ambient * (1.0 - _ShadowStrength * 0.5);

                // ── 9. Fog ────────────────────────────────────────────────────
                color = MixFog(color, IN.fogFactor);

                return float4(color, texColor.a * _BaseColor.a);
            }

            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PASS 2 — ShadowCaster
        //  Required so this object casts shadows onto other objects.
        //  This is boilerplate — you do not need to modify it.
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #pragma target   3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVaryings   { float4 positionCS : SV_POSITION; };

            float4 GetShadowPositionHClip(ShadowAttributes IN)
            {
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, normalize(_LightPosition - posWS)));
                #else
                    float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, _LightDirection));
                #endif

                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return posCS;
            }

            ShadowVaryings shadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                OUT.positionCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            float4 shadowFrag(ShadowVaryings IN) : SV_Target { return 0; }

            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PASS 3 — DepthNormals
        //  Required so your existing CelShadeFeature (outline pass) can read
        //  per-object normals from _CameraNormalsTexture. Without this pass,
        //  objects using this shader will have no outline from your feature.
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   dnVert
            #pragma fragment dnFrag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _RimColor;
                float4 _SpecColor;
                float  _ShadowSteps;
                float  _ShadowThreshold;
                float  _ShadowStrength;
                float  _ShadowSoftness;
                float  _UseRim;
                float  _RimThreshold;
                float  _RimStrength;
                float  _RimSoftness;
                float  _UseSpec;
                float  _SpecThreshold;
                float  _SpecSoftness;
                float  _Shininess;
            CBUFFER_END

            struct DNAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct DNVaryings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVaryings dnVert(DNAttributes IN)
            {
                DNVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float4 dnFrag(DNVaryings IN) : SV_Target
            {
                return float4(normalize(IN.normalWS) * 0.5 + 0.5, 0);
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
