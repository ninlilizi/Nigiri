Shader "Hidden/Nigiri_Injection" 
{

	Properties
	{
		_Color("Tint", Color) = (1, 1, 1, 1)
		_MainTex("Albedo", 2D) = "white" {}

		[NoScaleOffset] _MetallicMap("Metallic", 2D) = "white" {}
		[Gamma] _Metallic("Metallic", Range(0, 1)) = 0
		_Smoothness("Smoothness", Range(0, 1)) = 0.1

		[NoScaleOffset] _EmissionMap("Emission", 2D) = "black" {}
		_Emission("Emission", Color) = (0, 0, 0)
	}

	SubShader
	{
			Pass
		{
			Tags
			{
				"LightMode" = "ForwardBase"
			}

			Blend[_SrcBlend][_DstBlend]
			//ZWrite[_ZWrite]
			ZWrite On
			Cull Off
			ZTest Always

			CGPROGRAM
				#pragma target 5.0
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_instancing

				#include "Nigiri_Injection.cginc"
				ENDCG
		}

		Pass
		{
			Tags
			{
				"LightMode" = "ForwardAdd"
			}

			Blend[_SrcBlend][_DstBlend]
			//ZWrite[_ZWrite]
			ZWrite On
			Cull Off
			ZTest Always

			CGPROGRAM
				#pragma target 5.0
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_instancing
				#pragma multi_compile_fwdadd_fullshadows

				#include "Nigiri_Injection.cginc"
			ENDCG
		}
	}
}
