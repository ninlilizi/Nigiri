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
				//float3 normal			: TEXCOORD1;
				//float3 worldDirection	: TEXCOORD2;
			};

			float4x4 clipToWorld;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				//float4 clip = float4(o.vertex.xy, 0.0, 1.0);
				//o.worldDirection = mul(clipToWorld, clip) - _WorldSpaceCameraPos;

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
			sampler2D _CameraGBufferTexture3;
			sampler2D _CameraDepthTexture;
			sampler2D _CameraDepthNormalsTexture;

			sampler2D GI;
			sampler2D albedoMap;
			sampler2D flux;
			sampler2D rsmWsPosMap;
			sampler2D rsmWsNorMap;
			sampler2D RSMFlux;
			sampler2D normalMap;
			sampler2D RSMPosition;

			uint RGBAtoUINT(float4 color)
			{
				//uint4 bitShifts = uint4(24, 16, 8, 0);
				//uint4 colorAsBytes = uint4(color * 255.0f) << bitShifts;

				uint4 kEncodeMul = uint4(16777216, 65536, 256, 1);
				uint4 colorAsBytes = round(color * 255.0f);

				return dot(colorAsBytes, kEncodeMul);
			}

			float4 UINTtoRGBA(uint value)
			{
				uint4 bitMask = uint4(0xff000000, 0x00ff0000, 0x0000ff00, 0x000000ff);
				uint4 bitShifts = uint4(24, 16, 8, 0);

				uint4 color = (uint4)value & bitMask;
				color >>= bitShifts;

				return color / 255.0f;
			}

			float2 rand(float2 coord)
			{
				float noiseX = saturate(frac(sin(dot(coord, float2(12.9898, 78.223))) * 43758.5453));
				float noiseY = saturate(frac(sin(dot(coord, float2(12.9898, 78.223)*2.0)) * 43758.5453));

				return float2(noiseX, noiseY);
			}

			// http://ericpolman.com/2016/04/13/reflective-shadow-maps-part-2-the-implementation/
			/*(float3 DoReflectiveShadowMapping(float3 P, bool divideByW, float3 N, float4 uv, out float3 vplPositionWS, out float3 vplNormalWS)
			{
				float rsmRMax = 0.07;
				uint rsmSampleCount = 8;
				float rsmIntensity = 1;

				float4 textureSpacePosition = mul(_WorldSpaceLightPos0.xyz, float4(P, 1.0));
				if (divideByW) textureSpacePosition.xyz /= textureSpacePosition.w;

				float3 indirectIllumination = float3(0, 0, 0);
				float rMax = rsmRMax;

				for (uint i = 0; i < rsmSampleCount; ++i)
				{
					float2 rnd = rand(uv.xy);
					if (i == 0) rnd = tex2D(_NoiseMap0, uv.xy).xyz;
					else if (i == 1) rnd = tex2D(_NoiseMap1, uv).xyz;
					else if (i == 2) rnd = tex2D(_NoiseMap2, uv).xyz;
					else if (i == 3) rnd = tex2D(_NoiseMap3, uv).xyz;
					else if (i == 4) rnd = tex2D(_NoiseMap4, uv).xyz;
					else if (i == 5) rnd = tex2D(_NoiseMap5, uv).xyz;
					else if (i == 6) rnd = tex2D(_NoiseMap6, uv).xyz;
					else if (i == 7) rnd = tex2D(_NoiseMap7, uv).xyz;
			

					float2 coords = textureSpacePosition.xy + rMax * rnd;

					vplPositionWS = tex2D(_CameraDepthTexture, coords.xy).xyz;
					vplNormalWS = tex2D(_CameraGBufferTexture2, coords.xy).xyz;
					float3 flux = RSMFluxLight(coords, P, N, CreateLight(interpolator));

					float3 result = flux * ((max(0, dot(vplNormalWS, P - vplPositionWS)) * (max(0, dot(N, vplPositionWS - P)))) / pow(length(P - vplPositionWS), 4));

					result *= rnd.x * rnd.x;
					indirectIllumination += result;
				}

				return saturate(indirectIllumination * rsmIntensity);
			}*/


			fixed4 frag (v2f i) : SV_Target
			{
				float4 col = tex2D(_MainTex, i.uv);
				// just invert the colors
				//col.rgb = 1 - col.rgb;


				half4 albedoMix = tex2D(albedoMap, i.uv);
				half4 RSMMix = tex2D(RSMFlux, i.uv);
				half4 GIMix = tex2D(GI, i.uv);
				//Average HSV values independantly for prettier result
				/*half4 RSMHSV = float4(rgb2hsv(RSM).rgb, 0);
				half4 colHSV = float4(rgb2hsv(col), 0);
				col.rgb *= RSM.rgb;
				colHSV.rg = float2(rgb2hsv(col).r, lerp(RSMHSV.g, colHSV.g, 0.5));
				//col = float4(hsv2rgb(colHSV).rgb, 0);
				col.rgb = RSM.rgb;*/

				//col = RSMMix * col;
				col = tex2D(RSMFlux, i.uv);;

				return col;
			}
			ENDCG
		}
	}
}
