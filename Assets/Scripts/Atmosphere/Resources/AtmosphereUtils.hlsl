#ifndef ATMOSPHERE_UTILS_INCLUDED
#define ATMOSPHERE_UTILS_INCLUDED

#include "AtmosphereCommon.hlsl"
#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc"

Texture3D<float4> _CameraScatter;
Texture3D<float3> _CameraTransmittance;

#ifdef ATMOSPHERIC_SCATTERING_ON
	#define ATMOSPHERIC_SUN_ATTENUATION(color, worldPos, lightDirection) color.rgb *= TransmittanceToAtmosphere(worldPos, lightDirection, float3(_WorldSpaceCameraPos.xz, -_PlanetRadius).xzy);
	#define ATMOSPHERIC_LIGHT_ATTENUATION(color, worldPos, lightPos) color.rgb *= TransmittanceToPoint(worldPos, lightPos, float3(_WorldSpaceCameraPos.xz, -_PlanetRadius).xzy);
#else
	#define	ATMOSPHERIC_SUN_ATTENUATION(color, worldPos, lightDirection)
	#define ATMOSPHERIC_LIGHT_ATTENUATION(color, worldPos, lightPos)
#endif

#if defined(AERIAL_PERSPECTIVE_ON)
	#define AERIAL_PERSPECTIVE_FACTORS(idx, idy) float4 scatter : TEXCOORD##idx; float3 transmittance : TEXCOORD##idy;
	#define CALCULATE_AERIAL_PERSPECTIVE(worldPos, o) CalculateAerialPerspective(worldPos, o.scatter, o.transmittance);
	#define APPLY_AERIAL_PERSPECTIVE(color, worldPos, i) color.rgb = ApplyAerialPerspective(color.rgb, worldPos, i.scatter, i.transmittance); 
	#define AERIAL_PERSPECTIVE(color, worldPos) color.rgb = AerialPerspective(color.rgb, worldPos);
#else
	#define AERIAL_PERSPECTIVE_FACTORS(idx, idy)
	#define CALCULATE_AERIAL_PERSPECTIVE(worldPos, o)
	#define APPLY_AERIAL_PERSPECTIVE(color, worldPos, i)
	#define AERIAL_PERSPECTIVE(color, worldPos)
#endif

// Calculates Aerial-Perspective per-vertex, which can be applied later in the frag shader
void CalculateAerialPerspective(float3 worldPos, out float4 scatter, out float3 transmittance)
{
	float4 clipPos = UnityWorldToClipPos(worldPos);
	float4 screenPos = ComputeScreenPos(clipPos);
	screenPos.xyz /= screenPos.w;
	screenPos.z = Linear01Depth(screenPos.z);
	
		// Align to texel center
	float3 resolution;
	_CameraScatter.GetDimensions(resolution.x, resolution.y, resolution.z);
	float3 uv = screenPos.xyz;
	uv.z = (uv.z * (resolution.z - 1.0) + 0.5) / resolution.z;
	
	scatter = _CameraScatter.SampleLevel(_LinearClampSampler, uv, 0);
	transmittance = _CameraTransmittance.SampleLevel(_LinearClampSampler, uv, 0);
}

// Applies per-pixel Aerial-Perspective computed per-vertex
float3 ApplyAerialPerspective(float3 color, float3 worldPos, float4 scatter, float3 transmittance)
{
	color *= transmittance;
	
	float3 rayDir = normalize(worldPos - _WorldSpaceCameraPos);
	float angle = dot(rayDir, _WorldSpaceLightPos0.xyz);
	color = ApplyAtmosphericScattering(color, scatter, 1.0, angle, 1);
	
	return color;
}

// Calculates and applies AP in one step. Less efficient than per-vertex
float3 AerialPerspective(float3 color, float3 worldPos)
{
	float4 scatter;
	float3 transmittance;
	CalculateAerialPerspective(worldPos, scatter, transmittance);
	return ApplyAerialPerspective(color, worldPos, scatter, transmittance);
}

#endif