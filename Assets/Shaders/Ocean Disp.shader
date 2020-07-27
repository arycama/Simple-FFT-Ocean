Shader "Ocean/Ocean Disp" 
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
		float4 _OceanNormal_TexelSize;
		half3 _Color;
		half _OceanScale;

		struct Input 
		{
			float3 worldPos;
			float2 oceanUv;
		};

		void vert(inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);

			float3 worldPos = mul(unity_ObjectToWorld, v.vertex);

			float2 oceanUv = (worldPos.xz + 0.5) / _OceanScale;
			o.oceanUv = oceanUv;

			float3 displacement = 0;
			displacement.y = tex2Dlod(_OceanHeight, float4(oceanUv, 0, 0));
			displacement.xz = tex2Dlod(_OceanDisplacement, float4(oceanUv, 0, 0));

			v.vertex.xyz += mul(unity_WorldToObject, displacement);

			v.normal = tex2Dlod(_OceanNormal, float4(oceanUv, 0, 0)) * float3(1, 1, 1);
			v.tangent = float4(1, 0, 0, -1);
		}
		
		void surf(Input IN, inout SurfaceOutputStandard o) 
		{
			float3 normal = tex2D(_OceanNormal, IN.oceanUv + _OceanNormal_TexelSize.xy * 0.5);

			//float3 normal;
			//normal.xz = packedNormal;
			//normal.y = sqrt(1 - saturate(dot(normal.xz, normal.xz)));

			o.Albedo = _Color;
			o.Smoothness = 1;
			//o.Normal = normalize(normal.xzy);
		}

		ENDCG
	} 
}















