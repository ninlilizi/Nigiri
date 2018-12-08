Shader "Hidden/Nigiri_Tracing"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
		SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		CGINCLUDE

		#include "UnityCG.cginc"

		#define PI 3.1415926f

		struct colorStruct
		{
			float4 value;
		};

		uniform texture3D				voxelGrid1;
		uniform texture3D				voxelGrid1A;
		uniform texture3D				voxelGrid2;
		uniform texture3D				voxelGrid2A;
		uniform texture3D				voxelGrid3;
		uniform texture3D				voxelGrid3A;
		uniform texture3D				voxelGrid4;
		uniform texture3D				voxelGrid4A;
		uniform texture3D				voxelGrid5;
		uniform texture3D				voxelGrid5A;
		uniform sampler3D				voxelGridCascade1;
		uniform sampler3D				voxelGridCascade2;	

		uniform texture2D 				_MainTex;
		uniform texture2D				_CameraDepthTexture;
		uniform texture2D				_CameraDepthNormalsTexture;
		uniform texture2D				_CameraGBufferTexture0;
		uniform texture2D				_CameraGBufferTexture1;
		uniform texture2D				_CameraGBufferTexture2;
		uniform SamplerState			my_point_clamp_sampler;
		uniform SamplerState			my_linear_clamp_sampler;

		//uniform sampler2D				depthTexture;

		half4							_CameraDepthTexture_ST;
		half4							_CameraDepthNormalsTexture_ST;
		half4							_CameraGBufferTexture0_ST;
		half4							_CameraGBufferTexture1_ST;
		half4							_CameraGBufferTexture2_ST;

		uniform float4x4				InverseProjectionMatrix;
		uniform float4x4				InverseViewMatrix;

		uniform float4					_MainTex_TexelSize;

		uniform float					worldVolumeBoundary;
		uniform float					indirectLightingStrength;
		uniform float					EmissiveStrength;
		uniform float					EmissiveAttribution;

		uniform float					lengthOfCone;
		uniform int						StochasticSampling;
		uniform float					stochasticSamplingScale;
		uniform float					maximumIterations;
		uniform int						skipFirstMipLevel;
		uniform int						skipLastMipLevel;

		//uniform int						usePathCache;

		// Reflection
		uniform float					rayStep;
		uniform float					rayOffset;
		uniform float3					mainCameraPosition;
		uniform int						maximumIterationsReflection;
		uniform int						DoReflections;
		uniform float					BalanceGain;
		uniform float					skyReflectionIntensity;
		///

		uniform float					occlusionGain;

		uniform int						voxelResolution;

		uniform int						tracedTexture1UpdateCount;

		uniform float					coneLength;
		//uniform float					coneWidth;
		uniform	float					GIGain;
		uniform float					NearLightGain;
		//uniform float					OcclusionStrength;
		//uniform float					NearOcclusionStrength;
		//uniform float					FarOcclusionStrength;
		//uniform float					OcclusionPower;
		uniform int						VisualiseGI;
		uniform int						visualiseCache;
		uniform int						visualizeOcclusion;
		uniform int						visualizeReflections;
		uniform float					sunLightInjection;
		uniform int						sphericalSunlight;
		uniform half4					sunColor;
		uniform half4					skyColor;
		uniform half4					occlusionColor;
		uniform float3					sunLight;

		uniform float					skyVisibility;

		uniform int						depthStopOptimization;
		uniform int						Stereo2Mono;
		uniform int						stereoEnabled;
		uniform int						neighbourSearch;
		uniform int						highestValueSearch;
		//uniform uint					rng_state;
		uniform int						subsamplingRatio;
		
		uniform float3					gridOffset;

		uniform	sampler2D				NoiseTexture;

		//uniform StructuredBuffer<colorStruct> tracedBuffer0;
		//uniform RWStructuredBuffer<float4> tracedBuffer1 : register(u1);
		uniform RWStructuredBuffer<uint> voxelUpdateBuffer : register(u1);

		float ConeTraceBias;

		const float phi = 1.618033988;
		const float gAngle = 5.083203603249289;

		//Fix Stereo View Matrix
		float4x4 _LeftEyeProjection;
		float4x4 _RightEyeProjection;
		float4x4 _LeftEyeToWorld;
		float4x4 _RightEyeToWorld;
		//Fix Stereo View Matrix/

		struct appdata
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct v2f
		{
			float2 uv : TEXCOORD0;
			float4 vertex : SV_POSITION;
			float4 cameraRay : TEXCOORD1;
		};

		half4 _MainTex_ST;

		v2f vert(appdata v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.uv = v.uv;

			if (Stereo2Mono)
			{
				o.uv = v.uv;
				o.uv.x *= 0.5;
			}
			else if (stereoEnabled) o.uv = TransformStereoScreenSpaceTex(v.uv, 1);
			else o.uv = v.uv;

			float4 clipPos = float4(v.uv * 2.0f - 1.0f, 1.0f, 1.0f);
		
			float4 cameraRay = mul(InverseProjectionMatrix, clipPos);
			o.cameraRay = cameraRay / cameraRay.w;

			return o;
		}

		/*uint rand_xorshift()
		{
			// Xorshift algorithm from George Marsaglia's paper
			rng_state ^= (rng_state << 13);
			rng_state ^= (rng_state >> 17);
			rng_state ^= (rng_state << 5);
			return rng_state * 0.00000001;
		}*/

		float GetDepthTexture(float2 uv)
		{
#if defined(UNITY_REVERSED_Z)
#if defined(VRWORKS)
			return 1.0 - SAMPLE_DEPTH_TEXTURE(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x;
#else
			//return 1.0 - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float4(uv.xy, 0.0, 0.0)).x;
			return 1.0 - _CameraDepthTexture.Sample(my_point_clamp_sampler, float4(uv.xy, 0.0, 0.0));
#endif
#else
#if defined(VRWORKS)
			return SAMPLE_DEPTH_TEXTURE(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x;
#else
			//return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float4(uv.xy, 0.0, 0.0)).x;
			return _CameraDepthTexture.Sample(my_point_clamp_sampler, float4(uv.xy, 0.0, 0.0));
#endif
#endif
		}

		float4 GetViewSpacePosition(float2 uv)
		{
			//float depth = tex2Dlod(_CameraDepthTexture, float4(uv.xy, 0.0, 0.0)).x;
			float depth = _CameraDepthTexture.Sample(my_point_clamp_sampler, float4(uv.xy, 0.0, 0.0));

#if defined(UNITY_REVERSED_Z)
			depth = 1.0 - depth;
#endif

			if (stereoEnabled)
			{
				//Fix Stereo View Matrix
				float depth = GetDepthTexture(uv);
				float4x4 proj, eyeToWorld;

				if (uv.x < .5) // Left Eye
				{
					uv.x = saturate(uv.x * 2); // 0..1 for left side of buffer
					proj = _LeftEyeProjection;
					eyeToWorld = _LeftEyeToWorld;
				}
				else // Right Eye
				{
					uv.x = saturate((uv.x - 0.5) * 2); // 0..1 for right side of buffer
					proj = _RightEyeProjection;
					eyeToWorld = _RightEyeToWorld;
				}

				float2 uvClip = uv * 2.0 - 1.0;
				float4 clipPos = float4(uvClip, 1 - depth, 1.0);
				float4 viewPos = mul(proj, clipPos); // inverse projection by clip position
				viewPos /= viewPos.w; // perspective division
				float3 worldPos = mul(eyeToWorld, viewPos).xyz;
				//Fix Stereo View Matrix/

				return viewPos;
			}
			else
			{
				float4 viewPosition = mul(InverseProjectionMatrix, float4(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0, 2.0 * depth - 1.0, 1.0));
				viewPosition /= viewPosition.w;
				return viewPosition;
			}	
		}

		float GISampleWeight(float3 pos)
		{
			float weight = 1.0;

			if (pos.x < 0.0 || pos.x > 1.0 ||
				pos.y < 0.0 || pos.y > 1.0 ||
				pos.z < 0.0 || pos.z > 1.0)
			{
				weight = 0.0;
			}

			return weight;
		}

		uint threeD2oneD(float3 coord)
		{
			return coord.z * (voxelResolution * voxelResolution) + (coord.y * voxelResolution) + coord.x;
		}

float4 frag_position(v2f i) : SV_Target
{
	// read low res depth and reconstruct world position
	//float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
	float depth = _CameraDepthTexture.Sample(my_point_clamp_sampler, i.uv);

	//linearise depth		
	float lindepth = Linear01Depth(depth);

	//get view and then world positions		
	float4 viewPos = float4(i.cameraRay.xyz * lindepth, 1.0f);
	float3 worldPos = mul(InverseViewMatrix, viewPos).xyz;

	//worldPos.z *= 100;

	if (Stereo2Mono)
	{
		if (i.uv.x < 0.5) return float4(worldPos.xyz - gridOffset.xyz, lindepth);
		else return float4(0, 0, 0, 0);
	}
	else
	{
		return float4(worldPos.xyz - gridOffset.xyz, lindepth);
	}
}

//http://graphicrants.blogspot.com/2009/04/rgbm-color-encoding.html
float4 RGBMEncode(float3 color) {
	//color = pow(color, 0.454545); // Convert Linear to Gamma
	float4 rgbm;
	color *= 1.0 / 6.0;
	rgbm.a = saturate(max(max(color.r, color.g), max(color.b, 1e-6)));
	rgbm.a = ceil(rgbm.a * 255.0) / 255.0;
	rgbm.rgb = color / rgbm.a;
	return rgbm;
}

float3 RGBMDecode(float4 rgbm) {
	return 6.0 * rgbm.rgb * rgbm.a;
	//return pow(6.0 * rgbm.rgb * rgbm.a, 2.2); // Also converts Gamma to Linear
}
///

float3 offsets[6] =
{
	float3(1, 0, 0),
	float3(-1, 0, -0),
	float3(0, 1, 0),
	float3(0, -1, 0),
	float3(0, 0, 1),
	float3(0, 0, -1)
};

// Returns the voxel position in the grids
inline float4 GetVoxelPosition(float3 worldPosition)
{
	worldPosition = worldPosition.xyz - gridOffset.xyz;
	   
	uint cascade = 1;
	float cascade1 = 0.33;
	float cascade2 = 0.66;
	float cascade3 = 1.00;
	int cascadeBoundary = worldVolumeBoundary;
	int cascadeBoundary1 = worldVolumeBoundary * cascade1;
	int cascadeBoundary2 = worldVolumeBoundary * cascade2;
	int cascadeBoundary3 = worldVolumeBoundary * cascade3;

	if ((abs(worldPosition.x) < cascadeBoundary1) && (abs(worldPosition.y) < cascadeBoundary1) && (abs(worldPosition.z) < cascadeBoundary1))
	{
		cascade = 1;
		cascadeBoundary = cascadeBoundary1;
	}
	else if ((abs(worldPosition.x) < cascadeBoundary2) && (abs(worldPosition.y) < cascadeBoundary2) && (abs(worldPosition.z) < cascadeBoundary2))
	{
		cascade = 2;
		cascadeBoundary = cascadeBoundary2;
	}
	else if ((abs(worldPosition.x) < cascadeBoundary3) && (abs(worldPosition.y) < cascadeBoundary3) && (abs(worldPosition.z) < cascadeBoundary3))
	{
		cascade = 3;
		cascadeBoundary = cascadeBoundary3;
	}
	else cascade = 4;
	
	float3 voxelPosition = worldPosition / cascadeBoundary;
	voxelPosition += float3(1.0f, 1.0f, 1.0f);
	voxelPosition /= 2.0f;

	return float4(voxelPosition, cascade);
}

// Returns the voxel information from grid 1
inline half4 GetVoxelInfo1(float3 voxelPosition)
{
	if (voxelGrid1A.Sample(my_point_clamp_sampler, voxelPosition).r > 0.1)
	{
		uint index = threeD2oneD(voxelPosition);
		if (voxelUpdateBuffer[index] == 0) voxelUpdateBuffer[index] = 2;

		float4 tex = voxelGrid1.Sample(my_linear_clamp_sampler, voxelPosition);

		if (neighbourSearch)
		{
			[unroll(6)]
			for (int j = 0; j < 6; j++)
			{
				float3 offset = float3(0, 0, 0);
				offset = offsets[j];
				tex = max(voxelGrid1.Sample(my_linear_clamp_sampler, voxelPosition + offset), tex);
			}
		}
		return tex;
	}
	else return (0).xxxx;
}

// Returns the voxel information from grid 2
inline half4 GetVoxelInfo2(float3 voxelPosition)
{
	if (voxelGrid2A.Sample(my_point_clamp_sampler, voxelPosition).r > 0.1)
	{
		float4 tex = voxelGrid2.Sample(my_linear_clamp_sampler, voxelPosition);

		if (neighbourSearch)
		{
			[unroll(6)]
			for (int j = 0; j < 6; j++)
			{
				float3 offset = float3(0, 0, 0);
				offset = offsets[j];
				tex = max(voxelGrid2.Sample(my_linear_clamp_sampler, voxelPosition + offset), tex);
			}
		}

		return tex;
	}
	else return (0).xxxx;
}

// Returns the voxel information from grid 3
inline half4 GetVoxelInfo3(float3 voxelPosition)
{
	if (voxelGrid3A.Sample(my_point_clamp_sampler, voxelPosition).r > 0.1)
	{
		float4 tex = voxelGrid3.Sample(my_linear_clamp_sampler, voxelPosition);

		if (neighbourSearch)
		{
			[unroll(6)]
			for (int j = 0; j < 6; j++)
			{
				float3 offset = float3(0, 0, 0);
				offset = offsets[j];
				tex = max(voxelGrid3.Sample(my_linear_clamp_sampler, voxelPosition + offset), tex);
			}
		}
		return tex;
	}
	else return (0).xxxx;
}

// Returns the voxel information from grid 4
inline half4 GetVoxelInfo4(float3 voxelPosition)
{
	if (voxelGrid4A.Sample(my_point_clamp_sampler, voxelPosition).r > 0.1)
	{
		float4 tex = voxelGrid4.Sample(my_linear_clamp_sampler, voxelPosition);

		if (neighbourSearch)
		{
			[unroll(6)]
			for (int j = 0; j < 6; j++)
			{
				float3 offset = float3(0, 0, 0);
				offset = offsets[j];
				tex = max(voxelGrid4.Sample(my_linear_clamp_sampler, voxelPosition + offset), tex);
			}
		}
		return tex;
	}
	else return (0).xxxx;
}

// Returns the voxel information from grid 5
inline half4 GetVoxelInfo5(float3 voxelPosition)
{
	if (voxelGrid5A.Sample(my_point_clamp_sampler, voxelPosition).r > 0.1)
	{
		float4 tex = voxelGrid5.Sample(my_linear_clamp_sampler, voxelPosition);

		if (neighbourSearch)
		{
			[unroll(6)]
			for (int j = 0; j < 6; j++)
			{
				float3 offset = float3(0, 0, 0);
				offset = offsets[j];
				tex = max(voxelGrid5.Sample(my_linear_clamp_sampler, voxelPosition + offset), tex);
			}
		}
		return tex;
	}
	else return (0).xxxx;
}

// Returns the voxel information from cascade 1
inline half4 GetCascadeVoxelInfo2(float3 voxelPosition)
{
	half4 tex = tex3D(voxelGridCascade1, voxelPosition);

	if (neighbourSearch)
	{
		[unroll]
		for (int j = 0; j < 6; j++)
		{
			float3 offset = float3(0, 0, 0);
			offset = offsets[j];
			tex = max(tex3D(voxelGridCascade1, voxelPosition + offset), tex);
		}
	}

	return tex;
}

// Returns the voxel information from cascade 2
inline half4 GetCascadeVoxelInfo3(float3 voxelPosition)
{
	half4 tex = tex3D(voxelGridCascade2, voxelPosition);

	if (neighbourSearch)
	{
		[unroll]
		for (int j = 0; j < 6; j++)
		{
			float3 offset = float3(0, 0, 0);
			offset = offsets[j];
			tex = max(tex3D(voxelGridCascade2, voxelPosition + offset), tex);
		}
	}
	return tex;
}

float4 frag_debug(v2f i) : SV_Target
{
	// read low res depth and reconstruct world position
	//float depth = tex2D(_CameraDepthTexture, UnityStereoScreenSpaceUVAdjust(i.uv, _CameraDepthTexture_ST));
	float depth = 0;
	if (stereoEnabled) depth = 1 - GetDepthTexture(i.uv.xy);
	else depth = 1 - GetDepthTexture(i.uv.xy);
	//linearise depth		
	float lindepth = Linear01Depth(depth);

	//get view and then world positions		

	float3 worldPos = float3(0, 0, 0);
	if (stereoEnabled)
	{
		//Fix Stereo View Matrix
		float depth = GetDepthTexture(i.uv);
		float4x4 proj, eyeToWorld;

		if (i.uv.x < .5) // Left Eye
		{
			i.uv.x = saturate(i.uv.x * 2); // 0..1 for left side of buffer
			proj = _LeftEyeProjection;
			eyeToWorld = _LeftEyeToWorld;
		}
		else // Right Eye
		{
			i.uv.x = saturate((i.uv.x - 0.5) * 2); // 0..1 for right side of buffer
			proj = _RightEyeProjection;
			eyeToWorld = _RightEyeToWorld;
		}

		float2 uvClip = i.uv * 2.0 - 1.0;
		float4 clipPos = float4(uvClip, 1 - depth, 1.0);
		float4 viewPos = mul(proj, clipPos); // inverse projection by clip position
		viewPos /= viewPos.w; // perspective division
		worldPos = mul(eyeToWorld, viewPos).xyz;
		//Fix Stereo View Matrix/
	}
	else
	{
		float4 viewPos = float4(i.cameraRay.xyz * lindepth, 1.0f);
		worldPos = mul(InverseViewMatrix, viewPos).xyz;
	}

	half4 voxelInfo = half4(0.0f, 0.0f, 0.0f, 0.0f);
	float4 voxelPosition = float4(0.0f, 0.0f, 0.0f, 0.0f);

	#if defined(GRID_1)
	voxelInfo = GetVoxelInfo1(GetVoxelPosition(worldPos));
	#endif

	#if defined(GRID_2)
	voxelPosition = GetVoxelPosition(worldPos);
	if (voxelPosition.w == 1) voxelInfo = GetVoxelInfo2(voxelPosition);
	else if (voxelPosition.w == 2) voxelInfo = GetCascadeVoxelInfo2(voxelPosition);
	#endif

	#if defined(GRID_3)
	voxelPosition = GetVoxelPosition(worldPos);
	if (voxelPosition.w == 1) voxelInfo = GetVoxelInfo3(voxelPosition);
	else if (voxelPosition.w == 3) voxelInfo = GetCascadeVoxelInfo3(voxelPosition);
	#endif

	#if defined(GRID_4)
	voxelInfo = GetVoxelInfo4(GetVoxelPosition(worldPos));
	#endif

	#if defined(GRID_5)
	voxelInfo = GetVoxelInfo5(GetVoxelPosition(worldPos));
	#endif

	float3 resultingColor = (voxelInfo.a > 0.0f ? voxelInfo.rgb : float3(0.0f, 0.0f, 0.0f));
	return float4(resultingColor, 1.0f);
}

float3 GetWorldNormal(float2 uv)
{
	//float3 worldSpaceNormal = tex2D(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(uv));
	float3 worldSpaceNormal = _CameraGBufferTexture2.Sample(my_point_clamp_sampler, UnityStereoTransformScreenSpaceTex(uv));
	worldSpaceNormal = normalize(worldSpaceNormal);

	return worldSpaceNormal;
}

// Returns the voxel information
inline half4 GetVoxelInfo(float3 worldPosition)
{
	// Default value
	half4 info = half4(0.0f, 0.0f, 0.0f, 0.0f);

	uint cascade = 1;
	float cascade1 = 0.33;
	float cascade2 = 0.66;
	float cascade3 = 1.00;
	int cascadeBoundary = worldVolumeBoundary;
	int cascadeBoundary1 = worldVolumeBoundary * cascade1;
	int cascadeBoundary2 = worldVolumeBoundary * cascade2;
	int cascadeBoundary3 = worldVolumeBoundary * cascade3;

	if ((abs(worldPosition.x) < cascadeBoundary1) && (abs(worldPosition.y) < cascadeBoundary1) && (abs(worldPosition.z) < cascadeBoundary1))
	{
		cascade = 1;
		cascadeBoundary = cascadeBoundary1;
	}
	else if ((abs(worldPosition.x) < cascadeBoundary2) && (abs(worldPosition.y) < cascadeBoundary2) && (abs(worldPosition.z) < cascadeBoundary2))
	{
		cascade = 2;
		cascadeBoundary = cascadeBoundary2;
	}
	else if ((abs(worldPosition.x) < cascadeBoundary3) && (abs(worldPosition.y) < cascadeBoundary3) && (abs(worldPosition.z) < cascadeBoundary3))
	{
		cascade = 3;
		cascadeBoundary = cascadeBoundary3;
	}

	// Check if the given position is inside the voxelized volume
	if ((abs(worldPosition.x) < cascadeBoundary) && (abs(worldPosition.y) < cascadeBoundary) && (abs(worldPosition.z) < cascadeBoundary))
	{
		worldPosition += cascadeBoundary;
		worldPosition /= (2.0f * cascadeBoundary);

		if (cascade == 1)
		{
			info = voxelGrid1.Sample(my_linear_clamp_sampler, worldPosition);
			info += voxelGrid2.Sample(my_linear_clamp_sampler, worldPosition);
			info += voxelGrid3.Sample(my_linear_clamp_sampler, worldPosition);
			info += voxelGrid4.Sample(my_linear_clamp_sampler, worldPosition);
			info += voxelGrid5.Sample(my_linear_clamp_sampler, worldPosition);
		}
		else if (cascade == 2) info += tex3D(voxelGridCascade1, worldPosition) * 3;
		else if (cascade == 3) info += tex3D(voxelGridCascade2, worldPosition) * 3;
	}

	return info;
}

// Traces a ray starting from the current voxel in the reflected ray direction and accumulates color
inline half4 RayTrace(float3 worldPosition, float3 reflectedRayDirection, float3 pixelNormal)
{
	worldPosition = worldPosition.xyz - gridOffset.xyz;

	// Color for storing all the samples
	half4 accumulatedColor = half4(0.0f, 0.0f, 0.0f, 0.0f);

	float3 currentPosition = worldPosition + (rayOffset * pixelNormal);
	half4 currentVoxelInfo = half4(0.0f, 0.0f, 0.0f, 0.0f);

	bool hitFound = false;

	// Loop for tracing the ray through the scene
	[loop]
	for (float i = 0.0f; i < maximumIterationsReflection; i += 1.0f)
	{
		// Traverse the ray in the reflected direction
		currentPosition += (reflectedRayDirection * rayStep);

		// Get the currently hit voxel's information
		currentVoxelInfo = GetVoxelInfo(currentPosition);

		// At the currently traced sample
		if ((currentVoxelInfo.w > 0.0f) && (!hitFound))
		{
			accumulatedColor += currentVoxelInfo * (rayStep).xxxx;
			//if (depthStopOptimization) hitFound = true; // We dont do this or fails to reflect!
		}
	}

	return accumulatedColor;
}


inline float3 ConeTrace(float3 worldPosition, float3 coneDirection, float2 uv, float3 blueNoise, out float skyVisibility)
{
	//Temp consts till integration
	//float3 SEGISunlightVector = _WorldSpaceLightPos0;
	//float3 skyColor = unity_AmbientSky;
	//float SunlightInjection = 5.5f;
	//float3 GISunColor = float3(256 / 124, 256 / 122, 256 / 118);
	///

	float3 computedColor = float3(0.0f, 0.0f, 0.0f);

	float iteration0 = maximumIterations / 32.0f;
	float iteration1 = maximumIterations / 32.0f;
	float iteration2 = maximumIterations / 16.0f;
	float iteration3 = maximumIterations / 8.0f;
	float iteration4 = maximumIterations / 4.0f;
	float iteration5 = maximumIterations / 2.0f;

	float coneStep = lengthOfCone / maximumIterations;

	//float3 worldNormal = tex2D(_CameraGBufferTexture2, uv).rgb;

	blueNoise.xy *= 0.0625;
	blueNoise.z *= 0.0625;
	blueNoise.z -= blueNoise.z * 2;


	//float3 cacheOrigin = worldPosition + worldNormal * 0.003 * ConeTraceBias * 1.25;

	//return float4(worldPos.x - (int)gridOffset.x, worldPos.y - (int)gridOffset.y, worldPos.z - (int)gridOffset.z, lindepth);

	float3 coneOrigin = worldPosition + (coneDirection * coneStep * iteration0) * ConeTraceBias;
	//coneOrigin =  float3(coneOrigin.x - (int)gridOffset.x, coneOrigin.y - (int)gridOffset.y, coneOrigin.z - (int)gridOffset.z);

	float3 currentPosition = coneOrigin;
	half4 currentVoxelInfo = half4(0.0f, 0.0f, 0.0f, 0.0f);

	float hitFound = 0.0f;

	int coordSet = 0;
	skyVisibility = 1.0f;
	//float3 skyVisibility2 = 1.0f;
	float occlusion;
	half4 gi = half4(0, 0, 0, 0);
	//float2 interMult = float2(0, 0);
	float4 voxelPosition = (0).xxxx;

	// Sample voxel grid 1
	if (skipFirstMipLevel == 0)
	{
		[loop]
		for (float i1 = 0.0f; i1 < iteration1; i1 += 1.0f)
		{
			currentPosition += (coneStep * coneDirection);

			float fi = ((float)i1 + blueNoise.y * StochasticSampling) / iteration2;
			fi = lerp(fi, 1.0, 0.0);

			float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
			float coneSize = coneDistance * 63.6;

			if (hitFound < 0.9f)
			{
				voxelPosition = GetVoxelPosition(currentPosition);
				if (voxelPosition.w == 1) currentVoxelInfo = GetVoxelInfo1(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
				if (currentVoxelInfo.a > 0.0f)
				{
					if (depthStopOptimization) hitFound = 1.0f;
					/*if (coordSet == 0)
					{
						coordSet = 1;
						voxelBufferCoord = voxelPosition * (coneDistance * 1.72);
					}*/
				}
				if (currentVoxelInfo.a < 0.5f) currentVoxelInfo.rgb + blueNoise.xyz;
			}
			occlusion = skyVisibility * skyVisibility;
			//float3 localOcclusionColor = 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);

			float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

			currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, 0.5f);
			currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
			if (visualizeOcclusion) gi.rgb += 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);
			else gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 / falloffFix;// *(1.0 - fi * fi);// / falloffFix;

			skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * 0.0015f * (1.0 + coneDistance)), 0.65f);
		}
		computedColor = gi;

	}

	// Sample voxel grid 2
	hitFound = 0;
	gi = (0.0f).xxxx;
	//skyVisibility = 1.0f;
	currentPosition = worldPosition + (coneDirection * coneStep * iteration2);
	[loop]
	for (float i2 = 0.0f; i2 < iteration2; i2 += 1.0f)
	{
		currentPosition += (coneStep * coneDirection);

		float fi = ((float)i2 + blueNoise.y * StochasticSampling) / iteration2;
		fi = lerp(fi, 1.0, 0.0);

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
		float coneSize = coneDistance * 63.6;

		if (hitFound < 0.9f)
		{
			voxelPosition = GetVoxelPosition(currentPosition);
			if (voxelPosition.w == 1) currentVoxelInfo = GetVoxelInfo2(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
			if (voxelPosition.w == 2) currentVoxelInfo = GetCascadeVoxelInfo2(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
			if (currentVoxelInfo.a > 0.0f)
			{
				if (depthStopOptimization) hitFound = 1.0f;
				/*if (coordSet == 0)
				{
					coordSet = 1;
					voxelBufferCoord = voxelPosition * (coneDistance * 1.72);
				}*/
			}
			if (currentVoxelInfo.a < 0.5f) currentVoxelInfo.rgb + blueNoise.xyz;
		}
		occlusion = skyVisibility * skyVisibility;
		//float3 localOcclusionColor = 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);

		float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

		currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, 0.5f);
		currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
		if (visualizeOcclusion) gi.rgb += 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);
		else gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 / falloffFix;// *(1.0 - fi * fi);// / falloffFix;

		skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * 0.0015f * (1.0 + coneDistance)), 0.65f);
	}
	if (highestValueSearch) computedColor += lerp(computedColor, gi, 0.75);
	else computedColor += gi;

	// Sample voxel grid 3
	hitFound = 0;
	gi = (0.0f).xxxx;
	//skyVisibility = 1.0f;
	currentPosition = worldPosition + (coneDirection * coneStep * iteration3);
	[loop]
	for (float i3 = 0.0f; i3 < iteration3; i3 += 1.0f)
	{
		currentPosition += coneStep * coneDirection;

		float fi = ((float)i3 + blueNoise.y * StochasticSampling) / iteration3;
		fi = lerp(fi, 1.0, 0.0);

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
		float coneSize = coneDistance * 63.6;

		if (hitFound < 0.9f)
		{
			voxelPosition = GetVoxelPosition(currentPosition);
			if (voxelPosition.w == 1) currentVoxelInfo = GetVoxelInfo3(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
			if (voxelPosition.w == 3) currentVoxelInfo = GetCascadeVoxelInfo3(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
			if (currentVoxelInfo.a > 0.0f)
			{
				if (depthStopOptimization) hitFound = 1.0f;
			}
		}
		occlusion = skyVisibility * skyVisibility;
		//float3 localOcclusionColor = 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);

		float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

		currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, 0.5f);
		currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
		if (visualizeOcclusion) gi.rgb += 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);
		else gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 / falloffFix;// *(1.0 - fi * fi);// / falloffFix;

		skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * 0.0015f * (1.0 + coneDistance)), 0.65f);
	}
	if (highestValueSearch) computedColor += lerp(computedColor, gi, 0.75);
	else computedColor += gi;

	// Sample voxel grid 4
	hitFound = 0;
	gi = (0.0f).xxxx;
	//skyVisibility = 1.0f;
	currentPosition = worldPosition + (coneDirection * coneStep * iteration4);
	[loop]
	for (float i4 = 0.0f; i4 < iteration4; i4 += 1.0f)
	{
		currentPosition += coneStep * coneDirection;

		float fi = ((float)i4 + blueNoise.y * StochasticSampling) / iteration4;
		fi = lerp(fi, 1.0, 0.0);

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
		float coneSize = coneDistance * 63.6;

		if (hitFound < 0.9f)
		{
			voxelPosition = GetVoxelPosition(currentPosition);
			currentVoxelInfo = GetVoxelInfo4(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
			if (currentVoxelInfo.a > 0.0f)
			{
				if (depthStopOptimization) hitFound = 1.0f;
			}
		}
		occlusion = skyVisibility * skyVisibility;
		//float3 localOcclusionColor = 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);

		float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

		currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, 0.5f);
		currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
		if (visualizeOcclusion) gi.rgb += 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);
		else gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 / falloffFix;// *(1.0 - fi * fi);// / falloffFix;

		skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * 0.0015f * (1.0 + coneDistance)), 0.65f);
	}
	if (highestValueSearch) computedColor += lerp(computedColor, gi, 0.75);
	else computedColor += gi;

	// Sample voxel grid 5
	if (skipLastMipLevel == 0)
	{
		hitFound = 0;
		gi = (0.0f).xxxx;
		currentPosition = worldPosition + (coneDirection * coneStep * iteration5);
		[loop]
		for (float i5 = 0.0f; i5 < iteration5; i5 += 1.0f)
		{
			currentPosition += coneStep * coneDirection;

			float fi = ((float)i5 + blueNoise.y * StochasticSampling) / iteration5;
			fi = lerp(fi, 1.0, 0.0);

			float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
			float coneSize = coneDistance * 63.6;

			if (hitFound < 0.9f)
			{
				voxelPosition = GetVoxelPosition(currentPosition);
				currentVoxelInfo = GetVoxelInfo5(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
				if (currentVoxelInfo.a > 0.0f)
				{
					if (depthStopOptimization) hitFound = 1.0f;
				}
			}
			occlusion = skyVisibility * skyVisibility;
			//float3 localOcclusionColor = 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);

			float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

			currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, 0.5f);
			currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
			if (visualizeOcclusion) gi.rgb += 1 - max(currentVoxelInfo.a, (1 - currentVoxelInfo.a) * occlusionColor.rgb);
			else gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 / falloffFix;// *(1.0 - fi * fi);// / falloffFix;

			skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * 0.0015f * (1.0 + coneDistance)), 0.65f);
		}
		if (highestValueSearch) computedColor += lerp(computedColor, gi, 0.75);
		else computedColor += gi;
	}

	//Calculate lighting attribution
	if (!visualizeOcclusion)
	{
		float3 worldSpaceNormal = GetWorldNormal(uv);

		float NdotL = pow(saturate(dot(worldSpaceNormal, coneDirection) * 1.0 - 0.0), 0.5);

		computedColor *= NdotL;
		skyVisibility *= NdotL;
		skyVisibility *= lerp(saturate(dot(coneDirection, float3(0.0, 1.0, 0.0)) * 10.0 + 0.0), 1.0, sphericalSunlight);
		float3 skyColor = float3(0.0, 0.0, 0.0);

		float upGradient = saturate(dot(coneDirection, float3(0.0, 1.0, 0.0)));
		float sunGradient = saturate(dot(coneDirection, -sunLight.xyz));
		skyColor += lerp(skyColor.rgb * 1.0, skyColor.rgb * 0.5, pow(upGradient, (0.5).xxx));
		skyColor += sunColor.rgb * pow(sunGradient, (4.0).xxx) * sunLightInjection;

		computedColor.rgb *= GIGain * 0.15;

		computedColor.rgb += (skyColor * skyVisibility);

	}
	else
	{
		// gi /= maximumIterations * iteration1 * iteration2 * iteration3 * iteration4 * iteration5;
		computedColor *= GIGain;
	}

	//computedColor.rgb = gi.rgb;

	return computedColor;
}

