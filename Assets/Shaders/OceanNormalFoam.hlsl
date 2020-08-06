#include "UnityCG.cginc"

sampler2D _OceanHeight, _OceanDisplacement;
float4 _OceanHeight_TexelSize, _OceanDisplacement_TexelSize;
float _OceanScale;

fixed4 frag (v2f_img i) : SV_Target
{
	float leftHeight = tex2D(_OceanHeight, i.uv + _OceanHeight_TexelSize.xy * float2(-1, 0));
	float rightHeight = tex2D(_OceanHeight, i.uv + _OceanHeight_TexelSize.xy * float2(1, 0));
	float backHeight = tex2D(_OceanHeight, i.uv + _OceanHeight_TexelSize.xy * float2(0, -1));
	float forwardHeight = tex2D(_OceanHeight, i.uv + _OceanHeight_TexelSize.xy * float2(0, 1));
    
	float2 delta = _OceanHeight_TexelSize.zw / _OceanScale;

	float xSlope = leftHeight - rightHeight;
	float zSlope = backHeight - forwardHeight;

	 // Store foam (folding) in w
	float2 leftDisplacement = tex2D(_OceanDisplacement, i.uv + _OceanDisplacement_TexelSize.xy * float2(-1, 0));
	float2 rightDisplacement = tex2D(_OceanDisplacement, i.uv + _OceanDisplacement_TexelSize.xy * float2(1, 0));
	float2 backDisplacement = tex2D(_OceanDisplacement, i.uv + _OceanDisplacement_TexelSize.xy * float2(0, -1));
	float2 forwardDisplacement = tex2D(_OceanDisplacement, i.uv + _OceanDisplacement_TexelSize.xy * float2(0, 1));
	
	float2 dx = leftDisplacement - rightDisplacement;
	float2 dz = backDisplacement - forwardDisplacement;

	float2 jx = -dx * delta;
	float2 jz = -dz * delta;

	float jacobian = (1 + jx.x) * (1 + jz.y) - jx.y * jz.x;
	float4 result = float4(normalize(float3(xSlope * delta.x, 4, zSlope * delta.y)) * 0.5f + 0.5f, saturate(jacobian));
	
	return result;
}