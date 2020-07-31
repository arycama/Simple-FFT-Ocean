Shader "Ocean/Ocean Disp" 
{
	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
		_Scatter("Scatter", Color) = (1, 1, 1, 1)
		_BumpMap("Normal Map", 2D) = "bump" {}

		_WaveNoise("Wave Noise", 2D) = "black" {}

		[Header(Foam)]
		_FoamThreshold("Foam Threshold", Range(0, 1)) = 0.3
		_FoamStrength("Foam Strength", Range(0, 2)) = 1
		_FoamMap("Foam Map", 2D) = "clear" {}
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard vertex:vert

		sampler2D _OceanHeight, _OceanDisplacement, _OceanNormal, _BumpMap, _FoamMap, _WaveNoise;
		float4 _OceanNormal_TexelSize, _WaveNoise_ST;
		half3 _Color, _Scatter;
		half _FoamStrength, _FoamThreshold, _OceanScale;

		struct Input 
		{
			float2 oceanUv;
			float2 uv_BumpMap;
			float2 uv_FoamMap;
			float3 worldPos;
		};

		void vert(inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);

			float3 worldPos = mul(unity_ObjectToWorld, v.vertex);
			o.oceanUv = worldPos.xz / _OceanScale;

			// Sample a noise texture to break up repetition
			float noise = tex2Dlod(_WaveNoise, float4(worldPos.xz * _WaveNoise_ST.xy + _WaveNoise_ST.zw * _Time.y, 0, 0));

			float3 displacement = 0;
			displacement.y = tex2Dlod(_OceanHeight, float4(o.oceanUv, 0, 0));
			displacement.xz = tex2Dlod(_OceanDisplacement, float4(o.oceanUv, 0, 0));

			v.vertex.xyz += mul(unity_WorldToObject, displacement * noise);

			v.normal = float3(0, 1, 0);
			v.tangent = float4(1, 0, 0, -1);
			v.texcoord = float4(worldPos.xz, 0, 0);
		}
		
		void surf(Input IN, inout SurfaceOutputStandard o) 
		{
			fixed4 normalFolding = tex2D(_OceanNormal, IN.oceanUv);

			fixed3 geomNormal = 2.0 * normalFolding - 1.0;

			float noise = tex2D(_WaveNoise, IN.worldPos.xz * _WaveNoise_ST.xy + _WaveNoise_ST.zw * _Time.y);
			geomNormal = normalize(lerp(geomNormal, float3(0, 1, 0), noise));

			fixed3 geomTangent = cross(geomNormal, float3(0, 0, 1));
			fixed3 geomBitangent = cross(geomTangent, geomNormal);

			fixed3 normalMap = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
			fixed3 normal = normalMap.x * geomTangent + normalMap.y * geomBitangent + normalMap.z * geomNormal;

			// Foam
			fixed foamFactor = saturate(_FoamStrength * (-normalFolding.w + _FoamThreshold));
			fixed foamOpacity = tex2D(_FoamMap, IN.uv_FoamMap).r * noise;

			// Scatter
			fixed scatterFactor = saturate(1 - geomNormal.y);
			fixed3 albedo = lerp(_Color, _Scatter, scatterFactor);

			o.Albedo = lerp(albedo, 1, foamFactor * foamOpacity);
			o.Smoothness = 1;
			o.Normal = normal.xzy;
		}

		ENDCG
	} 
}