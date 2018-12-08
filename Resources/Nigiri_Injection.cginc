// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

#include "UnityCG.cginc"
#include "UnityPBSLighting.cginc"
#include "AutoLight.cginc"


sampler2D _MainTex;
sampler2D _MetallicMap;
float _Metallic;
float _Smoothness;
float3 _Emission;
float EmissiveStrength;
sampler2D _CameraDepthTexture;
float worldVolumeBoundary;
int highestVoxelResolution;
uniform uint3					gridOffset;

uniform RWStructuredBuffer<uint> voxelUpdateBuffer : register(u5);
//uniform RWStructuredBuffer<float4> positionBuffer : register(u6);
uniform RWTexture3D<half4> voxelGrid : register(u6);
uniform RWTexture3D<half> voxelGridA : register(u7);

float4x4 InverseProjectionMatrix;

UNITY_INSTANCING_BUFFER_START(InstanceProperties)
UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
#define _Color_arr InstanceProperties
UNITY_INSTANCING_BUFFER_END(InstanceProperties)

struct vertOutput
{
	float4 pos : SV_POSITION;	// Clip space
	fixed4 color : COLOR;		// Vertex colour
	float2 texcoord : TEXCOORD0;	// UV data

	float3 wPos : TEXCOORD1;	// World position
	float4 sPos : TEXCOORD2;	// Screen position
	float3 cPos : TEXCOORD3;	// Object center in world

	float3 normal : TEXCOORD4;

	float4 cameraRay : TEXCOORD5;

	float3 tangent : TEXCOORD6;
	float3 binormal : TEXCOORD7;
};

float3 CreateBinormal(float3 normal, float3 tangent, float binormalSign) {
	return cross(normal, tangent.xyz) *
		(binormalSign * unity_WorldTransformParams.w);
}

vertOutput vert(appdata_full v)
{
	vertOutput o;
	o.pos = UnityObjectToClipPos(v.vertex);
	o.color = v.color;
	o.texcoord = v.texcoord;

	o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
	o.sPos = ComputeScreenPos(o.pos);
	o.cPos = mul(unity_ObjectToWorld, half4(0, 0, 0, 1));

	o.normal = UnityObjectToWorldNormal(v.normal);

	//transform clip pos to view space
	float4 clipPos = float4(v.texcoord.xy * 2.0f - 1.0f, 1.0f, 1.0f);
	float4 cameraRay = mul(InverseProjectionMatrix, clipPos);
	o.cameraRay = cameraRay / cameraRay.w;

	float4 clipSpace = UnityObjectToClipPos(v.vertex);
	clipSpace.xy /= clipSpace.w;
	clipSpace.xy = 0.5*(clipSpace.xy + 1.0);

	o.tangent = UnityObjectToWorldDir(v.tangent.xyz);
	o.binormal = CreateBinormal(v.normal, v.tangent, v.tangent.w);

	return o;
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

inline uint3 GetVoxelPosition(float3 worldPosition)
{
	worldPosition = worldPosition.xyz - (int3)gridOffset.xyz;
	float3 cascadeBoundary = worldVolumeBoundary * 0.33f;

	float3 encodedPosition = worldPosition / cascadeBoundary;
	encodedPosition += float3(1.0f, 1.0f, 1.0f);
	encodedPosition /= 2.0f;
	uint3 voxelPosition = (uint3)(encodedPosition * highestVoxelResolution);
	return voxelPosition;
}

UnityIndirect CreateIndirectLight(vertOutput i, float3 viewDir) {
	UnityIndirect indirectLight;
	indirectLight.diffuse = 0;
	indirectLight.specular = 0;

#if defined(VERTEXLIGHT_ON)
	indirectLight.diffuse = i.vertexLightColor;
#endif

#if defined(FORWARD_BASE_PASS) || defined(DEFERRED_PASS)
#if defined(LIGHTMAP_ON)
	indirectLight.diffuse =
		DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, i.lightmapUV));

#if defined(DIRLIGHTMAP_COMBINED)
	float4 lightmapDirection = UNITY_SAMPLE_TEX2D_SAMPLER(
		unity_LightmapInd, unity_Lightmap, i.lightmapUV
	);
	indirectLight.diffuse = DecodeDirectionalLightmap(
		indirectLight.diffuse, lightmapDirection, i.normal
	);
#endif

	ApplySubtractiveLighting(i, indirectLight);
#endif

#if defined(DYNAMICLIGHTMAP_ON)
	float3 dynamicLightDiffuse = DecodeRealtimeLightmap(
		UNITY_SAMPLE_TEX2D(unity_DynamicLightmap, i.dynamicLightmapUV)
	);

#if defined(DIRLIGHTMAP_COMBINED)
	float4 dynamicLightmapDirection = UNITY_SAMPLE_TEX2D_SAMPLER(
		unity_DynamicDirectionality, unity_DynamicLightmap,
		i.dynamicLightmapUV
	);
	indirectLight.diffuse += DecodeDirectionalLightmap(
		dynamicLightDiffuse, dynamicLightmapDirection, i.normal
	);
