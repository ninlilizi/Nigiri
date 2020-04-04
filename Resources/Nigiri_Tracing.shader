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

		// Include
		#include "UnityCG.cginc"
		#include "NKLI_Nigiri_SVONode.cginc"

		#define PI 3.1415926f



		struct colorStruct
		{
			float4 value;
		};

		//uniform sampler3D				voxelGrid1;
		//uniform sampler3D				voxelGrid2;
		//uniform sampler3D				voxelGrid3;
		//uniform sampler3D				voxelGrid4;
		//uniform sampler3D				voxelGrid5;

		uniform sampler2D 				_MainTex;
		uniform sampler2D				_IndirectTex;
		uniform sampler2D				_CameraDepthTexture;
		uniform sampler2D				_CameraDepthNormalsTexture;
		uniform sampler2D				_CameraGBufferTexture0;
		uniform sampler2D				_CameraGBufferTexture1;

		//uniform sampler2D				depthTexture;

		half4							_CameraDepthTexture_ST;
		half4							_CameraGBufferTexture0_ST;
		half4							_CameraGBufferTexture1_ST;
		half4							_CameraDepthNormalsTexture_ST;

		uniform float4x4				InverseProjectionMatrix;
		uniform float4x4				InverseViewMatrix;

		uniform float4					_MainTex_TexelSize;

		uniform float					_giAreaSize;
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
		uniform uint					rng_state;
		
		uniform float3					gridOffset;

		uniform sampler2D _CameraGBufferTexture2;
		half4 _CameraGBufferTexture2_ST;

		uniform sampler2D gi;
		uniform sampler2D lpv;

		uniform sampler2D NoiseTexture;

		//uniform StructuredBuffer<colorStruct> tracedBuffer0;
		//uniform RWStructuredBuffer<float4> tracedBuffer1 : register(u1);
		//uniform RWStructuredBuffer<uint> voxelUpdateBuffer : register(u1);
		uniform StructuredBuffer<SVONode> _SVO;

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

		uint rand_xorshift()
		{
			// Xorshift algorithm from George Marsaglia's paper
			rng_state ^= (rng_state << 13);
			rng_state ^= (rng_state >> 17);
			rng_state ^= (rng_state << 5);
			return rng_state * 0.00000001;
		}

		float GetDepthTexture(float2 uv)
		{
#if defined(UNITY_REVERSED_Z)
#if defined(VRWORKS)
			return 1.0 - SAMPLE_DEPTH_TEXTURE(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x;
#else
			return 1.0 - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float4(uv.xy, 0.0, 0.0)).x;
#endif
#else
#if defined(VRWORKS)
			return SAMPLE_DEPTH_TEXTURE(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x;
#else
			return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float4(uv.xy, 0.0, 0.0)).x;
#endif
#endif
		}

		float4 GetViewSpacePosition(float2 uv)
		{
			float depth = tex2Dlod(_CameraDepthTexture, float4(uv.xy, 0.0, 0.0)).x;

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

		float4 DecodeRGBAuint(uint value)
		{
			uint ai = value & 0x0000007F;
			uint vi = (value / 0x00000080) & 0x000007FF;
			uint si = (value / 0x00040000) & 0x0000007F;
			uint hi = value / 0x02000000;

			float h = float(hi) / 127.0;
			float s = float(si) / 127.0;
			float v = (float(vi) / 2047.0) * 10.0;
			float a = ai * 2.0;

			v = pow(v, 3.0);

			float3 color = hsv2rgb(float3(h, s, v));

			return float4(color.rgb, a);
		}

		uint EncodeRGBAuint(float4 color)
		{
			//7[HHHHHHH] 7[SSSSSSS] 11[VVVVVVVVVVV] 7[AAAAAAAA]
			float3 hsv = rgb2hsv(color.rgb);
			hsv.z = pow(hsv.z, 1.0 / 3.0);

			uint result = 0;

			uint a = min(127, uint(color.a / 2.0));
			uint v = min(2047, uint((hsv.z / 10.0) * 2047));
			uint s = uint(hsv.y * 127);
			uint h = uint(hsv.x * 127);

			result += a;
			result += v * 0x00000080; // << 7
			result += s * 0x00040000; // << 18
			result += h * 0x02000000; // << 25

			return result;
		}

		uint threeD2oneD(float3 coord)
		{
			return coord.z * (voxelResolution * voxelResolution) + (coord.y * voxelResolution) + coord.x;
		}

float4 frag_position(v2f i) : SV_Target
{
	// read low res depth and reconstruct world position
	float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);

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
		return float4(worldPos.xyz, lindepth);
	}
}

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
inline float3 GetVoxelPosition(float3 worldPosition)
{
	float3 voxelPosition = worldPosition / _giAreaSize;
	voxelPosition += float3(1.0f, 1.0f, 1.0f);
	voxelPosition /= 2.0f;
	return voxelPosition;
}
/*
// Returns the voxel information from grid 1
inline float4 GetVoxelInfo1(float3 voxelPosition)
{
	//voxelUpdateBuffer
	//uint threeD2oneD(float3 coord)

	//uint index = threeD2oneD(voxelPosition);
	//if (voxelUpdateBuffer[index] == 0) 
		//voxelUpdateBuffer[index] = 2;

	float4 tex = tex3D(voxelGrid1, voxelPosition);
	
	if (neighbourSearch)
	{
		[unroll]
		for (int j = 0; j < 6; j++)
		{
			float3 offset = float3(0, 0, 0);
			offset = offsets[j];
			tex = max(tex3D(voxelGrid1, voxelPosition + offset), tex);
		}
	}
	return tex;
}

// Returns the voxel information from grid 2
inline float4 GetVoxelInfo2(float3 voxelPosition)
{
	float4 tex = tex3D(voxelGrid2, voxelPosition);

	if (neighbourSearch)
	{
		[unroll]
		for (int j = 0; j < 6; j++)
		{
			float3 offset = float3(0, 0, 0);
			offset = offsets[j];
			tex = max(tex3D(voxelGrid2, voxelPosition + offset), tex);
		}
	}

	return tex;
}

// Returns the voxel information from grid 3
inline float4 GetVoxelInfo3(float3 voxelPosition)
{
	float4 tex = tex3D(voxelGrid3, voxelPosition);

	if (neighbourSearch)
	{
		[unroll]
		for (int j = 0; j < 6; j++)
		{
			float3 offset = float3(0, 0, 0);
			offset = offsets[j];
			tex = max(tex3D(voxelGrid3, voxelPosition + offset), tex);
		}
	}
	return tex;
}

// Returns the voxel information from grid 4
inline float4 GetVoxelInfo4(float3 voxelPosition)
{
	float4 tex = tex3D(voxelGrid4, voxelPosition);
	if (neighbourSearch)
	{
		[unroll]
		for (int j = 0; j < 6; j++)
		{
			float3 offset = float3(0, 0, 0);
			offset = offsets[j];
			tex = max(tex3D(voxelGrid4, voxelPosition + offset), tex);
		}
	}
	return tex;
}

// Returns the voxel information from grid 5
inline float4 GetVoxelInfo5(float3 voxelPosition)
{
	float4 tex = tex3D(voxelGrid5, voxelPosition);
	if (neighbourSearch)
	{
		[unroll]
		for (int j = 0; j < 6; j++)
		{
			float3 offset = float3(0, 0, 0);
			offset = offsets[j];
			tex = max(tex3D(voxelGrid5, voxelPosition + offset), tex);
		}
	}
	return tex;
}
*/

