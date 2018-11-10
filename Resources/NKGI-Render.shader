Shader "Hidden/NKGI-Render"
{
	SubShader
	{
		// No culling or depth
		//Cull Off ZWrite Off ZTest Always

		Pass
		{
			HLSLPROGRAM
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
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			struct PSIn
			{
				float4 pos : SV_POSITION;
				float3 normal : NORMAL;
				float3 tangent : TANGENT;
				float3 bitangent : BITANGENT;
				float2 texcoord : TEXCOORD0;
			};

			// Start of common.hlsl.inc
		#define LPV_DIM 32
		#define LPV_DIMH 16
		#define LPV_CELL_SIZE 4.0

			int3 getGridPos(float3 worldPos)
			{
				return (worldPos / LPV_CELL_SIZE) + int3(LPV_DIMH, LPV_DIMH, LPV_DIMH);
			}
			float3 getGridPosAsFloat(float3 worldPos)
			{
				return (worldPos / LPV_CELL_SIZE) + float3(LPV_DIMH, LPV_DIMH, LPV_DIMH);
			}

			// https://github.com/mafian89/Light-Propagation-Volumes/blob/master/shaders/lightInject.frag and
			// https://github.com/djbozkosz/Light-Propagation-Volumes/blob/master/data/shaders/lpvInjection.cs seem
			// to use the same coefficients, which differ from the RSM paper. Due to completeness of their code, I will stick to their solutions.
			/*Spherical harmonics coefficients – precomputed*/
			#define SH_C0 0.282094792f // 1 / 2sqrt(pi)
			#define SH_C1 0.488602512f // sqrt(3/pi) / 2

			/*Cosine lobe coeff*/
			#define SH_cosLobe_C0 0.886226925f // sqrt(pi)/2
			#define SH_cosLobe_C1 1.02332671f // sqrt(pi/3)
			#define PI 3.1415926f

			float4 dirToCosineLobe(float3 dir) {
				//dir = normalize(dir);
				return float4(SH_cosLobe_C0, -SH_cosLobe_C1 * dir.y, SH_cosLobe_C1 * dir.z, -SH_cosLobe_C1 * dir.x);
			}

			float4 dirToSH(float3 dir) {
				return float4(SH_C0, -SH_C1 * dir.y, SH_C1 * dir.z, -SH_C1 * dir.x);
			}

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			// End of common.hlsl.inc

			sampler3D lpvR;
			sampler3D lpvG;
			sampler3D lpvB;
			sampler2D albedoMap;
			sampler2D normalMap;
			sampler2D Occlusion;
			sampler2D _CameraDepthTexture;


			float4 frag(v2f IN) : SV_Target
			{
			float3 albedo = tex2D(albedoMap, IN.uv).xyz;
			float3 pxPosWS = tex2D(_CameraDepthTexture, IN.uv).xyz;
			float3 pxNorWS = tex2D(normalMap, IN.uv).xyz;
			float3 gridPos = getGridPosAsFloat(pxPosWS);

			// https://github.com/mafian89/Light-Propagation-Volumes/blob/master/shaders/basicShader.frag
			float4 SHintensity = dirToSH(-pxNorWS);
			float3 lpvIntensity = (float3)0;

			float4 lpvRtex = tex3D(lpvR, gridPos / float3(LPV_DIM, LPV_DIM, LPV_DIM));
			float4 lpvGtex = tex3D(lpvG, gridPos / float3(LPV_DIM, LPV_DIM, LPV_DIM));
			float4 lpvBtex = tex3D(lpvB, gridPos / float3(LPV_DIM, LPV_DIM, LPV_DIM));

			lpvIntensity = float3(
			dot(SHintensity, lpvRtex),
			dot(SHintensity, lpvGtex),
			dot(SHintensity, lpvBtex));

			float3 finalLPVRadiance = max(0, lpvIntensity) / PI;

			float occlusion = tex2D(albedoMap, IN.vertex.xy).a;

			float4 result = float4(finalLPVRadiance, 1.0) * occlusion * float4(albedo, 1.0);
			result = float4(finalLPVRadiance, 1.0) * occlusion;
			return result;
			}
					ENDHLSL
		}
	}
}
