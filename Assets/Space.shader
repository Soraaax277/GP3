Shader "Skybox/URP_LowPolySpace_Twinkle"
{
    Properties
    {
        _SpaceColor ("Space Base Color", Color) = (0.05, 0.04, 0.1, 1)
        _StarColor ("Star Color", Color) = (1, 1, 1, 1)
        _StarDensity ("Grid Density (More = smaller/more stars)", Range(10, 300)) = 150
        _StarThreshold ("Star Probability", Range(0.0, 1.0)) = 0.8
        _StarSize ("Star Size", Range(0.01, 0.5)) = 0.1
        _TwinkleSpeed ("Twinkle Speed", Range(0.1, 10)) = 3.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _SpaceColor;
                half4 _StarColor;
                float _StarDensity;
                float _StarThreshold;
                float _StarSize;
                float _TwinkleSpeed;
            CBUFFER_END

            // A simple 3D hash function to generate random values based on position
            float hash31(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            Varyings vert (Attributes input)
            {
                Varyings output;
                // Standard URP transformation
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Use the object space position as the view direction for the skybox projection
                output.viewDir = input.positionOS.xyz;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // Normalize to ensure consistent mapping across the skybox cube/sphere
                float3 dir = normalize(input.viewDir);

                // Chop the skybox into a 3D grid
                float3 grid = floor(dir * _StarDensity);
                // Get the local position inside each grid cell (-0.5 to 0.5)
                float3 localPos = frac(dir * _StarDensity) - 0.5;

                // Get a pseudo-random value for this specific grid cell
                float rand = hash31(grid);

                // Determine if a star actually spawns in this grid cell based on threshold
                float hasStar = step(_StarThreshold, rand);

                // Create a sharp, un-feathered shape for the star to fit the low-poly look
                // We use step() to avoid soft gradients.
                float starShape = step(length(localPos), _StarSize * rand) * hasStar;

                // Create the twinkle effect using time and offset by the cell's random value
                float twinkle = sin(_Time.y * _TwinkleSpeed + (rand * 100.0)) * 0.5 + 0.5;

                // Multiply it all together
                half3 finalStars = starShape * _StarColor.rgb * twinkle;

                // Combine the void of space with the stars
                return half4(_SpaceColor.rgb + finalStars, 1.0);
            }
            ENDHLSL
        }
    }
}