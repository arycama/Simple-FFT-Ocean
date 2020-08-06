Shader "Hidden/Ocean Normal Foam"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert_img
            #pragma fragment frag

            #include "OceanNormalFoam.hlsl"

            ENDHLSL
        }
    }
}