Shader "Custom/URP/CelShadeFilter"
{
    Properties { }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            TEXTURE2D_X(_CameraNormalsTexture);
            SAMPLER(sampler_CameraNormalsTexture);

            float  _PosterizeSteps;
            float  _PosterizeStrength;
            float  _OutlineThickness;
            float  _OutlineDepthThreshold;
            float  _OutlineNormalThreshold;
            float  _OutlineDepthScale;
            float  _OutlineMaxDepth;      // NEW: ignore edges beyond this scene depth
            float  _OutlineMaxDepthDelta; // NEW: ignore edges where depth gap is too large
            float4 _OutlineColor;
            float  _SaturationBoost;

            float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            float Posterize(float v, float steps)
            {
                return floor(v * steps + 0.5) / steps;
            }

            // Only posterize colourful pixels - flat/grey/dark tiles are fully skipped
            float3 PosterizeColor(float3 col, float steps, float strength)
            {
                float3 posterized;
                posterized.r = Posterize(col.r, steps);
                posterized.g = Posterize(col.g, steps);
                posterized.b = Posterize(col.b, steps);

                float maxC   = max(col.r, max(col.g, col.b));
                float minC   = min(col.r, min(col.g, col.b));
                float chroma = maxC - minC;
                float luma   = Luma(col);

                // Gate 1: must be colourful enough (kills grey/blue water and fog)
                float chromaMask = smoothstep(0.08, 0.25, chroma);

                // Gate 2: must not be too dark (kills the grey-to-black snapping on hex tiles)
                // Pixels below luma 0.25 are left completely untouched
                float lumaMask = smoothstep(0.15, 0.35, luma);

                return lerp(col, posterized, strength * chromaMask * lumaMask);
            }

            float3 BoostSaturation(float3 col, float amount)
            {
                float luma = Luma(col);
                return lerp(float3(luma, luma, luma), col, 1.0 + amount);
            }

            float SampleDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
            }

            float3 SampleNormal(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv).rgb * 2.0 - 1.0;
            }

            float DetectEdge(float2 uv, float thickness)
            {
                float2 tx = _BlitTexture_TexelSize.xy * thickness;

                float dC  = SampleDepth(uv);

                // Skip outlines on pixels that are too far away (water, sky, far terrain)
                // This kills the island-silhouette halo entirely
                if (dC < _OutlineMaxDepth) return 0.0;

                float d00 = SampleDepth(uv + float2(-tx.x,  tx.y));
                float d10 = SampleDepth(uv + float2(    0,  tx.y));
                float d20 = SampleDepth(uv + float2( tx.x,  tx.y));
                float d01 = SampleDepth(uv + float2(-tx.x,     0));
                float d21 = SampleDepth(uv + float2( tx.x,     0));
                float d02 = SampleDepth(uv + float2(-tx.x, -tx.y));
                float d12 = SampleDepth(uv + float2(    0, -tx.y));
                float d22 = SampleDepth(uv + float2( tx.x, -tx.y));

                // If ANY neighbour is far away (water/sky), this is a silhouette edge - skip it
                float minNeighbour = min(min(min(d00, d10), min(d20, d01)),
                                        min(min(d21, d02), min(d12, d22)));
                if ((dC - minNeighbour) > _OutlineMaxDepthDelta) return 0.0;

                // Sobel on depth
                float sobelX = -d00 - 2.0*d01 - d02 + d20 + 2.0*d21 + d22;
                float sobelY = -d00 - 2.0*d10 - d20 + d02 + 2.0*d12 + d22;
                float depthEdge   = sqrt(sobelX*sobelX + sobelY*sobelY);
                float depthResult = step(_OutlineDepthThreshold * 0.0001, depthEdge * _OutlineDepthScale);

                // Sobel on normals
                float3 n00 = SampleNormal(uv + float2(-tx.x,  tx.y));
                float3 n10 = SampleNormal(uv + float2(    0,  tx.y));
                float3 n20 = SampleNormal(uv + float2( tx.x,  tx.y));
                float3 n01 = SampleNormal(uv + float2(-tx.x,     0));
                float3 n21 = SampleNormal(uv + float2( tx.x,     0));
                float3 n02 = SampleNormal(uv + float2(-tx.x, -tx.y));
                float3 n12 = SampleNormal(uv + float2(    0, -tx.y));
                float3 n22 = SampleNormal(uv + float2( tx.x, -tx.y));

                float3 nSobelX   = -n00 - 2.0*n01 - n02 + n20 + 2.0*n21 + n22;
                float3 nSobelY   = -n00 - 2.0*n10 - n20 + n02 + 2.0*n12 + n22;
                float normalEdge = sqrt(dot(nSobelX, nSobelX) + dot(nSobelY, nSobelY));
                float normalResult = step(_OutlineNormalThreshold, normalEdge);

                return saturate(depthResult + normalResult);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv  = IN.texcoord;
                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // 1. Posterize only vivid pixels - water/fog/sky are untouched
                col = PosterizeColor(col, _PosterizeSteps, _PosterizeStrength);

                // 2. Saturation boost
                if (_SaturationBoost > 0.001)
                    col = BoostSaturation(col, _SaturationBoost);

                // 3. Outline - only draws on close geometry, never on silhouettes vs water/sky
                if (_OutlineThickness > 0.001)
                {
                    float edge     = DetectEdge(uv, _OutlineThickness);
                    float hardEdge = step(0.5, edge);
                    col = lerp(col, _OutlineColor.rgb, hardEdge * _OutlineColor.a);
                }

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
