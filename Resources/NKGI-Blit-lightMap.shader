// Upgrade NOTE: commented out 'sampler2D unity_Lightmap', a built-in variable
// Upgrade NOTE: replaced tex2D unity_Lightmap with UNITY_SAMPLE_TEX2D

Shader "Hidden/NKGI-Blit-lightMap"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
		[NoScaleOffset] _NoiseMap0("Noise 0", 2D) = "white" {}
		[NoScaleOffset] _NoiseMap1("Noise 1", 2D) = "white" {}
		[NoScaleOffset] _NoiseMap2("Noise 2", 2D) = "white" {}
		[NoScaleOffset] _NoiseMap3("Noise 3", 2D) = "white" {}
		[NoScaleOffset] _NoiseMap4("Noise 4", 2D) = "white" {}
		[NoScaleOffset] _NoiseMap5("Noise 5", 2D) = "white" {}
		[NoScaleOffset] _NoiseMap6("Noise 6", 2D) = "white" {}
		[NoScaleOffset] _NoiseMap7("Noise 7", 2D) = "white" {}
    }

    CGINCLUDE

        #include "UnityCG.cginc"
        //#include "Common.cginc"

        struct Varyings
        {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
			float3 worldPos : TEXCOORD1;
        };

		sampler2D _MainTex;
		half4 _MainTex_ST;

		sampler2D _CameraDepthTexture;
		sampler2D _CameraGBufferTexture0;
		sampler2D _CameraGBufferTexture1;
		sampler2D _CameraGBufferTexture2;
		half4 _EmissionColor;

		sampler2D _NoiseMap0;
		sampler2D _NoiseMap1;
		sampler2D _NoiseMap2;
		sampler2D _NoiseMap3;
		sampler2D _NoiseMap4;
		sampler2D _NoiseMap5;
		sampler2D _NoiseMap6;
		sampler2D _NoiseMap7;

        Varyings VertBlit(appdata_img v)
        {
            Varyings o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = UnityStereoScreenSpaceUVAdjust(v.texcoord, _MainTex_ST);
			o.worldPos.xyz = mul(unity_ObjectToWorld, v.vertex);
            return o;
        }

		float2 rand(float2 coord)
		{
			float noiseX = saturate(frac(sin(dot(coord, float2(12.9898, 78.223))) * 43758.5453));
			float noiseY = saturate(frac(sin(dot(coord, float2(12.9898, 78.223)*2.0)) * 43758.5453));

			return float2(noiseX, noiseY);
		}

		// http://ericpolman.com/2016/04/13/reflective-shadow-maps-part-2-the-implementation/
		float3 DoReflectiveShadowMapping(float3 P, bool divideByW, float3 N, float4 uv)
		{
			float rsmRMax = 0.07;
			uint rsmSampleCount = 4;
			float rsmIntensity = 1;

			float4 textureSpacePosition = mul(_WorldSpaceCameraPos.xyz, float4(P, 1.0));
			if (divideByW) textureSpacePosition.xyz /= textureSpacePosition.w;

			float3 indirectIllumination = float3(0, 0, 0);
			float rMax = rsmRMax;

			for (uint i = 0; i < rsmSampleCount; ++i)
			{
				float2 rnd = float2(0, 0);
				if (i == 0) rnd = tex2D(_NoiseMap0, uv.xy).xyz;
				else if (i == 1) rnd = tex2D(_NoiseMap1, uv).xyz;
				else if (i == 2) rnd = tex2D(_NoiseMap2, uv).xyz;
				else if (i == 3) rnd = tex2D(_NoiseMap3, uv).xyz;
				else if (i == 4) rnd = tex2D(_NoiseMap4, uv).xyz;
				else if (i == 5) rnd = tex2D(_NoiseMap5, uv).xyz;
				else if (i == 6) rnd = tex2D(_NoiseMap6, uv).xyz;
				else if (i == 7) rnd = tex2D(_NoiseMap7, uv).xyz;

				float2 coords = textureSpacePosition.xy + rMax * rnd;

				float3 vplPositionWS = tex2D(_CameraDepthTexture, coords.xy).xyz;
				float3 vplNormalWS = tex2D(_CameraGBufferTexture2, coords.xy).xyz;
				float3 RSMflux = tex2D(_CameraGBufferTexture1, coords.xy).xyz;

				float3 result = RSMflux * ((max(0, dot(vplNormalWS, P - vplPositionWS)) * (max(0, dot(N, vplPositionWS - P)))) / pow(length(P - vplPositionWS), 4));

				result *= rnd.x * rnd.x;
				indirectIllumination += result;
			}

			return saturate(indirectIllumination * rsmIntensity);
		}
		//#endif

        half4 FragBlit(Varyings i) : SV_Target
        {
            half3 col = tex2D(_MainTex, i.uv).rgb;
			half3 emission = tex2D(_CameraGBufferTexture1, i.uv).rgb;
			if (emission.r < 0.1 && emission.g < 0.1 && emission.b < 0.1) emission.rgb = float3(0, 0, 0);

			float3 rsmFlux = DoReflectiveShadowMapping(i.worldPos, true, tex2D(_CameraGBufferTexture2, i.uv.xy).xyz, float4(i.uv.xy, 0, 0));


			float4 mixedcols = float4(rsmFlux + emission, 1);
            return mixedcols;
        }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

                #pragma vertex VertBlit
                #pragma fragment FragBlit

            ENDCG
        }
    }
}