/// <summary>
/// Returns the node offset calculated from spatial relation
/// </summary>
inline uint GetSVOBitOffset(float3 tX, float3 tM)
{
	uint nextIndex = 0;
	if (tX.x > tM.x)
		nextIndex = nextIndex | (1);
	if (tX.y > tM.y)
		nextIndex = nextIndex | (1 << 1);
	if (tX.z > tM.z)
		nextIndex = nextIndex | (1 << 2);

	return nextIndex;
}


// Returns the voxel information from grid 1
inline float4 GetVoxelInfoSVO(float3 worldPosition)
{
	/// Calculate initial values
	// AABB Min/Max x,y,z
	float halfArea = _giAreaSize / 2;
	float3 t0 = float3(-halfArea, -halfArea, -halfArea);
	float3 t1 = float3(halfArea, halfArea, halfArea);

	// Traverse tree
	uint offset = 0;
	while (true)
	{
		// Retrieve node
		SVONode node = _SVO[offset];

		// If no children then tag for split queue consideration
		if (node.referenceOffset == 0)
		{
			// If no child then return colour
			return node.UnPackColour();
		}
		else
		{
			// Middle of node coordiates
			float3 tM = float3(0.5 * (t0.x + t1.x), 0.5 * (t0.y + t1.y), 0.5 * (t0.z + t1.z));

			// Child node offset index
			uint childIndex = GetSVOBitOffset(worldPosition.xyz, tM);

			// Set extents of child node
			switch (childIndex)
			{
			case 0:
				t0 = float3(t0.x, t0.y, t0.z);
				t1 = float3(tM.x, tM.y, tM.z);
				break;
			case 4:
				t0 = float3(t0.x, t0.y, tM.z);
				t1 = float3(tM.x, tM.y, t1.z);
				break;
			case 2:
				t0 = float3(t0.x, tM.y, t0.z);
				t1 = float3(tM.x, t1.y, tM.z);
				break;
			case 6:
				t0 = float3(t0.x, tM.y, tM.z);
				t1 = float3(tM.x, t1.y, t1.z);
				break;
			case 1:
				t0 = float3(tM.x, t0.y, t0.z);
				t1 = float3(t1.x, tM.y, tM.z);
				break;
			case 5:
				t0 = float3(tM.x, t0.y, tM.z);
				t1 = float3(t1.x, tM.y, t1.z);
				break;
			case 3:
				t0 = float3(tM.x, tM.y, t0.z);
				t1 = float3(t1.x, t1.y, tM.z);
				break;
			case 7:
				t0 = float3(tM.x, tM.y, tM.z);
				t1 = float3(t1.x, t1.y, t1.z);
				break;
			}

			// Offet is reference + the node offset index
			offset = node.referenceOffset + childIndex;
		}
	}
	return float4(0, 0, 0, 0);
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

	float4 voxelInfo = float4(0.0f, 0.0f, 0.0f, 0.0f);
	float3 voxelPosition = (0).xxx;

	#if defined(GRID_1)
	//voxelInfo = GetVoxelInfo1(GetVoxelPosition(worldPos));
	#endif

	#if defined(GRID_2)
	//voxelPosition = GetVoxelPosition(worldPos);
	//voxelInfo = GetVoxelInfo2(voxelPosition);
	#endif

	#if defined(GRID_3)
	//voxelPosition = GetVoxelPosition(worldPos);
	//voxelInfo = GetVoxelInfo3(voxelPosition);
	#endif

	#if defined(GRID_4)
	//voxelInfo = GetVoxelInfo4(GetVoxelPosition(worldPos));
	#endif

	#if defined(GRID_5)
	//voxelInfo = GetVoxelInfo5(GetVoxelPosition(worldPos));
	#endif



	#if defined(GRID_SVO)
	voxelInfo = GetVoxelInfoSVO(worldPos);
	#endif

	//return float4(worldPos, 1);

	float3 resultingColor = (voxelInfo.a > 0.0f ? voxelInfo.rgb : float3(0.0f, 0.0f, 0.0f));
	return float4(resultingColor, 1.0f);
}

