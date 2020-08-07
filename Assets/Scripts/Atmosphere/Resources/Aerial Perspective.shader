Shader "Hidden/Aerial Perspective"
{
	SubShader
	{
		Cull Off
		ZWrite Off 
		ZTest Always

		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#pragma multi_compile _ ATMOSPHERIC_SHADOWS_ON
			#pragma multi_compile_shadowcollector

			#include "AerialPerspective.hlsl"

			ENDHLSL
		}
	}
}