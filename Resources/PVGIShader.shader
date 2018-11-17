Shader "Hidden/PVGIShader"
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

		uniform sampler3D				voxelGrid1;
		uniform sampler3D				voxelGrid2;
		uniform sampler3D				voxelGrid3;
		uniform sampler3D				voxelGrid4;
		uniform sampler3D				voxelGrid5;

		uniform sampler2D 				_MainTex;
		uniform sampler2D				_IndirectTex;
		uniform sampler2D				_CameraDepthTexture;
		uniform sampler2D				_CameraDepthNormalsTexture;
		uniform sampler2D				_CameraGBufferTexture0;
		uniform sampler2D				_CameraGBufferTexture1;

		uniform float4x4				InverseProjectionMatrix;
		uniform float4x4				InverseViewMatrix;

		uniform float4					_MainTex_TexelSize;

		uniform float					worldVolumeBoundary;
		uniform float					indirectLightingStrength;
		uniform float					SunlightInjection;

		uniform float					lengthOfCone;
		uniform int						StochasticSampling;
		uniform float					maximumIterations;

		uniform int						highestVoxelResolution;

		uniform int						tracedTexture1UpdateCount;

		uniform float					coneLength;
		uniform float					coneWidth;
		uniform int						VisualiseGI;

		uniform sampler2D _CameraGBufferTexture2;

		uniform sampler2D gi;
		uniform sampler2D lpv;

		uniform sampler2D NoiseTexture;

		uniform StructuredBuffer<colorStruct> tracedBuffer0;
		uniform RWStructuredBuffer<float4> tracedBuffer1 : register(u1);

		const float phi = 1.618033988;
		const float gAngle = 5.083203603249289;

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

		v2f vert(appdata v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.uv = v.uv;

			//transform clip pos to view space
			float4 clipPos = float4(v.uv * 2.0f - 1.0f, 1.0f, 1.0f);
			float4 cameraRay = mul(InverseProjectionMatrix, clipPos);
			o.cameraRay = cameraRay / cameraRay.w;

			return o;
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

		float4 frag_position(v2f i) : SV_Target
		{
			// read low res depth and reconstruct world position
			float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture , i.uv);

			//linearise depth		
			float lindepth = Linear01Depth(depth);

			//get view and then world positions		
			float4 viewPos = float4(i.cameraRay.xyz * lindepth, 1.0f);
			float3 worldPos = mul(InverseViewMatrix, viewPos).xyz;

			return float4(worldPos, lindepth);
	}

			// Returns the voxel position in the grids
			inline float3 GetVoxelPosition(float3 worldPosition)
			{
				float3 voxelPosition = worldPosition / worldVolumeBoundary;
				voxelPosition += float3(1.0f, 1.0f, 1.0f);
				voxelPosition /= 2.0f;
				return voxelPosition;
			}

	// Returns the voxel information from grid 1
	inline float4 GetVoxelInfo1(float3 voxelPosition)
	{
		float4 info = tex3D(voxelGrid1, voxelPosition);
		return info;
	}

	// Returns the voxel information from grid 2
	inline float4 GetVoxelInfo2(float3 voxelPosition)
	{
		float4 info = tex3D(voxelGrid2, voxelPosition);
		return info;
	}

	// Returns the voxel information from grid 3
	inline float4 GetVoxelInfo3(float3 voxelPosition)
	{
		float4 info = tex3D(voxelGrid3, voxelPosition);
		return info;
	}

	// Returns the voxel information from grid 4
	inline float4 GetVoxelInfo4(float3 voxelPosition)
	{
		float4 info = tex3D(voxelGrid4, voxelPosition);
		return info;
	}

	// Returns the voxel information from grid 5
	inline float4 GetVoxelInfo5(float3 voxelPosition)
	{
		float4 info = tex3D(voxelGrid5, voxelPosition);
		return info;
	}

	float4 frag_debug(v2f i) : SV_Target
	{
		// read low res depth and reconstruct world position
		float depth = tex2D(_CameraDepthTexture, i.uv);

	//linearise depth		
	float lindepth = Linear01Depth(depth);

	//get view and then world positions		
	float4 viewPos = float4(i.cameraRay.xyz * lindepth, 1.0f);
	float3 worldPos = mul(InverseViewMatrix, viewPos).xyz;

	float4 voxelInfo = float4(0.0f, 0.0f, 0.0f, 0.0f);

	#if defined(GRID_1)
	voxelInfo = GetVoxelInfo1(GetVoxelPosition(worldPos));
	#endif

	#if defined(GRID_2)
	voxelInfo = GetVoxelInfo2(GetVoxelPosition(worldPos));
	#endif

	#if defined(GRID_3)
	voxelInfo = GetVoxelInfo3(GetVoxelPosition(worldPos));
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

float GetDepthTexture(float2 uv)
{
#if defined(UNITY_REVERSED_Z)
#if defined(VRWORKS)
	return 1.0 - SAMPLE_DEPTH_TEXTURE(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x;
#else
	return 1.0 - SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float4(uv.x, uv.y, 0.0, 0.0)).x;
#endif
#else
#if defined(VRWORKS)
	return SAMPLE_DEPTH_TEXTURE(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x;
#else
	return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float4(uv.x, uv.y, 0.0, 0.0)).x;
#endif
#endif
}

float3 GetWorldNormal(float2 screenspaceUV)
{
	//float depthValue;
	//float3 viewSpaceNormal;
	//DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, screenspaceUV), depthValue, viewSpaceNormal);
	//viewSpaceNormal = normalize(viewSpaceNormal);
	//float3 worldSpaceNormal = mul((float3x3)InverseViewMatrix, viewSpaceNormal);
	//float3 worldSpaceNormal = mul((float3x3)InverseViewMatrix, tex2D(_CameraGBufferTexture2, screenspaceUV));
	float3 worldSpaceNormal = tex2D(_CameraGBufferTexture2, screenspaceUV);
	worldSpaceNormal = normalize(worldSpaceNormal);

	return worldSpaceNormal;
}

inline float3 ConeTrace(float3 worldPosition, float3 coneDirection, float2 uv, float3 blueNoise, out float3 voxelBufferCoord)
{
	//Temp consts till integration
	float NearLightGain = 1.14f;
	float NearOcclusionStrength = 0.5f;
	float OcclusionStrength = 0.15;
	float FarOcclusionStrength = 1;
	float OcclusionPower = 0.65;
	float ConeTraceBias = 2.01;
	int SEGISphericalSkylight = 0;
	float3 SEGISunlightVector = _WorldSpaceLightPos0;
	float3 SEGISkyColor = float3(256 / 193, 256 / 223, 256 / 243);
	float3 GISunColor = float3(256 / 124, 256 / 122, 256 / 118);;
	float GIGain = 1;
	///

	float3 computedColor = float3(0.0f, 0.0f, 0.0f);

	float coneStep = lengthOfCone / maximumIterations;

	float iteration0 = maximumIterations / 32.0f;
	float iteration1 = maximumIterations / 32.0f;
	float iteration2 = maximumIterations / 16.0f;
	float iteration3 = maximumIterations / 8.0f;
	float iteration4 = maximumIterations / 4.0f;
	float iteration5 = maximumIterations / 2.0f;



	blueNoise.xy *= 0.125;
	blueNoise.z *= 0.125;
	blueNoise.z -= blueNoise.z * 2;

	float3 coneOrigin = worldPosition + (coneDirection * coneStep * iteration0);

	float3 currentPosition = coneOrigin;
	float4 currentVoxelInfo = float4(0.0f, 0.0f, 0.0f, 0.0f);

	float hitFound = 0.0f;

	float skyVisibility = 1.0f;
	float occlusion;
	float4 gi = float4(0, 0, 0, 0);

	float shadowMap = tex2D(_CameraGBufferTexture1, uv).a;

	//float3 voxelBufferCoord = float3(0, 0, 0);

	// Sample voxel grid 1
	for (float i1 = 0.0f; i1 < iteration1; i1 += 1.0f)
	{
		currentPosition += (coneStep * coneDirection);

		float fi = ((float)i1 + blueNoise.y * StochasticSampling) / maximumIterations;
		fi = lerp(fi, 1.0, 0.0);

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
		//float coneDistance = currentPosition.z;

		float coneSize = coneDistance * coneWidth;

		if (hitFound < 0.9f)
		{
			currentVoxelInfo = GetVoxelInfo1(GetVoxelPosition(currentPosition));
			if (currentVoxelInfo.a > 0.0f)
			{
				hitFound = 1.0f;
				voxelBufferCoord = GetVoxelPosition(currentPosition);// *(coneLength * 1.12 * coneDistance);
			}
			currentVoxelInfo.a = shadowMap;

			occlusion = skyVisibility * skyVisibility;

			float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

			currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, NearOcclusionStrength);
			currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
			gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 * (1.0 - fi * fi) * SunlightInjection;

			skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * OcclusionStrength * (1.0 + coneDistance * FarOcclusionStrength)), 1.0 * OcclusionPower);
		}
	}

	// Sample voxel grid 2
	for (float i2 = 0.0f; i2 < iteration2; i2 += 1.0f)
	{
		currentPosition += (coneStep * coneDirection);

		float fi = ((float)i2 + blueNoise.y * StochasticSampling) / maximumIterations;
		fi = lerp(fi, 1.0, 0.0);

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
		//float coneDistance = currentPosition.z;

		float coneSize = coneDistance * coneWidth * 10.3;

		if (hitFound < 0.9f)
		{
			currentVoxelInfo = GetVoxelInfo2(GetVoxelPosition(currentPosition));
			if (currentVoxelInfo.a > 0.0f)
			{
				hitFound = 1.0f;
				voxelBufferCoord = GetVoxelPosition(currentPosition);// *(coneLength * 1.12 * coneDistance);
			}
			currentVoxelInfo.a = shadowMap;

			occlusion = skyVisibility * skyVisibility;

			float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

			currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, NearOcclusionStrength);
			currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
			gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 * (1.0 - fi * fi);

			skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * OcclusionStrength * (1.0 + coneDistance * FarOcclusionStrength)), 1.0 * OcclusionPower);
		}
	}

	// Sample voxel grid 3
	for (float i3 = 0.0f; i3 < iteration3; i3 += 1.0f)
	{
		currentPosition += (coneStep * coneDirection);

		float fi = ((float)i3 + blueNoise.y * StochasticSampling) / maximumIterations;
		fi = lerp(fi, 1.0, 0.0);

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
		//float coneDistance = currentPosition.z;

		float coneSize = coneDistance * coneWidth * 10.3;

		if (hitFound < 0.9f)
		{
			currentVoxelInfo = GetVoxelInfo3(GetVoxelPosition(currentPosition));
			if (currentVoxelInfo.a > 0.0f)
			{
				hitFound = 1.0f;
				voxelBufferCoord = GetVoxelPosition(currentPosition);// *(coneLength * 1.12 * coneDistance);
			}
			currentVoxelInfo.a = shadowMap;

			occlusion = skyVisibility * skyVisibility;

			float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

			currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, NearOcclusionStrength);
			currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
			gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 * (1.0 - fi * fi);

			skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * OcclusionStrength * (1.0 + coneDistance * FarOcclusionStrength)), 1.0 * OcclusionPower);
		}
	}

	// Sample voxel grid 4
	for (float i4 = 0.0f; i4 < iteration4; i4 += 1.0f)
	{
		currentPosition += (coneStep * coneDirection);

		float fi = ((float)i4 + blueNoise.y * StochasticSampling) / maximumIterations;
		fi = lerp(fi, 1.0, 0.0);

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
		//float coneDistance = currentPosition.z;

		float coneSize = coneDistance * coneWidth * 10.3;

		if (hitFound < 0.9f)
		{
			currentVoxelInfo = GetVoxelInfo4(GetVoxelPosition(currentPosition));
			if (currentVoxelInfo.a > 0.0f)
			{
				hitFound = 1.0f;
				voxelBufferCoord = GetVoxelPosition(currentPosition);// *(coneLength * 1.12 * coneDistance);
			}
			currentVoxelInfo.a = shadowMap;

			occlusion = skyVisibility * skyVisibility;

			float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

			currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, NearOcclusionStrength);
			currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
			gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 * (1.0 - fi * fi);

			skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * OcclusionStrength * (1.0 + coneDistance * FarOcclusionStrength)), 1.0 * OcclusionPower);
		}
	}

	// Sample voxel grid 5
	for (float i5 = 0.0f; i5 < iteration5; i5 += 1.0f)
	{
		currentPosition += (coneStep * coneDirection);

		float fi = ((float)i5 + blueNoise.y * StochasticSampling) / maximumIterations;
		fi = lerp(fi, 1.0, 0.0);

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;
		//float coneDistance = currentPosition.z;

		float coneSize = coneDistance * coneWidth * 10.3;

		if (hitFound < 0.9f)
		{
			currentVoxelInfo = GetVoxelInfo5(GetVoxelPosition(currentPosition));
			if (currentVoxelInfo.a > 0.0f)
			{
				hitFound = 1.0f;
				voxelBufferCoord = GetVoxelPosition(currentPosition);// *(coneLength * 1.12 * coneDistance);
			}
			currentVoxelInfo.a = shadowMap;

			occlusion = skyVisibility * skyVisibility;

			float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

			currentVoxelInfo.a *= lerp(saturate(coneSize / 1.0), 1.0, NearOcclusionStrength);
			currentVoxelInfo.a *= (0.8 / (fi * fi * 2.0 + 0.15));
			gi.rgb += currentVoxelInfo.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 * (1.0 - fi * fi);

			skyVisibility *= pow(saturate(1.0 - currentVoxelInfo.a * OcclusionStrength * (1.0 + coneDistance * FarOcclusionStrength)), 1.0 * OcclusionPower);
		}
	}

	//gi.rgb /= 32;
	//skyVisibility /= 32;

	//Calculate lighting attribution
	float3 worldSpaceNormal = GetWorldNormal(uv);

	float NdotL = pow(saturate(dot(worldSpaceNormal, coneDirection) * 1.0 - 0.0), 0.5);

	gi *= NdotL;
	skyVisibility *= NdotL;
	skyVisibility *= lerp(saturate(dot(coneDirection, float3(0.0, 1.0, 0.0)) * 10.0 + 0.0), 1.0, SEGISphericalSkylight);
	float3 skyColor = float3(0.0, 0.0, 0.0);

	float upGradient = saturate(dot(coneDirection, float3(0.0, 1.0, 0.0)));
	float sunGradient = saturate(dot(coneDirection, -SEGISunlightVector.xyz));
	skyColor += lerp(SEGISkyColor.rgb * 1.0, SEGISkyColor.rgb * 0.5, pow(upGradient, (0.5).xxx));
	skyColor += GISunColor.rgb * pow(sunGradient, (4.0).xxx) * SunlightInjection;

	gi.rgb *= GIGain;// *0.15;

	gi.rgb += (skyColor * skyVisibility);

	computedColor.rgb = gi.rgb;

	return computedColor;
}

