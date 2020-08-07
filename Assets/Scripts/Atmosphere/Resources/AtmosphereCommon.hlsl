#ifndef ATMOSPHERE_COMMON_INCLUDED
#define ATMOSPHERE_COMMON_INCLUDED

#include "GeometryUtils.hlsl"

float _CelestialLightCount;
float3 _CelestialLightDirections[4], _CelestialLightColors[4], _CelestialLightAttenuatedColors[4];

SamplerState _LinearClampSampler;
Texture3D<float4> _SkyScatter;
Texture2D<float3> _AtmosphereTransmittance;

cbuffer AtmosphereData
{
	float4 _AtmosphereExtinction, _AtmosphereScatter;
	float3 _RayleighToMie;
	float2 _AtmosphereScaleHeights;
	float _AtmosphereHeight, _MiePhase, _PlanetRadius;
};

float RayleighPhase(float angle)
{
	return 0.75 * (1.0 + pow(angle, 2));
}

float RayleighPhaseElek(float angle)
{
	return (8.0 / 10.0) * (7.0 / 5.0 + 0.5 * angle);
}

float PhaseHG(float cosAngle, float g)
{
	return (1 - g * g) / pow(1 + g * g - 2 * g * cosAngle, 1.5);
}

float3 ScatterCoordsToSkyParams(float3 uv, float3 resolution)
{
	float height = _AtmosphereHeight * pow(uv.x, 2.0);

	float cosHorizon = -sqrt(height * (height + 2.0 * _PlanetRadius)) / (_PlanetRadius + height);
	float cosViewAngle;
	if (uv.y > 0.5)
	{
		uv.y = saturate((uv.y - (0.5 + 0.5 / resolution.y))) * resolution.y / (resolution.y / 2.0 - 1.0);
		uv.y = pow(uv.y, 5.0);
		cosViewAngle = max(cosHorizon + uv.y * (1.0 - cosHorizon), cosHorizon + 1e-4);
	}
	else
	{
		uv.y = saturate((uv.y - 0.5 / resolution.y)) * resolution.y / (resolution.y / 2.0 - 1.0);
		uv.y = pow(uv.y, 5);
		cosViewAngle = min(cosHorizon - uv.y * (cosHorizon + 1), cosHorizon - 1e-4);
	}
	
	cosViewAngle = clamp(cosViewAngle, -1.0, 1.0);
	
	float cosSunAngle = tan((2.0 * uv.z - 1.0 + 0.26) * 0.75) / tan(1.26 * 0.75);
	cosSunAngle = clamp(cosSunAngle, -1.0, 1.0);
	
	return float3(height, cosViewAngle, cosSunAngle);
}

float2 SkyParamsToTransmittanceCoord(float height, float sunAngle)
{
	float normalizedHeight = saturate(height / _AtmosphereHeight);
	float scaledHeight = normalizedHeight <= 0 ? 0 : sqrt(normalizedHeight);
	
	// Normalised coordinates based on angle between zenith direction and view direction
	float normalisedSunAngle = 0.5 * (atan(max(sunAngle, -0.45) * tan(1.26 * 0.75)) / 0.75 + (1.0 - 0.26));
	
	//scaledHeight = normalizedHeight;
	//normalisedSunAngle = 0.5 * sunAngle + 0.5;
	
	float2 resolution;
	_AtmosphereTransmittance.GetDimensions(resolution.x, resolution.y);
	
	// Map normalised coordinates into in - between pixel range according to resolution
	float2 coord = float2(scaledHeight, normalisedSunAngle);
	return (coord * (resolution - 1) + 0.5) / resolution;
}

float3 SkyParamsToScatterCoords(float3 skyParams, float3 resolution)
{	
	// Normalised coordinates based on camera height between ground level 0 and atsmosphere height
	float normalizedHeight = pow(saturate(skyParams.x / _AtmosphereHeight), 0.5);

	// Normalised coordinates based on andgle between zenith direction and view direction
	skyParams.x = max(skyParams.x, 0);
	float cosHorizon = -sqrt(skyParams.x * (2.f * _PlanetRadius + skyParams.x)) / (_PlanetRadius + skyParams.x);
	float normalisedViewZenithScatt;
	if (skyParams.y > cosHorizon)
	{
		skyParams.y = max(skyParams.y, cosHorizon + 0.0001f);
		normalisedViewZenithScatt = saturate((skyParams.y - cosHorizon) / (1.f - cosHorizon));
		normalisedViewZenithScatt = pow(normalisedViewZenithScatt, 0.2f);
		normalisedViewZenithScatt = 0.5f + 0.5f / resolution.y + normalisedViewZenithScatt * (resolution.y / 2.f - 1.f) / resolution.y;
	}
	else
	{
		skyParams.y = min(skyParams.y, cosHorizon - 0.0001f);
		normalisedViewZenithScatt = saturate((cosHorizon - skyParams.y) / (cosHorizon - (-1.f)));
		normalisedViewZenithScatt = pow(normalisedViewZenithScatt, 0.2f);
		normalisedViewZenithScatt = 0.5f / resolution.y + normalisedViewZenithScatt * (resolution.y / 2.f - 1.f) / resolution.y;
	}
	
	float normalisedSunZenith = 0.5 * (atan(max(skyParams.z, -0.45f) * tan(1.26f * 0.75f)) / 0.75f + (1.0 - 0.26f));

	// Map normalised coordinates into in - between pixel range according to resolution
	float3 result = float3(normalizedHeight, normalisedViewZenithScatt, normalisedSunZenith);
	result.xz = (result.xz * (resolution.xz - 1) + 0.5) / resolution.xz;
	return result;
}

