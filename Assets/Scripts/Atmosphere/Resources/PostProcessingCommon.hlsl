// Contains common PostProcessing helper functions and variables

#ifndef TECH_HUNTER_POST_PROCESSING_INCLUDED
#define TECH_HUNTER_POST_PROCESSING_INCLUDED


// Re-declaring some common Unity variables
Texture2D<float4> _MainTex;
Texture2D<float2> _CameraMotionVectorsTexture;
Texture2D<float> _CameraDepthTexture;

float4x4 unity_CameraInvProjection, unity_MatrixInvV;
SamplerState sampler_MainTex, sampler_CameraDepthTexture, sampler_CameraMotionVectorsTexture;

float4 _WorldSpaceLightPos0;
float4 _LightColor0;

#include  "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

struct Varyings
{
	float4 vertex : SV_POSITION;
	float2 texcoord : TEXCOORD0;
	float2 texcoordStereo : TEXCOORD1;
	#if STEREO_INSTANCING_ENABLED
		uint stereoTargetEyeIndex : SV_RenderTargetArrayIndex;
	#endif

	float3 ray : TEXCOORD2;
};

Varyings Vert(AttributesDefault v)
{
	Varyings o;
	o.vertex = float4(v.vertex.xy, 0.0, 1.0);
	o.texcoord = TransformTriangleVertexToUV(v.vertex.xy);

	#if UNITY_UV_STARTS_AT_TOP
		o.texcoord = o.texcoord * float2(1.0, -1.0) + float2(0.0, 1.0);
		float4 clipPos = float4(v.vertex.xy * float2(1, -1), 0, 1.0);
	#else
		float4 clipPos = o.vertex;
	#endif

	o.texcoordStereo = TransformStereoScreenSpaceTex(o.texcoord, 1.0);
	o.ray = mul(unity_CameraInvProjection, clipPos).xyz;
	return o;
}

float3 WorldPositionFromDepth(float depth, float3 ray)
{
	float eyeDepth = LinearEyeDepth(depth);
	return mul(unity_MatrixInvV, float4(ray * eyeDepth, 1)).xyz;
}

float3 WorldPositionFromDepth(float2 uv, float3 ray)
{
	float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv);
	return WorldPositionFromDepth(depth, ray);
}

float4 ComputeNonStereoScreenPos(float4 pos) 
{
	float4 o = pos * 0.5f;
	o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
	o.zw = pos.zw;
	return o;
}

#if defined(UNITY_SINGLE_PASS_STEREO)
	float2 TransformStereoScreenSpaceTex(float2 uv, float w)
	{
		float4 scaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
		return uv.xy * scaleOffset.xy + scaleOffset.zw * w;
	}
#else
	#define TransformStereoScreenSpaceTex(uv, w) uv
#endif

float4 ComputeScreenPos(float4 pos) 
{
	float4 o = ComputeNonStereoScreenPos(pos);
#if defined(UNITY_SINGLE_PASS_STEREO)
	o.xy = TransformStereoScreenSpaceTex(o.xy, pos.w);
#endif
	return o;
}

float4 ComputeGrabScreenPos(float4 pos) 
{
#if UNITY_UV_STARTS_AT_TOP
	float scale = -1.0;
#else
	float scale = 1.0;
#endif
	float4 o = pos * 0.5f;
	o.xy = float2(o.x, o.y * scale) + o.w;
#ifdef UNITY_SINGLE_PASS_STEREO
	o.xy = TransformStereoScreenSpaceTex(o.xy, pos.w);
#endif
	o.zw = pos.zw;
	return o;
}

#endif