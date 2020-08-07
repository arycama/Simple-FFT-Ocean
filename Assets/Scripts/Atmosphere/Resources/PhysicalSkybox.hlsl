#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc"
#include "AtmosphereCommon.hlsl"

SamplerState sampler_SkyAmbient, sampler_StarMap;
Texture2D<float3> _SkyAmbient;
TextureCube<float3> _StarMap;

float _StarSpeed;
float3 _GroundColor, _StarColor, _StarAxis;
	
struct v2f
{
	UNITY_POSITION(pos);
	float3 vertex : POSITION1;
};

v2f Vert(float4 vertex : POSITION)
{
	v2f o;
	o.vertex = vertex.xyz;
	o.pos = UnityObjectToClipPos(vertex);
	return o;
}

float3 Frag(v2f i) : SV_Target
{
	float3 center = float3(_WorldSpaceCameraPos.xz, -_PlanetRadius).xzy;
	
	float3 direction = normalize(i.vertex);
	float angle = dot(direction, _WorldSpaceLightPos0.xyz);
	float4 scatter = ScatteringToAtmosphere(_WorldSpaceCameraPos, direction, _WorldSpaceLightPos0.xyz, center);
	return ApplyAtmosphericScattering(0, scatter, 1, angle, 1);
}