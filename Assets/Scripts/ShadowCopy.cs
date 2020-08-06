#pragma warning disable 0108

using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, RequireComponent(typeof(Light))]
public class ShadowCopy : MonoBehaviour
{
    [SerializeField]
    private string propertyName = "_ShadowCopy";

    [SerializeField]
    private string keyword = "SHADOW_COPY_ON";

    private Light light;
    private CommandBuffer commandBuffer;

    private void OnEnable()
    {
        light = GetComponent<Light>();

        commandBuffer = new CommandBuffer { name = "Copy Shadow Map" };
        commandBuffer.SetGlobalTexture(propertyName, BuiltinRenderTextureType.CurrentActive);

        light = RenderSettings.sun;
        light.AddCommandBuffer(LightEvent.AfterShadowMap, commandBuffer);

        Shader.EnableKeyword(keyword);
    }

    private void OnDisable()
    {
        light.RemoveCommandBuffer(LightEvent.AfterShadowMap, commandBuffer);
        Shader.DisableKeyword(keyword);
    }
}