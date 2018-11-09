Shader "Hidden/NKGI_RSM_Post"
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
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex			: POSITION;
				float3 normal			: NORMAL;
				float2 uv				: TEXCOORD0;
			};

			struct v2f
			{
				
				float4 vertex			: SV_POSITION;
				float2 uv				: TEXCOORD0;
				float3 normal			: TEXCOORD1;
				float3 worldDirection	: TEXCOORD2;
			};

			float4x4 clipToWorld;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				float4 clip = float4(o.vertex.xy, 0.0, 1.0);
				o.worldDirection = mul(clipToWorld, clip) - _WorldSpaceCameraPos;

				return o;
			}

			const int StereoEnabled = 0;
			const float4x4 _LeftEyeProjection;
			const float4x4 _LeftEyeToWorld;
			const float4x4 _RightEyeProjection;
			const float4x4 _RightEyeToWorld;
			float4x4 CameraToWorld;
			float4x4 ProjectionMatrixInverse;
			
			sampler2D _MainTex;

			sampler2D _CameraGBufferTexture0;
			sampler2D _CameraGBufferTexture1;
			sampler2D _CameraGBufferTexture2;
			sampler2D _CameraDepthTexture;
			sampler2D _CameraDepthNormalsTexture;

			sampler2D NKGI_RSM;

			float3 rgb2hsv(float3 c)
			{
				float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp(float4(c.bg, k.wz), float4(c.gb, k.xy), step(c.b, c.g));
				float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

				float d = q.x - min(q.w, q.y);
				float e = 1.0e-10;

				return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}

			float3 hsv2rgb(float3 c)
			{
				float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
				float3 p = abs(frac(c.xxx + k.xyz) * 6.0 - k.www);
				return c.z * lerp(k.xxx, saturate(p - k.xxx), c.y);
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				// just invert the colors
				//col.rgb = 1 - col.rgb;



				half4 RSM = tex2D(NKGI_RSM, i.uv.xy);

				//Average HSV values independantly for prettier result
				half4 RSMHSV = float4(rgb2hsv(RSM).rgb, 0);
				half4 colHSV = float4(rgb2hsv(col), 0);
				col.rgb *= RSM.rgb;
				colHSV.rg = float2(rgb2hsv(col).r, lerp(RSMHSV.g, colHSV.g, 0.5));
				col = float4(hsv2rgb(colHSV).rgb, 0);

				return col;
			}
			ENDCG
		}
	}
}
