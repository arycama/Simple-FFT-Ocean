Shader "Ocean/Ocean" 
{
	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
	}

		SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard vertex:vert

		sampler2D _OceanHeight, _OceanDisplacement, _OceanNormal;
		half3 _Color;
		half _OceanScale;

		struct Input 
		{
			float2 uv_MainTex;
			float3 worldPos;
		};

		void vert(inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);

			float3 worldPos = mul(unity_ObjectToWorld, v.vertex);

			float2 oceanUv = worldPos.xz / _OceanScale;

			float3 displacement = 0;
			displacement.y = tex2Dlod(_OceanHeight, float4(oceanUv, 0, 0));

			//v.vertex.xyz += mul(unity_WorldToObject, displacement);
		}
		
		void surf(Input IN, inout SurfaceOutputStandard o) 
		{
			float2 oceanUv = IN.worldPos.xz / _OceanScale;
			float2 packedNormal = tex2D(_OceanNormal, oceanUv);

			float3 normal;
			normal.xz = 2.0 * packedNormal - 1.0;
			normal.y = sqrt(1 - saturate(pow(normal.xz, 2.0)));

			o.Albedo = _Color;
			o.Smoothness = 1;
			//o.Normal = normal.xzy;
		}

		ENDCG
	} 
}