inline float3 ComputeIndirectContribution(float3 worldPosition, float4 viewPos, float3 worldNormal, float2 uv, float depth, out float skyVisibility)
{
	float3 gi = float3(0.0f, 0.0f, 0.0f);

	float2 noiseCoord = (uv.xy * _MainTex_TexelSize.zw) / (64.0).xx;
	float4 blueNoise = tex2Dlod(NoiseTexture, float4(noiseCoord, 0.0, 0.0)).x;
	blueNoise *= (1 - depth);
	blueNoise * 0.125;
	blueNoise *= stochasticSamplingScale;


	float fi = (float)tracedTexture1UpdateCount + blueNoise.x * StochasticSampling;
	float fiN = fi / 65;
	float longitude = gAngle * fi;
	float latitude = asin(fiN * 2.0 - 1.0);

	float3 kernel;
	kernel.x = cos(latitude) * cos(longitude);
	kernel.z = cos(latitude) * sin(longitude);
	kernel.y = sin(latitude);

	//kernel = normalize(kernel + worldNormal.xyz * 1.0);

	float3 randomVector = normalize(kernel);
	float3 direction1 = normalize(cross(worldNormal, randomVector));
	float3 coneDirection2 = lerp(direction1, worldNormal, 0.3333f);

	/*
	///Reflection cone setup
	float depthValue;
	float3 viewSpaceNormal;
	DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(uv)), depthValue, viewSpaceNormal);
	viewSpaceNormal = normalize(viewSpaceNormal);
	float3 pixelNormal = mul((float3x3)InverseViewMatrix, viewSpaceNormal);
	float3 pixelToCameraUnitVector = normalize(mainCameraPosition - worldPosition);
	float3 reflectedRayDirection = reflect(pixelToCameraUnitVector, pixelNormal);
	reflectedRayDirection *= -1.0;
	float4 reflection = (0).xxxx;
	///
	*/
	//uint index = 0;
	//float3 voxelBufferCoord;
	/*if (usePathCache)
	{
		gi = ConeTrace(worldPosition, kernel.xyz, uv, blueNoise, voxelBufferCoord, skyVisibility);

		//voxelBufferCoord.x += (blueNoise.x * 0.00000001) * StochasticSampling;
		//voxelBufferCoord.y += (blueNoise.y * 0.00000001) * StochasticSampling;
		//voxelBufferCoord.z += (blueNoise.z * 0.00000001) * StochasticSampling;
		//voxelBufferCoord.xy *= uv;
		//voxelBufferCoord.z *= depth;
		index = voxelBufferCoord.x * (voxelResolution) * (voxelResolution)+voxelBufferCoord.y * (voxelResolution)+voxelBufferCoord.z;
		tracedBuffer1[index] += float4(gi, 1);
	}*/

	gi = ConeTrace(worldPosition, worldNormal, uv, blueNoise, skyVisibility);
	/*if ((DoReflections && !visualizeOcclusion && !VisualiseGI) || visualizeReflections)
	{
		float4 viewSpacePosition = GetViewSpacePosition(uv.xy);
		float3 viewVector = normalize(viewSpacePosition.xyz);
		float4 worldViewVector = mul(InverseViewMatrix, float4(viewVector.xyz, 0.0));

		float4 spec = tex2D(_CameraGBufferTexture1, UnityStereoTransformScreenSpaceTex(uv));
		
		float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectedRayDirection.xyz)) * (spec.a), 5.0);
		fresnel = lerp(fresnel, (1.0).xxx, spec.rgb);
		fresnel *= saturate(spec.a * 6.0);

		reflection = RayTrace(worldPosition, reflectedRayDirection, pixelNormal) * BalanceGain;
		//reflection.rgb *= maximumIterationsReflection * BalanceGain;

		reflection.rgb = reflection.rgb * 0.7 + (reflection.a * 1.0 * skyColor) * 2.4015 * skyReflectionIntensity;

		if (visualizeReflections) reflection.rgb = lerp((0).xxx, reflection.rgb, fresnel.rgb);
		else gi.rgb = lerp(gi.rgb, reflection.rgb, fresnel.rgb);
	}*/

	/*if (usePathCache && !visualizeOcclusion)
	{
		float4 cachedResult = float4(tracedBuffer0[index]);// *0.000003;

		//Average HSV values independantly for prettier result
		half4 cachedHSV = float4(rgb2hsv(cachedResult.rgb), 0);
		half4 giHSV = float4(rgb2hsv(gi), 0);
		gi.rgb *= cachedResult.rgb * EmissiveAttribution;
		giHSV.rg = float2(rgb2hsv(gi).r, lerp(cachedHSV.g, giHSV.g, 0.5));
		gi.rgb += hsv2rgb(giHSV);

		if (visualiseCache) gi.rgb = cachedResult.rgb;	
	}*/
	//if (visualizeReflections) gi.rgb = reflection.rgb;

	return gi;
}

