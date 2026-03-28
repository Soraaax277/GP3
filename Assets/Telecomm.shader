Shader "Skybox/URP_PureProcedural_Telecom"
{
    Properties
    {
        [Header(Colors)]
        _BgColor ("Background Void Color", Color) = (0.02, 0.01, 0.05, 1)
        _FreqColor ("Sky Frequencies (Cyan)", Color) = (0.0, 1.0, 0.8, 1)
        _RingColor ("Floor Rings (Magenta)", Color) = (0.8, 0.2, 1.0, 1)
        _PulseColor ("Main Center Pulse (White)", Color) = (1.0, 1.0, 1.0, 1)
        
        [Header(Low Poly Settings)]
        _PolySteps ("Digital Stepping (Lower = Blockier)", Range(2.0, 50.0)) = 15.0
        
        [Header(Speeds and Scales)]
        _GlobalSpeed ("Global Animation Speed", Range(0.1, 5.0)) = 1.0
        _RingDensity ("Floor Ring Density", Range(1.0, 10.0)) = 4.0
        _WaveComplexity ("Sky Wave Complexity", Range(1.0, 10.0)) = 5.0
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
                half4 _BgColor;
                half4 _FreqColor;
                half4 _RingColor;
                half4 _PulseColor;
                float _PolySteps;
                float _GlobalSpeed;
                float _RingDensity;
                float _WaveComplexity;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDir = input.positionOS.xyz;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // Normalize view direction
                float3 dir = normalize(input.viewDir);
                
                // 1. Coordinate Setup
                float theta = atan2(dir.z, dir.x); 
                float phi = dir.y;
                
                // Quantize coordinates
                float steppedTheta = floor(theta * _PolySteps) / _PolySteps;
                float steppedPhi = floor(phi * _PolySteps) / _PolySteps;

                // Split the screen
                float isFloor = step(dir.y, -0.05); 
                float isSky = 1.0 - isFloor;

                // --- 2. FOREGROUND: Pulsing Floor Circles ---
                float2 floorPos = dir.xz / (abs(dir.y) + 0.0001);
                float dist = length(floorPos);
                
                float steppedDist = floor(dist * (_PolySteps * 0.5)) / (_PolySteps * 0.5);
                
                float ringMath = sin(steppedDist * _RingDensity - _Time.y * (_GlobalSpeed * 4.0));
                float rings = step(0.8, ringMath); 
                
                float ringFade = exp(-dist * 0.1);
                half3 finalFloor = rings * _RingColor.rgb * ringFade * isFloor;

                // --- 3. BACKGROUND: Fiber Optic Frequencies ---
                float wave1 = sin(steppedTheta * _WaveComplexity + _Time.y * _GlobalSpeed) * 0.3;
                float line1 = 1.0 - step(0.015, abs(steppedPhi - wave1 - 0.2));
                
                float wave2 = cos(steppedTheta * (_WaveComplexity * 1.5) - _Time.y * (_GlobalSpeed * 1.5)) * 0.2;
                float line2 = 1.0 - step(0.015, abs(steppedPhi - wave2 + 0.2));
                
                half3 finalSky = ((line1 + line2) * _FreqColor.rgb) * isSky;

                // --- 4. MIDDLE: Main Central Signal Pulse ---
                float centerAngle = atan2(dir.x, dir.z); 
                float steppedCenterAngle = floor(centerAngle * _PolySteps) / _PolySteps;
                
                float centerDist = length(float2(steppedCenterAngle, steppedPhi));
                float mainPulseMath = sin(centerDist * 15.0 - _Time.y * (_GlobalSpeed * 6.0));
                
                float mainPulse = step(0.9, mainPulseMath) * smoothstep(0.8, 0.0, centerDist);
                
                // Core beam removed. Just the main pulse remains.
                half3 finalCenterPulse = mainPulse * _PulseColor.rgb;

                // --- 5. COMPOSITION ---
                half3 finalColor = _BgColor.rgb;
                finalColor += finalFloor;
                finalColor += finalSky;
                finalColor += finalCenterPulse;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}