float3 GetWorldNormal(float2 uv)
{
	float3 worldSpaceNormal = tex2D(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(uv));
	worldSpaceNormal = normalize(worldSpaceNormal);

	return worldSpaceNormal;
}

// Returns the voxel information
inline float4 GetVoxelInfo(float3 worldPosition)
{
	// Default value
	float4 info = float4(0.0f, 0.0f, 0.0f, 0.0f);

	// Check if the given position is inside the voxelized volume
	if ((abs(worldPosition.x) < _giAreaSize) && (abs(worldPosition.y) < _giAreaSize) && (abs(worldPosition.z) < _giAreaSize))
	{
		worldPosition += _giAreaSize;
		worldPosition /= (2.0f * _giAreaSize);


		//info = tex3D(voxelGrid1, worldPosition);
		info = GetVoxelInfoSVO(worldPosition);
		//info += tex3D(voxelGrid2, worldPosition);
		//info += tex3D(voxelGrid3, worldPosition);
		//info += tex3D(voxelGrid4, worldPosition);
		//info += tex3D(voxelGrid5, worldPosition);
	}

	return info;
}

// Traces a ray starting from the current voxel in the reflected ray direction and accumulates color
inline float4 RayTrace(float3 worldPosition, float3 reflectedRayDirection, float3 pixelNormal)
{
	// Color for storing all the samples
	float4 accumulatedColor = float4(0.0f, 0.0f, 0.0f, 0.0f);

	float3 currentPosition = worldPosition + (rayOffset * pixelNormal);
	float4 currentVoxelInfo = float4(0.0f, 0.0f, 0.0f, 0.0f);

	bool hitFound = false;

	// Loop for tracing the ray through the scene
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
	float4 currentVoxelInfo = float4(0.0f, 0.0f, 0.0f, 0.0f);

	float hitFound = 0.0f;

	int coordSet = 0;
	skyVisibility = 1.0f;
	//float3 skyVisibility2 = 1.0f;
	float occlusion;
	float4 gi = float4(0, 0, 0, 0);
	//float2 interMult = float2(0, 0);
	float3 voxelPosition = (0).xxx;

	// Sample voxel grid 1
	if (skipFirstMipLevel == 0)
	{
		for (float i1 = 0.0f; i1 < iteration1; i1 += 1.0f)
		{
			currentPosition += (coneStep * coneDirection);

			float fi = ((float)i1 + blueNoise.y * StochasticSampling) / iteration2;
			fi = lerp(fi, 1.0, 0.0);

			float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
			float coneSize = coneDistance * 63.6;

			if (hitFound < 0.9f)
			{
				//voxelPosition = GetVoxelPosition(currentPosition);
				currentVoxelInfo = GetVoxelInfoSVO(currentPosition);// *GISampleWeight(voxelPosition);
				if (currentVoxelInfo.a > 0.0f)
				{
					if (depthStopOptimization) hitFound = 1.0f;
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

	/*
	// Sample voxel grid 2
	hitFound = 0;
	gi = (0.0f).xxxx;
	//skyVisibility = 1.0f;
	currentPosition = worldPosition + (coneDirection * coneStep * iteration2);
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
			//currentVoxelInfo = GetVoxelInfo2(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
			if (currentVoxelInfo.a > 0.0f)
			{
				if (depthStopOptimization) hitFound = 1.0f;
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
	*/

	/*
	// Sample voxel grid 3
	hitFound = 0;
	gi = (0.0f).xxxx;
	//skyVisibility = 1.0f;
	currentPosition = worldPosition + (coneDirection * coneStep * iteration3);
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
			//GetVoxelInfo3(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
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
	*/

	/*
	// Sample voxel grid 4
	hitFound = 0;
	gi = (0.0f).xxxx;
	//skyVisibility = 1.0f;
	currentPosition = worldPosition + (coneDirection * coneStep * iteration4);
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
			//currentVoxelInfo = GetVoxelInfo4(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
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
	*/

	/*
	// Sample voxel grid 5
	if (skipLastMipLevel == 0)
	{
		hitFound = 0;
		gi = (0.0f).xxxx;
		currentPosition = worldPosition + (coneDirection * coneStep * iteration5);
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
				//currentVoxelInfo = GetVoxelInfo5(voxelPosition.xyz) * GISampleWeight(voxelPosition.xyz);
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
	*/

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
	DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(uv)), depthValue, viewSpaceNormal);
	viewSpaceNormal = normalize(viewSpaceNormal);
	float3 pixelNormal = mul((float3x3)InverseViewMatrix, viewSpaceNormal);
	float3 pixelToCameraUnitVector = normalize(mainCameraPosition - worldPosition);
	float3 reflectedRayDirection = reflect(pixelToCameraUnitVector, pixelNormal);
	reflectedRayDirection *= -1.0;
	float4 reflection = (0).xxxx;
	///
	float4 viewSpacePosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(uv.xy));
	float3 viewVector = normalize(viewSpacePosition.xyz);
	float4 worldViewVector = mul(InverseViewMatrix, float4(viewVector.xyz, 0.0));

	float4 spec = tex2D(_CameraGBufferTexture1, UnityStereoTransformScreenSpaceTex(uv));

	float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectedRayDirection.xyz)) * (spec.a * 0.5 + 0.5), 5.0);
	fresnel = lerp(fresnel, (1.0).xxx, spec.rgb);
	fresnel *= saturate(spec.a * 6.0);

	reflection = RayTrace(worldPosition, reflectedRayDirection, pixelNormal) * BalanceGain / maximumIterationsReflection;

	reflection.rgb = reflection.rgb * 0.7 + (reflection.a * 1.00 * skyColor) * 2.41 * skyReflectionIntensity * skyVisibility;

	if (visualizeReflections) reflection.rgb = lerp((0).xxx, reflection.rgb, fresnel.rgb);
	else reflection.rgb = lerp(gi.rgb, reflection.rgb, fresnel.rgb);

	return reflection.rgb;
}

float4 frag_lighting(v2f i) : SV_Target
{
	rng_state = i.uv.x * i.uv.y;
	float3 directLighting = tex2D(_MainTex, i.uv).rgb;

	float4 gBufferSample = tex2D(_CameraGBufferTexture0, i.uv);
	//float3 albedo = gBufferSample.rgb;
	//float ao = gBufferSample.a;

	float metallic = tex2D(_CameraGBufferTexture1, i.uv).r;


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

	return float4(indirectLighting, 1.0f);
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
	//#pragma multi_compile GRID_1 GRID_2 GRID_3 GRID_4 GRID_5 GRID_SVO
	#pragma multi_compile GRID_SVO
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
Pass
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
}
	}
}