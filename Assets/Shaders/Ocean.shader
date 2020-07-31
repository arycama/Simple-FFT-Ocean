Shader "Ocean/Ocean Disp" 
{
	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
		_Scatter("Scatter", Color) = (1, 1, 1, 1)
		_BumpMap("Normal Map", 2D) = "bump" {}

		_WaveNoise("Wave Noise", 2D) = "black" {}

		[Header(Reflection)]
		_ReflectionScale("Reflection Scale", Range(0, 4)) = 1
		_ReflectionFalloff("Reflection Falloff", Range(0, 32)) = 5
		_ReflectionBias("Reflection Bias", Range(0, 2)) = 0

		[Header(Foam)]
		_FoamThreshold("Foam Threshold", Range(0, 1)) = 0.3
		_FoamStrength("Foam Strength", Range(0, 2)) = 1
		_FoamMap("Foam Map", 2D) = "clear" {}
	}

	SubShader
	{
		Tags{ "PreviewType"="Plane" "Queue"="Transparent" "RenderType"="Transparent"}

		Pass
		{
			Tags{"LightMode"="Forwardbase"}

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"

			sampler2D _OceanHeight, _OceanDisplacement, _OceanNormal, _BumpMap, _FoamMap, _WaveNoise;
			float4 _OceanNormal_TexelSize, _WaveNoise_ST, _BumpMap_ST, _FoamMap_ST;
			half3 _Color, _Scatter;
			half _FoamStrength, _FoamThreshold, _OceanScale;
			half _ReflectionScale, _ReflectionBias, _ReflectionFalloff;

			struct appdata
			{
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				UNITY_POSITION(pos);
				float3 viewDir : POSITION1;
				float2 uv : TEXCOORD0;
				float4 bumpFoamUv : TEXCOORD1;
				UNITY_FOG_COORDS(2)
			};

			v2f vert(appdata v)
			{
				UNITY_SETUP_INSTANCE_ID(v);

				float3 worldPos = mul(unity_ObjectToWorld, v.vertex);
				float2 oceanUv = worldPos.xz / _OceanScale;

				// Sample a noise texture to break up repetition
				float noise = tex2Dlod(_WaveNoise, float4(worldPos.xz * _WaveNoise_ST.xy + _WaveNoise_ST.zw * _Time.y, 0, 0));

				float3 displacement = 0;
				displacement.y = tex2Dlod(_OceanHeight, float4(oceanUv, 0, 0));
				displacement.xz = tex2Dlod(_OceanDisplacement, float4(oceanUv, 0, 0));
				displacement *= 1 - noise;

				worldPos += displacement;

				v2f o;
				o.pos = UnityWorldToClipPos(worldPos);
				o.uv = oceanUv;
				o.bumpFoamUv.xy = TRANSFORM_TEX(worldPos.xz, _BumpMap);
				o.bumpFoamUv.zw = TRANSFORM_TEX(worldPos.xz, _FoamMap);
				o.viewDir = _WorldSpaceCameraPos - worldPos;

				UNITY_TRANSFER_FOG(o, o.pos);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 normalFolding = tex2D(_OceanNormal, i.uv);

				// Calculate normals
				fixed3 geomNormal = 2.0 * normalFolding - 1.0;
				float noise = tex2D(_WaveNoise, (i.uv + _WaveNoise_ST.zw * _Time.y) * _WaveNoise_ST.xy);
				geomNormal = lerp(geomNormal, float3(0, 1, 0), noise);

				fixed3 geomTangent = cross(geomNormal, float3(0, 0, 1));
				fixed3 geomBitangent = cross(geomTangent, geomNormal);

				// Color
				// Scatter
				fixed scatterFactor = saturate(1 - geomNormal.y);
				fixed3 color = lerp(_Color, _Scatter, scatterFactor);

				// Foam
				fixed foamFactor = saturate(_FoamStrength * (-normalFolding.w + _FoamThreshold));
				fixed foamOpacity = tex2D(_FoamMap, i.bumpFoamUv.zw).r * (1 - noise);
				color = lerp(color, 1, foamFactor * foamOpacity);

				// Lighting
				color *= UNITY_LIGHTMODEL_AMBIENT.rgb + saturate(_WorldSpaceLightPos0.y) * _LightColor0.rgb;

				// Normal map
				fixed3 normalMap = UnpackNormal(tex2D(_BumpMap, i.bumpFoamUv.xy));
				fixed3 normal = normalMap.x * geomTangent + normalMap.y * geomBitangent + normalMap.z * geomNormal;
				fixed3 viewDir = normalize(i.viewDir);

				// Reflections
				fixed3 reflectionDir = reflect(-viewDir, normal);
				half3 reflection = DecodeHDR(UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectionDir), unity_SpecCube0_HDR);

				fixed ndotv = 1 - saturate(dot(normal, viewDir));
				ndotv = pow(saturate(ndotv * _ReflectionScale - _ReflectionBias), _ReflectionFalloff);
				color = lerp(color, reflection, ndotv);

				UNITY_APPLY_FOG(i.fogCoord, color);
				return float4(color, 1);
			}

			ENDCG
		}
	} 
}