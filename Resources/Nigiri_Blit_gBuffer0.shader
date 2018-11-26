Shader "Hidden/Nigiri_Blit_gBuffer0"
{
	CGINCLUDE

#include "UnityCG.cginc"
		//#include "Common.cginc"


		struct Varyings
	{
		float2 uv : TEXCOORD0;
		float4 vertex : SV_POSITION;
	};

	sampler2D _CameraGBufferTexture0;
	half4 _CameraGBufferTexture0_ST;


	Varyings VertBlit(appdata_img v)
	{
		Varyings o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = TransformStereoScreenSpaceTex(v.texcoord, _CameraGBufferTexture0_ST);
		return o;
	}


	half4 FragBlit(Varyings i) : SV_Target
	{
		return tex2D(_CameraGBufferTexture0, i.uv);
	}

		ENDCG

		SubShader
	{
		//Cull Off ZWrite Off ZTest Always

			Pass
		{
			CGPROGRAM

				#pragma vertex VertBlit
				#pragma fragment FragBlit

			ENDCG
		}

	}
}