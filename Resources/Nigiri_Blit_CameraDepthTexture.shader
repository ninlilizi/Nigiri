Shader "Hidden/Nigiri_Blit_CameraDepthTexture"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float4 worldPos : TEXCOORD2;
			};

			//float4x4	InverseProjectionMatrix;
			//float4x4	InverseViewMatrix;

			//float worldVolumeBoundary;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				o.worldPos = mul(unity_ObjectToWorld, v.vertex);

				return o;
			}
			
			//sampler2D _MainTex;
			//sampler2D _LastCameraDepthTexture;
			sampler2D _CameraDepthTexture;
			//sampler2D orthoDepth;
			//uniform RWStructuredBuffer<float4> positionBuffer : register(u6);


			fixed4 frag (v2f i) : SV_Target
			{
				/*half3 color = tex2D(_MainTex, i.uv).rgb;

				// read low res depth and reconstruct world position
				float depth = SAMPLE_DEPTH_TEXTURE(orthoDepth, i.uv);

				//linearise depth		
				float lindepth = Linear01Depth(depth);

				//get view and then world positions		
				float4 viewPos = float4(i.cameraRay.xyz * lindepth, 1.0f);
				float3 worldPos = mul(InverseViewMatrix, viewPos).xyz;
				
				return float4(worldPos, lindepth);*/

				return Linear01Depth(tex2D(_CameraDepthTexture, i.uv));

			}
			ENDCG
		}
	}
}
