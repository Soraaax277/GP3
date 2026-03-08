Shader "Custom/SoftFogInstanced"
{
    Properties
    {
        _MainTex ("Fog Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _InvFade ("Soft Factor", Range(0.01, 3.0)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing // Enables GPU Instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Required for Instancing
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 projPos : TEXCOORD1; // Used for depth sampling
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _InvFade;
            sampler2D _CameraDepthTexture; // The Magic: Automatically filled by Unity

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                
                // Compute screen space position for depth comparison
                o.projPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // 1. Soft Particle Logic
                // Pull depth from the camera buffer
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                float partZ = i.projPos.z;
                
                // Calculate fade factor based on distance to scene geometry
                float fade = saturate(_InvFade * (sceneZ - partZ));

                // 2. Final Color
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                col.a *= fade; // Apply the soft fade
                
                return col;
            }
            ENDCG
        }
    }
}