inline float3 ComputeIndirectContribution(float3 worldPosition, float3 worldNormal, float2 uv, float depth)
{
	float3 gi = float3(0.0f, 0.0f, 0.0f);

	float2 noiseCoord = (uv.xy * _MainTex_TexelSize.zw) / (64.0).xx;
	float4 blueNoise = tex2Dlod(NoiseTexture, float4(noiseCoord, 0.0, 0.0)).x;
	blueNoise *= (1 - depth);


	float fi = (float)tracedTexture1UpdateCount + blueNoise.x * StochasticSampling;
	float fiN = fi / 65;
	float longitude = gAngle * fi;
	float latitude = asin(fiN * 2.0 - 1.0);

	float3 kernel;
	kernel.x = cos(latitude) * cos(longitude);
	kernel.z = cos(latitude) * sin(longitude);
	kernel.y = sin(latitude);

	float3 randomVector = normalize(kernel);
	float3 direction1 = normalize(cross(worldNormal, randomVector));
	float3 coneDirection2 = lerp(direction1, worldNormal, 0.3333f);

	float3 voxelBufferCoord;
	gi = ConeTrace(worldPosition, kernel.xyz, uv, blueNoise, voxelBufferCoord);

	voxelBufferCoord.x += blueNoise.x * StochasticSampling;
	voxelBufferCoord.y += blueNoise.y * StochasticSampling;
	voxelBufferCoord.z += blueNoise.z * StochasticSampling;
	double index = voxelBufferCoord.x * (256) * (256) + voxelBufferCoord.y * (256) + voxelBufferCoord.z;
	tracedBuffer1[index] += float4(gi, 1);


	gi = ConeTrace(worldPosition, worldNormal, uv, blueNoise, voxelBufferCoord);


	float4 cachedResult = float4(tracedBuffer0[index]) * 0.000003;

	//Average HSV values independantly for prettier result
	half4 cachedHSV = float4(rgb2hsv(cachedResult.rgb), 0);
	half4 giHSV = float4(rgb2hsv(gi), 0);
	gi.rgb *= cachedResult.rgb;
	giHSV.rg = float2(rgb2hsv(gi).r, lerp(cachedHSV.g, giHSV.g, 0.5));
	gi.rgb = hsv2rgb(giHSV);

	

	return gi;
}