inline float3 ComputeReflection(float3 worldPosition, float2 uv, float3 gi, float skyVisibility)
{
	///Reflection cone setup
	float depthValue;
	float3 viewSpaceNormal;
	//DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(uv)), depthValue, viewSpaceNormal);
	DecodeDepthNormal(_CameraDepthNormalsTexture.Sample(my_point_clamp_sampler, UnityStereoTransformScreenSpaceTex(uv)), depthValue, viewSpaceNormal);

	viewSpaceNormal = normalize(viewSpaceNormal);
	float3 pixelNormal = mul((float3x3)InverseViewMatrix, viewSpaceNormal);
	float3 pixelToCameraUnitVector = normalize(mainCameraPosition - worldPosition);
	float3 reflectedRayDirection = reflect(pixelToCameraUnitVector, pixelNormal);
	reflectedRayDirection *= -1.0;
	half4 reflection = (0).xxxx;
	///
	float4 viewSpacePosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(uv.xy));
	float3 viewVector = normalize(viewSpacePosition.xyz);
	float4 worldViewVector = mul(InverseViewMatrix, float4(viewVector.xyz, 0.0));

	//half4 spec = tex2D(_CameraGBufferTexture1, UnityStereoTransformScreenSpaceTex(uv));
	half4 spec = _CameraGBufferTexture1.Sample(my_point_clamp_sampler, UnityStereoTransformScreenSpaceTex(uv));

	float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectedRayDirection.xyz)) * (spec.a * 0.5 + 0.5), 5.0);
	fresnel = lerp(fresnel, (1.0).xxx, spec.rgb);
	fresnel *= saturate(spec.a * 6.0);

	reflection = RayTrace(worldPosition, reflectedRayDirection, pixelNormal) * BalanceGain / maximumIterationsReflection;

	reflection.rgb = reflection.rgb * 0.7 + (reflection.a * 1.00 * skyColor) * 2.41 * skyReflectionIntensity * skyVisibility;

	if (visualizeReflections) reflection.rgb = lerp((0).xxx, reflection.rgb, fresnel.rgb);
	else reflection.rgb = lerp(gi.rgb, reflection.rgb, fresnel.rgb);

	return reflection.rgb;
}

