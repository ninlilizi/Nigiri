Shader "Hidden/Nigiri_Blit_CameraDepthTexture"
{
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
	{
		CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 5.0

#include "UnityCG.cginc"

		struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float2 uv : TEXCOORD0;
		float4 vertex : SV_POSITION;
	};

	sampler2D _CameraDepthTexture;
	half4 _CameraDepthTexture_ST;
	half4 _MainTex_ST;

	int stereoEnabled;
	int debug;
	//float eyeDistance;

	v2f vert(appdata v)
	{
		if (debug) {
		
			v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.uv = v.uv;			
					return o;
		}
		else {
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.uv = v.uv;

			if (stereoEnabled) o.uv.x *= 0.5;

			return o;
		}
	}

	fixed4 frag(v2f i) : SV_Target
	{
		if (debug) return Linear01Depth(tex2D(_CameraDepthTexture,UnityStereoTransformScreenSpaceTex(i.uv)));
		else {
		if (stereoEnabled)
		{
			if (i.uv.x < 0.5) return Linear01Depth(tex2D(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(i.uv)));
			else return float4(0,0,0,0);
		}
		else
			return Linear01Depth(tex2D(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(i.uv)));
		}
	}
		ENDCG
	}
	}
}

