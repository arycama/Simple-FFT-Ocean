Shader "Ocean/Ocean Disp" 
{
	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
		_BumpMap("Normal Map", 2D) = "bump" {}
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard vertex:vert

		sampler2D _OceanHeight, _OceanDisplacement, _OceanNormal, _BumpMap;
		float4 _OceanNormal_TexelSize;
		half3 _Color;
		half _OceanScale;

		struct Input 
		{
			float2 oceanUv;
			float2 uv_BumpMap;
		};

		void vert(inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);

			float3 worldPos = mul(unity_ObjectToWorld, v.vertex);

			float2 oceanUv = (worldPos.xz) / _OceanScale;
			o.oceanUv = oceanUv;

			float3 displacement = 0;
			displacement.y = tex2Dlod(_OceanHeight, float4(oceanUv, 0, 0));
			displacement.xz = tex2Dlod(_OceanDisplacement, float4(oceanUv, 0, 0));

			v.vertex.xyz += mul(unity_WorldToObject, displacement);

			v.normal.xz = 2.0 * tex2Dlod(_OceanNormal, float4(oceanUv, 0, 0)).rg - 1.0;
			v.normal.y = sqrt(1 - saturate(dot(v.normal.xz, v.normal.xz)));
			v.normal = float3(0, 1, 0);

			v.tangent = float4(cross(v.normal, float3(0, 0, 1)) , -1);
			v.texcoord = float4(oceanUv, 0, 0);
		}
		
		void surf(Input IN, inout SurfaceOutputStandard o) 
		{
			float3 normal;
			normal.xz = 2.0 * tex2D(_OceanNormal, IN.oceanUv ).rg - 1.0;
			normal.y = sqrt(1 - saturate(dot(normal.xz, normal.xz)));

			o.Albedo = _Color;
			o.Smoothness = 1;
			o.Normal = normal.xzy;// UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
		}

		ENDCG
	} 
}