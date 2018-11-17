#include "UnityCG.cginc"
#include "UnityPBSLighting.cginc"
#include "AutoLight.cginc"


sampler2D _MainTex;
sampler2D _MetallicMap;
float4 _Color;
float _Metallic;
float _Smoothness;
sampler2D _CameraDepthTexture;
float worldVolumeBoundary;
int highestVoxelResolution;
sampler2D positionTexture;

uniform RWStructuredBuffer<uint> lightMapBuffer : register(u5);

float4x4 InverseProjectionMatrix;

struct vertOutput
{
	float4 pos : SV_POSITION;	// Clip space
	fixed4 color : COLOR;		// Vertex colour
	float2 texcoord : TEXCOORD0;	// UV data

	float3 wPos : TEXCOORD1;	// World position
	float4 sPos : TEXCOORD2;	// Screen position
	float3 cPos : TEXCOORD3;	// Object center in world

	float3 normal : TEXCOORD4;
};


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

inline uint3 GetVoxelPosition(float3 worldPosition)
{
	float3 encodedPosition = worldPosition / worldVolumeBoundary;
	encodedPosition += float3(1.0f, 1.0f, 1.0f);
	encodedPosition /= 2.0f;
	uint3 voxelPosition = (uint3)(encodedPosition * highestVoxelResolution);
	return voxelPosition;
}

float FadeShadows(vertOutput i, float attenuation) {
#if HANDLE_SHADOWS_BLENDING_IN_GI || ADDITIONAL_MASKED_DIRECTIONAL_SHADOWS
	// UNITY_LIGHT_ATTENUATION doesn't fade shadows for us.
#if ADDITIONAL_MASKED_DIRECTIONAL_SHADOWS
	attenuation = SHADOW_ATTENUATION(i);
#endif
	float viewZ =
		dot(_WorldSpaceCameraPos - i.wPos, UNITY_MATRIX_V[2].xyz);
	float shadowFadeDistance =
		UnityComputeShadowFadeDistance(i.wPos, viewZ);
	float shadowFade = UnityComputeShadowFade(shadowFadeDistance);
	float bakedAttenuation =
		UnitySampleBakedOcclusion(i.lightmapUV, i.wPos);
	attenuation = UnityMixRealtimeAndBakedShadows(
		attenuation, bakedAttenuation, shadowFade
	);
#endif

	return attenuation;
}

UnityLight CreateLight(vertOutput i) {
	UnityLight light;

#if defined(DEFERRED_PASS) || SUBTRACTIVE_LIGHTING
	light.dir = float3(0, 1, 0);
	light.color = 0;
#else
#if defined(POINT) || defined(POINT_COOKIE) || defined(SPOT)
	light.dir = normalize(_WorldSpaceLightPos0.xyz - i.wPos.xyz);
#else
	light.dir = _WorldSpaceLightPos0.xyz;
#endif

	UNITY_LIGHT_ATTENUATION(attenuation, i, i.wPos.xyz);
	attenuation = FadeShadows(i, attenuation);

	light.color = _LightColor0.rgb * attenuation;
#endif

	return light;
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

half4 frag(vertOutput i) : COLOR
{
	i.sPos.xy /= i.sPos.w;
	float3 specularTint;
	float oneMinusReflectivity;
	float3 viewDir = normalize(_WorldSpaceCameraPos - i.wPos.xyz);
	float3 albedo = DiffuseAndSpecularFromMetallic(
		GetAlbedo(i), GetMetallic(i), specularTint, oneMinusReflectivity
	);
	float3 emission = GetEmission(i);

	float4 color = UNITY_BRDF_PBS(
		albedo, specularTint,
		oneMinusReflectivity, GetSmoothness(i),
		i.normal, viewDir,
		CreateLight(i), CreateIndirectLight(i, viewDir)
	);


	//Nin - NKGI - Sample shadowmap to pass to GI
	float3 lightColor1 = _LightColor0.rgb;
	float3 lightDir = _WorldSpaceLightPos0.xyz;
	UNITY_LIGHT_ATTENUATION(atten, i, _WorldSpaceLightPos0.xyz);
	float3 N = i.normal;
	float  NL = saturate(dot(N, lightDir));
	float3 shadowColor = albedo * lightColor1 * NL * atten;
	///

	float4 finalColor = float4((shadowColor.rgb * color.rgb * 128) + emission.rgb, 1);

	float3 index3d = GetVoxelPosition(i.wPos);
	if (index3d.x < 0 || index3d.x >= 256 ||
		index3d.y < 0 || index3d.y >= 256 ||
		index3d.z < 0 || index3d.z >= 256)
	{
		finalColor = float4(1, 0, 0, 0);
	}
	else
	{
		double index1d = (index3d.z * highestVoxelResolution * highestVoxelResolution) + (index3d.y * highestVoxelResolution) + index3d.x;
		lightMapBuffer[index1d] = EncodeRGBAuint(float4(finalColor.rgb, 0.001) + DecodeRGBAuint(lightMapBuffer[index1d]));
	}

return finalColor;
}

