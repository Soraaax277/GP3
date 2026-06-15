// =============================================================================
//  StylizedSurface_Hidden.shader
//
//  Identical to StylizedSurface visually but with two passes removed:
//    · NO Outline pass    — hidden tiles never show a per-object outline
//    · NO DepthNormals pass — hidden tiles are invisible to CelShadeFilter's
//      screen-space edge detector, so no ghost outlines bleed through the fog
//
//  USE ON:  Unexplored / fog-covered tiles.
//  HexTileReveal.cs swaps to StylizedSurface (with both passes) on reveal.
// =============================================================================

Shader "Custom/URP/StylizedSurface_Hidden"
{
    Properties
    {
        [MainTexture] _BaseMap   ("Albedo Texture", 2D)      = "white" {}
        [MainColor]   _BaseColor ("Color",          Color)   = (1,1,1,1)

        [Header(Cel Shading)]
        [Enum(None,0,Single,1,Steps,2)]
        _CelMode          ("Mode  (None / Single / Steps)", Float) = 1
        _ColorShaded      ("Color Shaded",          Color)   = (0.35,0.40,0.50,1)
        _SelfShadingSize  ("Shadow Size  [Single]", Range(0,1))   = 0.5
        _ShadowEdgeSize   ("Shadow Edge  [Single]", Range(0,0.5)) = 0.05
        _StepCount        ("Step Count   [Steps]",  Range(1,8))   = 3

        [Header(Extra Cel Layer)]
        [Toggle(_EXTRA_CEL)] _EnableExtraCel ("Enable",    Float) = 0
        _ExtraCelColor    ("Color",     Color)   = (0.2,0.22,0.30,1)
        _ExtraCelSize     ("Size",      Range(0,1))   = 0.3
        _ExtraCelEdge     ("Edge",      Range(0,0.5)) = 0.03

        [Header(Specular)]
        [Toggle(_SPECULAR)] _EnableSpecular ("Enable",         Float) = 0
        _SpecColor        ("Specular Color",    Color)   = (1,1,1,1)
        _SpecSize         ("Specular Size",     Range(0,1))   = 0.2
        _SpecEdge         ("Specular Edge",     Range(0,0.5)) = 0.02

        [Header(Rim)]
        [Toggle(_RIM)] _EnableRim ("Enable",          Float) = 0
        _RimColor         ("Rim Color",         Color)   = (0.8,0.9,1.0,1)
        _RimSize          ("Rim Size",          Range(0,1))   = 0.4
        _RimEdge          ("Rim Edge",          Range(0,0.5)) = 0.1
        _RimLightAlign    ("Light Align",       Range(0,1))   = 0.0

        [Header(Height Gradient)]
        [Toggle(_HEIGHT_GRAD)] _EnableHeightGrad ("Enable",   Float) = 0
        _GradientColor    ("Gradient Color",    Color)   = (0.1,0.15,0.25,1)
        _GradientCenter   ("Center Y (world)",  Float)   = 0.0
        _GradientSize     ("Size",              Float)   = 2.0

        [Header(Advanced Lighting)]
        [Range(0,1)] _LightColorContrib ("Light Color Contribution", Float) = 0.0

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        // Outline properties declared to keep material compatible when swapping
        // to StylizedSurface on reveal — values are preserved across the swap.
        [HideInInspector] _OutlineColor     ("", Color) = (0.1,0.1,0.12,1)
        [HideInInspector] _OutlineWidth     ("", Float) = 2.0
        [HideInInspector] _OutlineDepthOffset ("", Float) = 0.0
        [HideInInspector] _EnableOutline    ("", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        // ForwardLit — identical to StylizedSurface
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionWS:TEXCOORD2; float fogFactor:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            float CelBand(float a, float size, float edge)
            {
                return smoothstep(saturate(size-edge), saturate(size+edge), a);
            }

            float EvalCelMode(float NdotL)
            {
                float a = saturate(NdotL);
                if (_CelMode < 0.5) return 1.0;
                if (_CelMode < 1.5) return CelBand(a, 1.0 - _SelfShadingSize, _ShadowEdgeSize);
                return floor(a * _StepCount) / _StepCount;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = p.positionCS; OUT.positionWS = p.positionWS;
                OUT.normalWS = n.normalWS; OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 tex    = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 albedo = tex.rgb * _BaseColor.rgb;
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);
                float4 sc = TransformWorldToShadowCoord(IN.positionWS);
                Light  ml = GetMainLight(sc);
                float3 L  = normalize(ml.direction);
                float NdotL = dot(N, L);
                float atten = ml.shadowAttenuation * ml.distanceAttenuation;
                float lit   = EvalCelMode(NdotL * atten);
                float3 lightTint = lerp(float3(1,1,1), ml.color, _LightColorContrib);
                float3 color = lerp(_ColorShaded.rgb, albedo, lit) * lightTint;
                #ifdef _EXTRA_CEL
                    color = lerp(_ExtraCelColor.rgb, color, CelBand(saturate(NdotL*atten), 1.0-_ExtraCelSize, _ExtraCelEdge));
                #endif
                #ifdef _ADDITIONAL_LIGHTS
                    uint lc = GetAdditionalLightsCount();
                    for (uint i=0u;i<lc;++i) {
                        Light al=GetAdditionalLight(i,IN.positionWS);
                        float aa=al.shadowAttenuation*al.distanceAttenuation;
                        color+=albedo*lerp(float3(1,1,1),al.color,_LightColorContrib)*saturate(EvalCelMode(dot(N,normalize(al.direction))*aa)-( 1.0-lit));
                    }
                #endif
                #ifdef _SPECULAR
                    float3 H=normalize(L+V); float power=max(1.0,(1.0-_SpecSize)*128.0);
                    color+=_SpecColor.rgb*CelBand(pow(saturate(dot(N,H)),power),1.0-_SpecEdge,_SpecEdge*0.5)*atten;
                #endif
                #ifdef _RIM
                    float rim=1.0-saturate(dot(N,V));
                    color+=_RimColor.rgb*CelBand(rim,1.0-_RimSize,_RimEdge)*lerp(1.0,smoothstep(0.0,0.5,NdotL),_RimLightAlign);
                #endif
                #ifdef _HEIGHT_GRAD
                    float gradT=1.0-saturate((IN.positionWS.y-_GradientCenter)/max(0.001,_GradientSize));
                    color=lerp(color,_GradientColor.rgb,gradT*_GradientColor.a);
                #endif
                color += albedo * SampleSH(N) * 0.1;
                color += _EmissionColor.rgb;
                color  = MixFog(color, IN.fogFactor);
                return float4(color, tex.a * _BaseColor.a);
            }
            ENDHLSL
        }

        // ShadowCaster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma target 3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColor; float4 _ColorShaded; float4 _ExtraCelColor;
                float4 _SpecColor; float4 _RimColor; float4 _GradientColor; float4 _EmissionColor; float4 _OutlineColor;
                float _CelMode; float _SelfShadingSize; float _ShadowEdgeSize; float _StepCount;
                float _EnableExtraCel; float _ExtraCelSize; float _ExtraCelEdge;
                float _EnableSpecular; float _SpecSize; float _SpecEdge;
                float _EnableRim; float _RimSize; float _RimEdge; float _RimLightAlign;
                float _EnableHeightGrad; float _GradientCenter; float _GradientSize;
                float _LightColorContrib; float _EnableOutline; float _OutlineWidth; float _OutlineDepthOffset;
            CBUFFER_END
            struct SA { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct SV { float4 positionCS:SV_POSITION; };
            SV shadowVert(SA IN) {
                SV OUT; float3 pw=TransformObjectToWorld(IN.positionOS.xyz); float3 nw=TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    OUT.positionCS=TransformWorldToHClip(ApplyShadowBias(pw,nw,normalize(_LightPosition-pw)));
                #else
                    OUT.positionCS=TransformWorldToHClip(ApplyShadowBias(pw,nw,_LightDirection));
                #endif
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z=min(OUT.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z=max(OUT.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return OUT;
            }
            float4 shadowFrag(SV IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // NO Outline pass  — hidden tiles have no per-object border
        // NO DepthNormals  — hidden tiles invisible to CelShadeFilter
    }

    FallBack "Universal Render Pipeline/Lit"
}
