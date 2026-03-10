Shader "Custom/StylizedHexLand"
{
    Properties
    {
        // ── Required by HexTile.cs for tinting/highlighting ───────────────
        _Color ("Tile Tint Color", Color) = (1,1,1,1)

        [Header(Terrain Colors)]
        _GrassColorA ("Grass Color A (Light)", Color) = (0.35, 0.7, 0.3, 1)
        _GrassColorB ("Grass Color B (Dark)", Color) = (0.2, 0.5, 0.2, 1)
        _DirtColor ("Side Dirt Color", Color) = (0.4, 0.25, 0.15, 1)
        
        [Header(Color Variation)]
        _ColorVariationScale ("Color Variation Scale", Float) = 0.5
        
        [Header(Blending)]
        _BlendSharpness ("Edge Sharpness", Range(1, 50)) = 15.0
        _GrassOffset ("Grass Coverage Offset", Range(-1, 1)) = 0.1
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry" 
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float  fogFactor    : TEXCOORD1;
                float2 objPosXZ     : TEXCOORD2; // Store the object's origin center
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _GrassColorA;
                float4 _GrassColorB;
                float4 _DirtColor;
                float  _ColorVariationScale;
                float  _BlendSharpness;
                float  _GrassOffset;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                
                // Get the world position of the center of this specific hex tile (0,0,0 in local space)
                float3 centerPosWS = TransformObjectToWorld(float3(0, 0, 0));
                OUT.objPosXZ = centerPosWS.xz;

                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);

                // ── 1. Calculate Per-Tile Color Variation ──────────────────
                // Use sine and cosine waves mapped to the object's center position
                // to create large organic "patches" of color across the grid.
                float noise1 = sin(IN.objPosXZ.x * _ColorVariationScale + IN.objPosXZ.y * _ColorVariationScale * 0.5);
                float noise2 = cos(IN.objPosXZ.y * _ColorVariationScale - IN.objPosXZ.x * _ColorVariationScale * 0.3);
                
                // Combine the waves and remap from roughly (-2 to 2) down to (0 to 1)
                float variation = saturate((noise1 + noise2) * 0.25 + 0.5); 
                
                // Blend between our two grass colors based on map location
                float3 currentGrassColor = lerp(_GrassColorA.rgb, _GrassColorB.rgb, variation);

                // ── 2. Blend Grass and Dirt ────────────────────────────────
                float upFactor = normalWS.y + _GrassOffset;
                float grassMask = saturate(upFactor * _BlendSharpness);
                float3 finalColor = lerp(_DirtColor.rgb, currentGrassColor, grassMask);

                // ── 3. Apply Lighting and Tints ────────────────────────────
                finalColor *= _Color.rgb; // HexTile.cs interactions

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                
                float3 ambient = float3(0.2, 0.2, 0.3) * finalColor; 
                finalColor = (finalColor * mainLight.color * NdotL) + ambient;

                finalColor = MixFog(finalColor, IN.fogFactor);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}