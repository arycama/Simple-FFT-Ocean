Shader "Ocean/Ocean Surface" 
{
	Properties
	{
		_UnderwaterDepth("Underwater Depth", Float) = 64
		_Color("Color", Color) = (1, 1, 1, 1)
		_Extinction("Extinction", Color) = (0, 0, 0, 1)
		_Scatter("Scatter", Color) = (1, 1, 1, 1)
		_RefractionOffset("Refract Offset", Range(0, 1)) = 0.2
		_BumpMap("Normal Map", 2D) = "bump" {}

		[Header(Foam)]
		_FoamThreshold("Foam Threshold", Range(0, 1)) = 0.3
		_FoamStrength("Foam Strength", Range(0, 2)) = 1
		_FoamMap("Foam Map", 2D) = "clear" {}
	}

	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType" = "Transparent" }

		Pass
		{
			Tags { "LightMode" = "ForwardBase" }

			Cull Off

			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#pragma multi_compile _ _PLANAR_REFLECTIONS_ON

			#include "Ocean.hlsl"
			ENDCG
		}
	} 
}