#else
	indirectLight.diffuse += dynamicLightDiffuse;
#endif
#endif

#if !defined(LIGHTMAP_ON) && !defined(DYNAMICLIGHTMAP_ON)
#if UNITY_LIGHT_PROBE_PROXY_VOLUME
	if (unity_ProbeVolumeParams.x == 1) {
		indirectLight.diffuse = SHEvalLinearL0L1_SampleProbeVolume(
			float4(i.normal, 1), i.wPos
		);
		indirectLight.diffuse = max(0, indirectLight.diffuse);
#if defined(UNITY_COLORSPACE_GAMMA)
		indirectLight.diffuse =
			LinearToGammaSpace(indirectLight.diffuse);
#endif
	}
	else {
		indirectLight.diffuse +=
			max(0, ShadeSH9(float4(i.normal, 1)));
	}
#else
	indirectLight.diffuse += max(0, ShadeSH9(float4(i.normal, 1)));
#endif
#endif

	float3 reflectionDir = reflect(-viewDir, i.normal);
	Unity_GlossyEnvironmentData envData;
	envData.roughness = 1 - GetSmoothness(i);
	envData.reflUVW = BoxProjection(
		reflectionDir, i.wPos.xyz,
		unity_SpecCube0_ProbePosition,
		unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax
	);
	float3 probe0 = Unity_GlossyEnvironment(
		UNITY_PASS_TEXCUBE(unity_SpecCube0), unity_SpecCube0_HDR, envData
	);
	envData.reflUVW = BoxProjection(
		reflectionDir, i.wPos.xyz,
		unity_SpecCube1_ProbePosition,
		unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax
	);
#if UNITY_SPECCUBE_BLENDING
	float interpolator = unity_SpecCube0_BoxMin.w;
	UNITY_BRANCH
		if (interpolator < 0.99999) {
			float3 probe1 = Unity_GlossyEnvironment(
				UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1, unity_SpecCube0),
				unity_SpecCube0_HDR, envData
			);
			indirectLight.specular = lerp(probe1, probe0, interpolator);
		}
		else {
			indirectLight.specular = probe0;
		}
#else
	indirectLight.specular = probe0;
#endif

	float occlusion = GetOcclusion(i);
	indirectLight.diffuse *= occlusion;
	indirectLight.specular *= occlusion;

#if defined(DEFERRED_PASS) && UNITY_ENABLE_REFLECTION_BUFFERS
	indirectLight.specular = 0;
#endif
#endif

	return indirectLight;
}

float3 GetEmission(vertOutput i) {
#if defined(FORWARD_BASE_PASS) || defined(DEFERRED_PASS)
#if defined(_EMISSION_MAP)
	return tex2D(_EmissionMap, i.uv.xy) * _Emission;
#else
	return _Emission;
#endif
#else
	return 0;
#endif
}

float3 GetAlbedo(vertOutput i) {
	float3 albedo =
		tex2D(_MainTex, i.texcoord.xy).rgb * UNITY_ACCESS_INSTANCED_PROP(_Color_arr, _Color).rgb;
#if defined (_DETAIL_ALBEDO_MAP)
	float3 details = tex2D(_DetailTex, i.uv.zw) * unity_ColorSpaceDouble;
	albedo = lerp(albedo, albedo * details, GetDetailMask(i));
#endif
	return albedo;
}

float GetMetallic(vertOutput i) {
#if defined(_METALLIC_MAP)
	return tex2D(_MetallicMap, i.texcoord.xy).r;
#else
	return _Metallic;
#endif
}

float GetSmoothness(vertOutput i) {
	float smoothness = 1;
#if defined(_SMOOTHNESS_ALBEDO)
	smoothness = tex2D(_MainTex, i.texcoord.xy).a;
#elif defined(_SMOOTHNESS_METALLIC) && defined(_METALLIC_MAP)
	smoothness = tex2D(_MetallicMap, i.uv.xy).a;
#endif
	return smoothness * _Smoothness;
}

float3 GetTangentSpaceNormal(vertOutput i) {
	float3 normal = float3(0, 0, 1);
#if defined(_NORMAL_MAP)
	normal = UnpackScaleNormal(tex2D(_BumpMap, i.uv.xy), _BumpScale);
#endif
#if defined(_DETAIL_NORMAL_MAP)
	float3 detailNormal =
		UnpackScaleNormal(
			tex2D(_DetailNormalMap, i.uv.zw), _DetailBumpScale
		);
	detailNormal = lerp(float3(0, 0, 1), detailNormal, GetDetailMask(i));
	normal = BlendNormals(normal, detailNormal);
#endif
	return normal;
}

void InitializeFragmentNormal(vertOutput i) {
	float3 tangentSpaceNormal = GetTangentSpaceNormal(i);
	float3 binormal = i.binormal;

	i.normal = normalize(
		tangentSpaceNormal.x * i.tangent +
		tangentSpaceNormal.y * binormal +
		tangentSpaceNormal.z * i.normal
	);
}