float4 frag_lighting(v2f i) : SV_Target
{
	float3 directLighting = tex2D(_MainTex, i.uv).rgb;

	float4 gBufferSample = tex2D(_CameraGBufferTexture0, i.uv);
	float3 albedo = gBufferSample.rgb;
	float ao = gBufferSample.a;

	//directLighting = gBufferSample.rgb;

	float3 emissive = tex2D(_CameraGBufferTexture1, i.uv).rgb;


	// read low res depth and reconstruct world position
	float depth = 1 - GetDepthTexture(i.uv);

	//linearise depth		
	float lindepth = Linear01Depth(depth);

	//get view and then world positions		
	float4 viewPos = float4(i.cameraRay.xyz * lindepth, 1.0f);
	float3 worldPos = mul(InverseViewMatrix, viewPos).xyz;

	float3 worldSpaceNormal = 1 - GetWorldNormal(i.uv);

	//albedo * albedoTex.a * albedoTex.rgb;
		
	float3 indirectLighting = ((indirectLightingStrength * albedo + emissive) / PI) * ComputeIndirectContribution(worldPos, worldSpaceNormal, i.uv, 1 - depth);
	if (VisualiseGI) indirectLighting = ComputeIndirectContribution(worldPos, worldSpaceNormal, i.uv, 1 - depth);

	//indirectLighting = ao;

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