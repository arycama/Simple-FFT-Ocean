Shader "Sky/Physical Skybox"
{
    SubShader
    {
        Tags 
        { 
            "PreviewType" = "Skybox"
            "Queue" = "Background" 
        }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "PhysicalSkybox.hlsl"

            ENDHLSL
        }
    }
}