half4 frag_lighting(v2f i) : SV_Target
{
	//if (i.uv.x % subsamplingRatio == 0) return (0).xxxx;


	//rng_state = i.uv.x * i.uv.y;
	//float3 directLighting = tex2D(_MainTex, i.uv).rgb;
	half4 directLighting = _MainTex.Sample(my_point_clamp_sampler, i.uv);

	//half4 gBufferSample = tex2D(_CameraGBufferTexture0, i.uv);
	half4 gBufferSample = _CameraGBufferTexture0.Sample(my_point_clamp_sampler, i.uv);

	//float3 albedo = gBufferSample.rgb;
	//float ao = gBufferSample.a;

	//float metallic = tex2D(_CameraGBufferTexture1, i.uv).r;
	float metallic = _CameraGBufferTexture1.Sample(my_point_clamp_sampler, i.uv).r;


	// read low res depth and reconstruct world position
	float depth = GetDepthTexture(i.uv);

	//linearise depth		
	float lindepth = Linear01Depth(1 - depth);

	//get view and then world positions		

	float4 viewPos = float4(0, 0, 0, 0);
	float3 worldPos = float3(0, 0, 0);
	if (stereoEnabled)
	{
		//Fix Stereo View Matrix
		float depth = GetDepthTexture(i.uv);
		float4x4 proj, eyeToWorld;

		if (i.uv.x < .5) // Left Eye
		{
			i.uv.x = saturate(i.uv.x * 2); // 0..1 for left side of buffer
			proj = _LeftEyeProjection;
			eyeToWorld = _LeftEyeToWorld;
		}
		else // Right Eye
		{
			i.uv.x = saturate((i.uv.x - 0.5) * 2); // 0..1 for right side of buffer
			proj = _RightEyeProjection;
			eyeToWorld = _RightEyeToWorld;
		}

		float2 uvClip = i.uv * 2.0 - 1.0;
		float4 clipPos = float4(uvClip, 1 - depth, 1.0);
		viewPos = mul(proj, clipPos); // inverse projection by clip position
		viewPos /= viewPos.w; // perspective division
		worldPos = mul(eyeToWorld, viewPos).xyz;
		//Fix Stereo View Matrix/
	}
	else
	{
		viewPos = float4(i.cameraRay.xyz * lindepth, 1.0f);
		worldPos = mul(InverseViewMatrix, viewPos).xyz;
	}

	float3 worldSpaceNormal = GetWorldNormal(i.uv);

	float skyVisibility;
	float3 indirectContribution = ComputeIndirectContribution(worldPos, viewPos, worldSpaceNormal, i.uv, depth, skyVisibility);
	float3 indirectLighting = directLighting + ((gBufferSample.a * indirectLightingStrength * (1.0f - metallic) * gBufferSample.rgb) / PI) * indirectContribution;
	//indirectLighting = directLighting + indirectLighting * gBufferSample.a * gBufferSample.rgb;
	//if (skyVisibility == 0) indirectLighting += directLighting;
	if ((DoReflections && !visualizeOcclusion && !VisualiseGI) || visualizeReflections)
	{
		indirectLighting = ComputeReflection(worldPos, i.uv, indirectLighting, skyVisibility);
	}

	//else indirectLighting *= 1.85;

	

	if (VisualiseGI || visualizeOcclusion || visualiseCache) indirectLighting = indirectContribution / maximumIterations / 1.85;

	return half4(indirectLighting, 1.0f);
}