// Transmittance from one point to the atmosphere along a direction
float3 TransmittanceToAtmosphere(float3 rayPosition, float3 sunDirection, float3 center)
{
	float3 ray = rayPosition - center;
	float rayHeight = length(ray);
	float3 rayDir = ray / rayHeight;
	
	rayHeight -= _PlanetRadius;
	
	// Start by looking up the optical depth coming from the light source to this point
	float cosViewAngle = dot(sunDirection, rayDir);
	float2 uv = SkyParamsToTransmittanceCoord(rayHeight, cosViewAngle);
	
	// Get the density at this point, along with the optical depth from the light source to this point
	return _AtmosphereTransmittance.SampleLevel(_LinearClampSampler, uv, 0.0);
}

// Transmittance between two points
float3 TransmittanceToPoint(float3 rayStart, float3 rayEnd, float3 center)
{
	if (all(rayStart == rayEnd))
	{
		return 1.0;
	}
	
	float cameraHeight = distance(center, rayStart);
	bool cameraAbove = cameraHeight >= distance(center, rayEnd);
	float3 rayDir = normalize(rayEnd - rayStart) * (cameraAbove ? -1.0 : 1.0);
		
	float3 cameraDepth = TransmittanceToAtmosphere(rayStart, rayDir, center);
	float3 sampleDepth = TransmittanceToAtmosphere(rayEnd, rayDir, center);
	
	if (cameraAbove)
	{
		return sampleDepth * (cameraDepth == 0.0 ? 0.0 : 1.0 / cameraDepth);
	}
	else
	{
		return cameraDepth * (sampleDepth == 0.0 ? 0.0 : 1.0 / sampleDepth);
	}
}

// Transmittance between two points, and from the end-point to the light source
float3 CalculateTotalTransmittance(float3 rayStart, float3 rayEnd, float3 lightDir, float3 center)
{
	return TransmittanceToPoint(rayStart, rayEnd, center) * TransmittanceToAtmosphere(rayEnd, lightDir, center);
}

// In-scattering between two points
float4 ScatteringToAtmosphere(float3 rayStart, float3 rayDir, float3 lightDirection, float3 center)
{
	float height = distance(rayStart, center) - _PlanetRadius;
	
	float3 normal = normalize(rayStart - center);
	float lightAngle = dot(lightDirection, normal);
	float viewAngle = dot(rayDir, normal);
	
	float3 resolution;
	_SkyScatter.GetDimensions(resolution.x, resolution.y, resolution.z);
	
	float3 uv = SkyParamsToScatterCoords(float3(height, viewAngle, lightAngle), resolution);
	return _SkyScatter.SampleLevel(_LinearClampSampler, uv, 0.0);
}

// Scattering between two points. Note that this has artifacts at the horizon, so should only be used
// When the surface is entirely above, or below the horizon. (Eg clouds or ground sphere)
float4 ScatteringToPoint(float3 rayStart, float3 rayEnd, float3 lightDirection, float3 center)
{
	float cameraHeight = distance(center, rayStart);
	bool cameraAbove = cameraHeight >= distance(center, rayEnd);
	float3 rayDir = normalize(rayEnd - rayStart) * (cameraAbove ? -1.0 : 1.0);
			
	// We can compute in scatter by subtracting camera-to-object scatter * camera-to-object transmittance, from camera-to-atmosphere scatter
	// Note that this causes discontinuities at the horizon, but the cloud layer is generally above the camera
	float4 cameraToAtmosphereScatter = ScatteringToAtmosphere(rayStart, rayDir, lightDirection, center);
	float4 pointToAtmosphereScatter = ScatteringToAtmosphere(rayEnd, rayDir, lightDirection, center);
	float3 transmittance = TransmittanceToPoint(rayEnd, rayStart, center);
			
	if (cameraAbove)
	{
		return max(0, pointToAtmosphereScatter - cameraToAtmosphereScatter * transmittance.rgbr);
	}
	else
	{
		return max(0, cameraToAtmosphereScatter - pointToAtmosphereScatter * transmittance.rgbr);
	}
}

float3 ApplyAtmosphericScattering(float3 color, float4 scatter, float3 attenuation, float angle, float3 lightColor)
{
	color *= attenuation;
	
	if (scatter.r > 0.0)
	{
		float3 rayleigh = scatter.rgb;
		float3 mie = scatter.rgb * (scatter.a / scatter.r) * _RayleighToMie;
	
		// Calculate the in-scattering color and clamp it to the max color value
		float3 rayleighColor = rayleigh * RayleighPhaseElek(angle);
		float3 mieColor = mie * PhaseHG(angle, _MiePhase);
		float3 inScatter = rayleighColor + mieColor;
		
		color += inScatter * lightColor;
	}

	return color;
}

#endif