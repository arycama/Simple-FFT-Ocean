#define _ALPHAPREMULTIPLY_ON

#ifdef __INTELLISENSE__
	#define _PLANAR_REFLECTIONS_ON
	#define SHADOW_COPY_ON
#endif

#include "UnityPBSLighting.cginc"
#include "AutoLight.cginc"
#include "Assets/Scripts/Atmosphere/Resources/AtmosphereUtils.hlsl"

UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
UNITY_DECLARE_SHADOWMAP(_ShadowCopy);

sampler2D _BumpMap, _FoamMap, _OceanHeight, _OceanDisplacement, _OceanNormal, _CameraOpaqueTexture, _ReflectionTexture;
float4 _BumpMap_ST, _FoamMap_ST;
half3 _Color, _Extinction, _Scatter;
half _DepthFade, _FoamStrength, _FoamThreshold, _OceanScale, _RefractionOffset, _UnderwaterDepth;

struct appdata
{
	float4 vertex : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f 
{
	UNITY_POSITION(pos);
	float3 worldPos : POSITION1;
	float2 uv : TEXCOORD0;
	float4 screenPos : TEXCOORD1;
	UNITY_FOG_COORDS(2)
	AERIAL_PERSPECTIVE_FACTORS(3, 4)
};

v2f vert(appdata v)
{
	UNITY_SETUP_INSTANCE_ID(v)
	
	v2f o;
	o.worldPos = mul(unity_ObjectToWorld, v.vertex);
	o.uv = o.worldPos.xz / _OceanScale;
	o.worldPos.y += tex2Dlod(_OceanHeight, float4(o.uv, 0, 0));
	o.worldPos.xz += tex2Dlod(_OceanDisplacement, float4(o.uv, 0, 0));
	
	float dst = distance(o.worldPos.xz, _WorldSpaceCameraPos.xz) / _PlanetRadius;
	o.worldPos.y += _PlanetRadius * (sqrt(1 - dst * dst) - 1.0);
	
	o.pos = UnityWorldToClipPos(o.worldPos);
	o.screenPos = ComputeScreenPos(o.pos);
	UNITY_TRANSFER_FOG(o, o.pos);
	CALCULATE_AERIAL_PERSPECTIVE(o.worldPos, o)
	return o;
}
		
half4 frag(v2f i, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
	half4 normalFoam = tex2D(_OceanNormal, i.uv);
	
	// Foam
	float2 foamUv = TRANSFORM_TEX(i.worldPos.xz, _FoamMap);
	half foamFactor = saturate(_FoamStrength * (-normalFoam.a + _FoamThreshold));
	foamFactor *= tex2D(_FoamMap, foamUv).r;
	
	// Foam Albedo
	half oneMinusReflectivity;
	half3 specColor;
	float3 albedo = DiffuseAndSpecularFromMetallic(1, 0, specColor, oneMinusReflectivity);
	
	half outputAlpha;
	albedo = PreMultiplyAlpha(albedo, foamFactor, oneMinusReflectivity, outputAlpha);
	half smoothness = 1 - foamFactor;
	
	// Normal
	half3 normal;
	normal.xz = 2.0 * normalFoam.xz - 1.0;
	normal.y = sqrt(1 - dot(normal.xz, normal.xz));
	
	half scatterFactor = saturate(1 - normal.y);
	
	half3 tangent = cross(normal, float3(0, 0, 1));
	half3 binormal = cross(tangent, normal);

	float2 bumpUv = TRANSFORM_TEX(i.worldPos.xz, _BumpMap);
	half3 normalMap = UnpackNormal(tex2D(_BumpMap, bumpUv));
	normal = normalMap.x * tangent + normalMap.y * binormal + normalMap.z * normal;
	
	float3 viewVec = _WorldSpaceCameraPos - i.worldPos;
	float3 viewDir = normalize(viewVec);
	float3 reflectionDir = reflect(-viewDir, normal);
	
	UnityLight light;
	light.color = _LightColor0.rgb;
	light.dir = _WorldSpaceLightPos0.xyz;
	
	UnityIndirect indirect;
	indirect.diffuse = ShadeSH9(half4(0, 1, 0, 1));
	indirect.specular = DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectionDir, 0), unity_SpecCube0_HDR);
	
	// Screen-uv for reflection/refraction
	float2 uv = i.screenPos.xy / i.screenPos.w;
	uv += normal.xz * _RefractionOffset;
	
	#ifdef _PLANAR_REFLECTIONS_ON
		half4 reflection = tex2D(_ReflectionTexture, uv);
		indirect.specular = lerp(indirect.specular, reflection.rgb, reflection.a);
	#endif
	
	// When underwater, set reflection color to reflect/refract based on direction
	if (!isFrontFace)
	{
		indirect.diffuse = _Color * _Extinction;
		indirect.specular = _Color * _Extinction;
		albedo = 0;
	}
	
	// Lighting for foam, plus general reflections
	half3 color = UNITY_BRDF_PBS(albedo, specColor, oneMinusReflectivity, smoothness, normal, viewDir, light, indirect);
	
	// Underwater lighting/refraction
	float zDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
	float linearDepth = LinearEyeDepth(zDepth);
	float pixelDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(i.screenPos.z) + _ProjectionParams.y;
	float underwaterDepth = linearDepth - pixelDepth;
	
	if (underwaterDepth <= 0)
	{
		uv -= normal.xz * _RefractionOffset;
		zDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
		linearDepth = LinearEyeDepth(zDepth);
		underwaterDepth = max(0, linearDepth - pixelDepth);
	}
	
	// Fade edges when close to geometry
	half fade = saturate(underwaterDepth / _DepthFade);
	
	// When underwater, don't worry about the pixel depth for the color
	float3 underwaterPos = -viewVec / i.screenPos.w * linearDepth + _WorldSpaceCameraPos;
	if (!isFrontFace)
	{
		underwaterDepth = pixelDepth;
		underwaterPos = _WorldSpaceCameraPos;
		scatterFactor = 0;
	}
	
	// Also account for the distance light has to take from it's initial path
	if(_WorldSpaceLightPos0.y > 0)
	{
		float lightDistance = -underwaterPos.y / _WorldSpaceLightPos0.y;
		underwaterDepth += max(0, lightDistance);
	}
	
	// Subsurface scatering
	float3 lighting = indirect.diffuse + _LightColor0.rgb * saturate(_WorldSpaceLightPos0.y);
	half3 scatter = _Color * lighting ;
	half3 background = tex2D(_CameraOpaqueTexture, uv);
	half3 underwaterTint = exp(-underwaterDepth * _Extinction);
	
	// Need to multiply underwater color by inverse Alpha, and inverse fresnel
	half nv = abs(dot(normal, viewDir));
	half fresnel = FresnelTerm(specColor, nv);
	if (!isFrontFace)
	{
		float inv_eta = 1.34 / 1;
		float SinT2 = inv_eta * inv_eta * (1.0f - nv * nv);
		if (SinT2 > 1.0f)
		{
			background = 0;
		}
	}
	
	half3 underwaterColor = lerp(scatter, background, underwaterTint);
	underwaterColor = lerp(underwaterColor, _Scatter * lighting, scatterFactor);
	color += underwaterColor * (1 - outputAlpha) * (1 - fresnel);
	
	APPLY_AERIAL_PERSPECTIVE(color, i.worldPos, i)
	UNITY_APPLY_FOG(i.fogCoord, color);
	
	return half4(color, fade);
}