float4 frag_normal_texture(v2f i) : SV_Target
{
	float3 worldSpaceNormal = GetWorldNormal(i.uv);
	return float4(worldSpaceNormal, 1.0f);
}

ENDCG

// 0 : World Position Writing pass
Pass
{
	CGPROGRAM
	#pragma vertex vert
	#pragma fragment frag_position
	#pragma target 5.0
	ENDCG
}

// 1 : Voxel Grid debug pass
Pass
{
	CGPROGRAM
	#pragma vertex vert
	#pragma fragment frag_debug
	#pragma multi_compile GRID_1 GRID_2 GRID_3 GRID_4 GRID_5
	#pragma target 5.0
	ENDCG
}

// 2 : Lighting pass
Pass
{
	CGPROGRAM
	#pragma vertex vert
	#pragma fragment frag_lighting
	#pragma target 5.0
	ENDCG
}

// 3 : Composition pass
/*Pass
{
	CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag_blur
		#pragma fragmentoption ARB_precision_hint_fastest
		#pragma multi_compile_instancing
		#if defined (VRWORKS)
			#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
		#endif

		float4 frag_blur(v2f input) : COLOR0
		{
			half3 col = tex2D(_MainTex, input.uv).rgb;
			half3 giCol = tex2D(gi, input.uv).rgb;
			half3 lpvCol = tex2D(lpv, input.uv).rgb;
			//half3 gBufferSample = tex2D(_CameraGBufferTexture0, input.uv).rgb;
			//half3 finalLighting = giCol.rgb + lerp(col.rgb, gBufferSample.rgb * 0.25, 0.25);

			if (!VisualiseGI) return float4(giCol + lpvCol, 1);
			else return float4(giCol, 1);

			//return float4(finalLighting, 1);
		}

		ENDCG
}

// 4 : Normal texture writing
Pass
{
	CGPROGRAM
	#pragma vertex vert
	#pragma fragment frag_normal_texture
	ENDCG
}*/
}
}