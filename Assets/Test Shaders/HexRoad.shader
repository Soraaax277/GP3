Shader "Custom/HexRoad"
{
    Properties
    {
        // Surface colours
        _AsphaltColor    ("Asphalt Color",          Color)        = (0.08, 0.08, 0.08, 1)
        _SideColor       ("Side Color",             Color)        = (0.25, 0.22, 0.18, 1)

        // Border
        _BorderColor     ("Border Color",           Color)        = (0.92, 0.92, 0.92, 1)
        _BorderWidth     ("Border Width",           Range(0.0001, 0.3)) = 0.005

        // Hex geometry — tweak to match your prefab's object-space inradius
        _HexInradius     ("Hex Inradius",           Float)        = 0.85

        // Top-face detection
        _TopThreshold    ("Top Face Threshold",     Range(0.1, 1.0)) = 0.85

        // Set per-tile from GridManager.UpdateRoadBorders() via MaterialPropertyBlock.
        // IMPORTANT: _EdgeMask is declared OUTSIDE UnityPerMaterial below so the SRP
        // Batcher does not own it — this is what allows MaterialPropertyBlock to
        // override it per-renderer. Do NOT move it back into the CBUFFER.
        _EdgeMask        ("Edge Mask (auto)",       Float)        = 0.0
        _TileCenterWS    ("Tile Center WS (auto)",  Vector)       = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ── Forward Lit ───────────────────────────────────────────────
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

            // ── Per-material properties (SRP Batcher constant buffer) ──────────
            // Only stable, shared properties belong here.
            // Per-renderer values set via MaterialPropertyBlock must NOT be here.
            CBUFFER_START(UnityPerMaterial)
                half4  _AsphaltColor;
                half4  _SideColor;
                half4  _BorderColor;
                float  _BorderWidth;
                half   _HexInradius;
                half   _TopThreshold;
            CBUFFER_END

            // ── Per-renderer properties (set via MaterialPropertyBlock) ───────
            // Declared at global scope — NOT inside CBUFFER_START(UnityPerMaterial).
            // Properties inside that CBUFFER are owned by the SRP Batcher and
            // MaterialPropertyBlock writes to them are silently dropped.
            float  _EdgeMask;      // 6-bit mask: bit i=1 → draw border on edge i
            float4 _TileCenterWS;  // world-space XZ centre of this tile

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
            };

            // World-space XZ outward normals for each of the 6 hex edges.
            // Derived from GridManager.HexToWorld (pointy-top, offset coords):
            //   world.x = 2 * hexSize * (dq + dr * 0.5)
            //   world.z = sqrt(3) * hexSize * dr
            // where dq = CubeDirections[i].x, dr = CubeDirections[i].z, normalised.
            //
            // CubeDirections: (1,-1,0) (1,0,-1) (0,1,-1) (-1,1,0) (-1,0,1) (0,-1,1)
            static const float2 EdgeNormals[6] =
            {
                float2( 1.000,  0.000),  // bit 0 — dir( 1,-1, 0) dq= 1 dr= 0
                float2( 0.500, -0.866),  // bit 1 — dir( 1, 0,-1) dq= 1 dr=-1
                float2(-0.500, -0.866),  // bit 2 — dir( 0, 1,-1) dq= 0 dr=-1
                float2(-1.000,  0.000),  // bit 3 — dir(-1, 1, 0) dq=-1 dr= 0
                float2(-0.500,  0.866),  // bit 4 — dir(-1, 0, 1) dq=-1 dr= 1
                float2( 0.500,  0.866),  // bit 5 — dir( 0,-1, 1) dq= 0 dr= 1
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS  = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.normalWS    = vni.normalWS;
                OUT.shadowCoord = GetShadowCoord(vpi);
                OUT.fogFactor   = ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 nWS   = normalize(IN.normalWS);
                float  upDot = dot(nWS, float3(0, 1, 0));
                half   isTop = step(_TopThreshold, upDot);

                // ── Border: check which edges need a white line ───────
                // Offset from this tile's world-space centre — set per-tile by
                // GridManager.UpdateRoadBorders() via MaterialPropertyBlock.
                float2 localXZ = float2(
                    IN.positionWS.x - _TileCenterWS.x,
                    IN.positionWS.z - _TileCenterWS.z);

                int  edgeMask = (int)_EdgeMask;
                half isBorder = 0;

                [unroll]
                for (int i = 0; i < 6; i++)
                {
                    float proj     = dot(localXZ, EdgeNormals[i]);
                    bool  nearEdge = proj > (_HexInradius - _BorderWidth);
                    bool  bitSet   = (edgeMask & (1 << i)) != 0;
                    if (nearEdge && bitSet) isBorder = 1;
                }

                // Borders only visible on the top face
                isBorder *= isTop;

                // ── Colour composition ────────────────────────────────
                half3 col = lerp(_SideColor.rgb, _AsphaltColor.rgb, isTop);
                col       = lerp(col, _BorderColor.rgb, isBorder);

                // ── Flat / stepped diffuse (low-poly look) ────────────
                // Three discrete brightness steps instead of smooth shading
                Light mainLight = GetMainLight(IN.shadowCoord);
                float NdotL     = dot(nWS, mainLight.direction);
                float stepped   = NdotL < 0.0  ? 0.25 :
                                  NdotL < 0.45 ? 0.60 : 1.00;
                // Tiles do not receive shadows from buildings — the dense city
                // building clusters cast overlapping shadows that create a large
                // dark blob over the city center. The game board (tiles) are lit
                // purely by the stepped directional light, no shadow attenuation.
                half  shadow    = 1.0h;

                half3 ambient  = SampleSH(nWS) * col * 0.35;
                half3 diffuse  = mainLight.color * col * stepped * shadow;

                half3 finalCol = MixFog(ambient + diffuse, IN.fogFactor);
                return half4(finalCol, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster ─────────────────────────────────────────────
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

            // Only the properties actually used by shadow geometry belong here.
            // _EdgeMask and the former _TileCenterWS are not needed for shadow casting.
            CBUFFER_START(UnityPerMaterial)
                half4  _AsphaltColor; half4 _SideColor; half4 _BorderColor;
                float  _BorderWidth;  half  _HexInradius; half _TopThreshold;
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

        // ── Depth Only ────────────────────────────────────────────────
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

            // Only the properties actually used by depth geometry belong here.
            CBUFFER_START(UnityPerMaterial)
                half4  _AsphaltColor; half4 _SideColor; half4 _BorderColor;
                float  _BorderWidth;  half  _HexInradius; half _TopThreshold;
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