uint twoD2oneD(float2 coord)
{
	return coord.x + 1024 * coord.y;
}

uint threeD2oneD(float3 coord)
{
	return coord.z * (highestVoxelResolution * highestVoxelResolution) + (coord.y * highestVoxelResolution) + coord.x;
}

float4x4 InverseViewMatrix;

struct FragmentOutput {
	float4 mrt0 : SV_Target0;
	float4 mrt1 : SV_Target1;
};

FragmentOutput frag(vertOutput i)
{
	float3 color = (0).xxx;
	float4 newColor = (0).xxxx;
	//if (_Emission.r > 0 || _Emission.g > 0 || _Emission.b > 0)
	//{
		float3 index3d = GetVoxelPosition(i.wPos);
		if (index3d.x < 0 || index3d.x >= 256 ||
			index3d.y < 0 || index3d.y >= 256 ||
			index3d.z < 0 || index3d.z >= 256)
		{
			//finalColor = float4(1, 0, 1, 1);
		}
		else
		{
			/*float3 index3d0 = float3(index3d.x + 1, index3d.y, index3d.z);
			float3 index3d1 = float3(index3d.x - 1, index3d.y, index3d.z);
			float3 index3d2 = float3(index3d.x, index3d.y + 1, index3d.z);
			float3 index3d3 = float3(index3d.x, index3d.y - 1, index3d.z);
			float3 index3d4 = float3(index3d.x, index3d.y, index3d.z + 1);
			float3 index3d5 = float3(index3d.x, index3d.y, index3d.z - 1);*/

			InitializeFragmentNormal(i);

			float3 specularTint;
			float oneMinusReflectivity;
			float3 viewDir = normalize(_WorldSpaceCameraPos - i.wPos.xyz);
			float3 albedo = DiffuseAndSpecularFromMetallic(
				GetAlbedo(i), GetMetallic(i), specularTint, oneMinusReflectivity
			);

			//Nin - NKGI - Sample shadowmap to pass to GI
			float3 lightColor1 = _LightColor0.rgb;
			float3 lightDir = _WorldSpaceLightPos0.xyz;
			float4 colorTex = tex2D(_MainTex, i.texcoord.xy);
			UNITY_LIGHT_ATTENUATION(atten, i, _WorldSpaceLightPos0.xyz);
			float3 N = float3(0.0f, 1.0f, 0.0f);
			float  NL = saturate(dot(N, lightDir));
			float3 shadowColor = albedo.rgb * lightColor1 * NL * atten;
			///
			
			newColor = float4(shadowColor, 0) + (float4(GetEmission(i) * EmissiveStrength, 1));

			uint index1d = threeD2oneD(index3d);
			if (newColor.r > 0 || newColor.g > 0 || newColor.b > 0) {
				//lightMapBuffer[index1d] = EncodeRGBAuint(newColor);
				voxelGrid[index3d] = RGBMEncode(lerp(newColor.rgb, RGBMDecode(voxelGrid[index3d]), 0.5));
				voxelGridA[index3d] = lerp(newColor.a, voxelGridA[index3d], 0.5);
				voxelUpdateBuffer[index1d] = 1;
			}

			//float3 position = float3(i.wPos.x + worldVolumeBoundary, i.wPos.y + worldVolumeBoundary, i.wPos.z + worldVolumeBoundary);
			//position /= (2.0 * worldVolumeBoundary);

			//positionBuffer[twoD2oneD(i.sPos.xy)] = float4(position, 1);

			/*newColor *= 0.125;
			index1d = threeD2oneD(index3d0); lightMapBuffer[index1d] = EncodeRGBAuint(newColor + DecodeRGBAuint(lightMapBuffer[index1d]));
			index1d = threeD2oneD(index3d1); lightMapBuffer[index1d] = EncodeRGBAuint(newColor + DecodeRGBAuint(lightMapBuffer[index1d]));
			index1d = threeD2oneD(index3d2); lightMapBuffer[index1d] = EncodeRGBAuint(newColor + DecodeRGBAuint(lightMapBuffer[index1d]));
			index1d = threeD2oneD(index3d3); lightMapBuffer[index1d] = EncodeRGBAuint(newColor + DecodeRGBAuint(lightMapBuffer[index1d]));
			index1d = threeD2oneD(index3d4); lightMapBuffer[index1d] = EncodeRGBAuint(newColor + DecodeRGBAuint(lightMapBuffer[index1d]));
			index1d = threeD2oneD(index3d5); lightMapBuffer[index1d] = EncodeRGBAuint(newColor + DecodeRGBAuint(lightMapBuffer[index1d]));*/
		}
	//}
	//else albedo = float3(0, 0, 0);

	//float3 position = float3(i.wPos.x + worldVolumeBoundary, i.wPos.y + worldVolumeBoundary, i.wPos.z + worldVolumeBoundary);
	//position /= (2.0 * worldVolumeBoundary);

	FragmentOutput output;
	output.mrt0 = newColor;
	output.mrt1 = float4(i.wPos, 1);
	return output;

}

