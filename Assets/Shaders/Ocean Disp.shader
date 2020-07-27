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

			float2 oceanUv = (worldPos.xz + 0.5) / _OceanScale;
			o.oceanUv = oceanUv;

			float3 displacement = 0;
			displacement.y = tex2Dlod(_OceanHeight, float4(oceanUv, 0, 0));
			//displacement.xz = tex2Dlod(_OceanDisplacement, float4(oceanUv, 0, 0));

			v.vertex.xyz += mul(unity_WorldToObject, displacement);

			float2 slope = 2.0 * tex2Dlod(_OceanNormal, float4(oceanUv, 0, 0)).rg - 1.0;
			v.normal = normalize(float3(slope, 1).xzy);

			v.tangent = float4(cross(v.normal, float3(0, 0, 1)) , -1);
			v.texcoord.xy = oceanUv;
		}
		
		void surf(Input IN, inout SurfaceOutputStandard o) 
		{
			o.Albedo = _Color;
			o.Smoothness = 1;
			//o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
		}

		ENDCG
	} 
}