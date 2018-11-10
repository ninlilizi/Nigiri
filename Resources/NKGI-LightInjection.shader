Shader "Hidden/NKGI-LightInjection"
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
			#pragma target 5.0
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag
			

			#include "UnityCG.cginc"

			struct appdata
			{
				uint posIndex : SV_VertexID;
			};

			struct v2g
			{
				float4 cellIndex : SV_POSITION;
				float3 normal : WORLD_NORMAL;
				float3 flux : LIGHT_FLUX;
				float3 uv	: TEXCOORD0;
			};

			struct g2f
			{
				float4 screenPos : SV_POSITION;
				float3 normal : WORLD_NORMAL;
				float3 flux : LIGHT_FLUX;
				uint depthIndex : SV_RenderTargetArrayIndex;
			};

			struct v2o {
				float4 redSH : SV_Target0;
				float4 greenSH : SV_Target1;
				float4 blueSH : SV_Target2;
			};

#define LPV_DIM 32
#define LPV_DIMH 16
#define LPV_CELL_SIZE 4.0

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

#define POSWS_BIAS_NORMAL 2.0
#define POSWS_BIAS_LIGHT 1.0

			struct Light
			{
				float3 position;
				float range;
				//————————16 bytes
				float3 direction;
				float spotAngle;
				//————————16 bytes
				float3 color;
				uint type;
			};

			cbuffer b0 : register(b0)
			{

				float4x4 vpMatrix;
				float4x4 RsmToWorldMatrix;
				static Light light;
			};

			int3 getGridPos(float3 worldPos)
			{
				return (worldPos / LPV_CELL_SIZE) + int3(LPV_DIMH, LPV_DIMH, LPV_DIMH);
			}

			struct RsmTexel
			{
				float4 flux;
				float3 normalWS;
				float3 positionWS;
			};

			Texture2D RSMFlux : register(t0);
			Texture2D _CameraDepthTexture : register(t1);
			Texture2D normalMap : register(t2);

			float Luminance(RsmTexel rsmTexel)
			{
				return (rsmTexel.flux.r * 0.299f + rsmTexel.flux.g * 0.587f + rsmTexel.flux.b * 0.114f)
					+ max(0.0f, dot(rsmTexel.normalWS, -light.direction));
			}

			RsmTexel GetRsmTexel(int2 coords)
			{
				RsmTexel tx = (RsmTexel)0;
				tx.flux = float4(RSMFlux.Load(int3(coords, 0)).xyz, 0);
				tx.normalWS = normalMap.Load(int3(coords, 0)).xyz;
				tx.positionWS = _CameraDepthTexture.Load(int3(coords, 0)).xyz + (tx.normalWS * POSWS_BIAS_NORMAL);
				return tx;
			}

#define KERNEL_SIZE 4
#define STEP_SIZE 1

			v2g vert(appdata v)
			{
				v2g o;// = (v2f)0;

				uint2 RSMsize;
				_CameraDepthTexture.GetDimensions(RSMsize.x, RSMsize.y);
				RSMsize /= KERNEL_SIZE;
				int3 rsmCoords = int3(v.posIndex % RSMsize.x, v.posIndex / RSMsize.x, 0);

				// Pick brightest cell in KERNEL_SIZExKERNEL_SIZE grid
				float3 brightestCellIndex = 0;
				float maxLuminance = 0;
				{
					for (uint y = 0; y < KERNEL_SIZE; y += STEP_SIZE)
					{
						for (uint x = 0; x < KERNEL_SIZE; x += STEP_SIZE)
						{
							int2 texIdx = rsmCoords.xy * KERNEL_SIZE + int2(x, y);
							RsmTexel rsmTexel = GetRsmTexel(texIdx);
							float texLum = Luminance(rsmTexel);
							if (texLum > maxLuminance)
							{
								brightestCellIndex = getGridPos(rsmTexel.positionWS);
								maxLuminance = texLum;
							}
						}
					}
				}

				RsmTexel result = (RsmTexel)0;
				float numSamples = 0;
				for (uint y = 0; y < KERNEL_SIZE; y += STEP_SIZE)
				{
					for (uint x = 0; x < KERNEL_SIZE; x += STEP_SIZE)
					{
						int2 texIdx = rsmCoords.xy * KERNEL_SIZE + int2(x, y);
						RsmTexel rsmTexel = GetRsmTexel(texIdx);
						int3 texelIndex = getGridPos(rsmTexel.positionWS);
						float3 deltaGrid = texelIndex - brightestCellIndex;
						if (dot(deltaGrid, deltaGrid) < 10) // If cell proximity is good enough
						{
							// Sample from texel
							result.flux += rsmTexel.flux;
							result.positionWS += rsmTexel.positionWS;
							result.normalWS += rsmTexel.normalWS;
							++numSamples;
						}
					}
				}

				//if (numSamples > 0) // This is always true due to picking a brightestCell, however, not all cells have light
				//{
				result.positionWS /= numSamples;
				result.normalWS /= numSamples;
				result.normalWS = normalize(result.normalWS);
				result.flux /= numSamples;

				//RsmTexel result = GetRsmTexel(rsmCoords.xy);

				o.cellIndex = float4(getGridPos(result.positionWS), 1.0);
				o.normal = result.normalWS;
				o.flux = result.flux.rgb;

				return o;
			}

			[maxvertexcount(1)]
			void geom(point v2g input[1], inout PointStream<g2f> OutputStream) {
				g2f output = (g2f)0;

				output.depthIndex = input[0].cellIndex.z;
				output.screenPos.xy = (float2(input[0].cellIndex.xy) + 0.5) / float2(LPV_DIM, LPV_DIM) * 2.0 - 1.0;
				// invert y direction because y points downwards in the viewport?
				output.screenPos.y = -output.screenPos.y;
				output.screenPos.zw = float2(0, 1);

				output.normal = input[0].normal;
				output.flux = input[0].flux;

				OutputStream.Append(output);
			}
			
			float4 dirToCosineLobe(float3 dir) {
				//dir = normalize(dir);
				return float4(SH_cosLobe_C0, -SH_cosLobe_C1 * dir.y, SH_cosLobe_C1 * dir.z, -SH_cosLobe_C1 * dir.x);
			}

			float4 dirToSH(float3 dir) {
				return float4(SH_C0, -SH_C1 * dir.y, SH_C1 * dir.z, -SH_C1 * dir.x);
			}

			v2o frag (g2f i)
			{
				v2o output;

				const static float surfelWeight = 0.015;
				float4 coeffs = (dirToCosineLobe(i.normal) / PI) * surfelWeight;
				output.redSH = coeffs * i.flux.r;
				output.greenSH = coeffs * i.flux.g;
				output.blueSH = coeffs * i.flux.b;

				return output;
			}
			ENDCG
		}
	}
}
