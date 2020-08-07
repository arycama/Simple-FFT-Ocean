#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
#include "PostProcessingCommon.hlsl"
#include "AtmosphereCommon.hlsl"

#ifdef __INTELLISENSE__
	#define SHADOWS_SPLIT_SPHERES
	//#define SHADOWS_SINGLE_CASCADE

	float3 _WorldSpaceCameraPos;
#endif

SamplerState PointClampSampler;
Texture3D<float4> _CameraScatter;
Texture3D<float3> _CameraTransmittance;
Texture2D<float> _ShadowMapTextureCopy;

float4x4 unity_WorldToShadow[4];
float4 _LightShadowData, _LightSplitsNear, _LightSplitsFar, unity_ShadowFadeCenterAndType, unity_ShadowSplitSqRadii;
float3 unity_ShadowSplitSpheres[4];
float _ShadowSamples;

float CalculateShadow(float3 worldPos, float eyeDepth, float3 ray)
{
	// Limit raymarch to shadow distance
	#ifdef SHADOWS_SINGLE_CASCADE
		float maxZ = min(eyeDepth, _LightSplitsFar.x);
	#else
		// This will slightly overshoot for 2-cascades, but it only seems to be a couple of units
		float maxZ = min(eyeDepth, _LightSplitsFar.w);
	#endif
	
	//maxZ = eyeDepth;
	// Start position in shadow space (Camera position)
	// New world pos taking max shadow distance into account
	worldPos = mul(unity_MatrixInvV, float4(ray * maxZ, 1)).xyz;
	
	#if defined(SHADOWS_SPLIT_SPHERES) && !defined(SHADOWS_SINGLE_CASCADE)
		float3 fromCenter0 = worldPos - unity_ShadowSplitSpheres[0];
		float3 fromCenter1 = worldPos - unity_ShadowSplitSpheres[1];
		float3 fromCenter2 = worldPos - unity_ShadowSplitSpheres[2];
		float3 fromCenter3 = worldPos - unity_ShadowSplitSpheres[3];
	
		// Calculate sqrDist from start point splitSpheres, divide by sampleCount
		float4 sqrSplits;
		sqrSplits.x = dot(fromCenter0, fromCenter0);
		sqrSplits.y = dot(fromCenter1, fromCenter1);
		sqrSplits.z = dot(fromCenter2, fromCenter2);
		sqrSplits.w = dot(fromCenter3, fromCenter3);
		sqrSplits /= (_ShadowSamples - 1);
	#endif

	// Need to clamp this
	float3 rayStep = (worldPos - _WorldSpaceCameraPos) / (_ShadowSamples - 1.0);
	float zStep = maxZ / (_ShadowSamples - 1.0);
	
	float3 shadowStart = mul(unity_WorldToShadow[0], float4(_WorldSpaceCameraPos, 1.0)).xyz;
	float3 shadowEnd = mul(unity_WorldToShadow[0], float4(worldPos, 1.0)).xyz;
	float3 shadowStep = (shadowEnd - shadowStart) / (_ShadowSamples - 1.0);
	
	float occlusion = 0.0;
	for (float j = 0; j < _ShadowSamples; j++)
	{
		float3 rayPos = _WorldSpaceCameraPos + rayStep * j;
		float z = zStep * j;
		
		#ifdef SHADOWS_SINGLE_CASCADE
			// For single cascade, we can raymarch directly in shadow space 
			float3 coord = shadowStart + shadowStep * j;
		#else
			#ifdef SHADOWS_SPLIT_SPHERES
				float4 sqrSplitDists = (sqrSplits * j);
				float4 cascadeWeights = float4(sqrSplitDists < unity_ShadowSplitSqRadii);
				cascadeWeights.yzw = saturate(cascadeWeights.yzw - cascadeWeights.xyz);
			#else
				float4 zNear = float4(z >= _LightSplitsNear);
				float4 zFar = float4(z < _LightSplitsFar);
				float4 cascadeWeights = zNear * zFar;
			#endif
		
			// Pick the strongest cascade
			float cascadeIndex = dot(cascadeWeights, float4(0, 1, 2, 3));
			float4 coord = mul(unity_WorldToShadow[cascadeIndex], float4(rayPos, 1));
		
			//if (any(coord < 0 || coord > 1))
			//{
			//	occlusion += 1;
			//	continue;
			//}
		
			#if defined(UNITY_REVERSED_Z)
				coord.z += 1 - dot(cascadeWeights, 1);
			#endif
		#endif
		
		// Cascaded shadows might be 0 in some areas, eg where no geo was rendered.
		// To avoid black areas, we must manually check before comparing depth
		occlusion += _ShadowMapTextureCopy.SampleLevel(PointClampSampler, coord.xy, 0) < coord.z;
	}
		
	return lerp(_LightShadowData.r, 1.0, occlusion / _ShadowSamples);
}

float4 Frag(Varyings i) : SV_Target
{
	float4 color = _MainTex.Sample(sampler_MainTex, i.texcoord);
	float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.texcoord);

	float linearDepth = Linear01Depth(depth);
	float eyeDepth = LinearEyeDepth(depth);
	float3 worldPos = mul(unity_MatrixInvV, float4(i.ray * eyeDepth, 1)).xyz;
	
	// Volumetric shadows
	float occlusion = 1;
	#ifdef ATMOSPHERIC_SHADOWS_ON
		occlusion = CalculateShadow(worldPos, eyeDepth, i.ray);
	#endif
	
	// Skip if we hit the skybox
	if (linearDepth < 0.99)
	{
		// Align to texel center
		float3 resolution;
		_CameraScatter.GetDimensions(resolution.x, resolution.y, resolution.z);
		float3 uv = float3(i.texcoord, linearDepth);
		uv.z = (uv.z * (resolution.z - 1.0) + 0.5) / resolution.z;
		
		float4 scatter = _CameraScatter.Sample(_LinearClampSampler, uv) * occlusion;
		float3 attenuation = _CameraTransmittance.Sample(_LinearClampSampler, uv);
		
		// Calculate the in-scattering color and clamp it to the max color value
		float3 rayDir = normalize(worldPos - _WorldSpaceCameraPos);
		color.rgb *= attenuation;
		
		float angle = dot(rayDir, _WorldSpaceLightPos0.xyz);
		color.rgb = ApplyAtmosphericScattering(color.rgb, scatter, 1.0, angle, 1);
	}
	
	// Could attenuate skybox slightly... but we need to limit ray to shadow bounds or we'll just waste all our samples really far away
	//else
	//{
	//	color *= occlusion;
	//}
	
	return color;
}