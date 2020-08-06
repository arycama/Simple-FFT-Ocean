#pragma warning disable 0108

using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class CameraGrabPass : MonoBehaviour
{
    [SerializeField, Range(0, 3)]
    private int downSample = 1;

    [SerializeField]
    private string propertyName = "_CameraOpaqueTexture";

    private CommandBuffer commandBuffer;

    private void OnEnable()
    {
        commandBuffer = new CommandBuffer() { name = "CameraGrabPass" };
        Camera.onPreRender += CameraOnPreRender;
        Camera.onPostRender += CameraOnPostRender;
    }

    private void CameraOnPreRender(Camera camera)
    {
        commandBuffer.Clear();

        // Ensure width/height/format is correct, incase any properties (eg window size) have changed
        var width = camera.pixelWidth >> downSample;
        var height = camera.pixelHeight >> downSample;
        var format = camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        var id = Shader.PropertyToID("_CameraOpaqueTexture");

        commandBuffer.GetTemporaryRT(id, width, height, 0, FilterMode.Bilinear, format);
        commandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, id);
        commandBuffer.SetGlobalTexture(propertyName, id);

        camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, commandBuffer);
    }

    private void CameraOnPostRender(Camera camera)
    {
        camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, commandBuffer);
    }

    private void OnDisable()
    {
        Camera.onPreRender -= CameraOnPreRender;
        Camera.onPostRender -= CameraOnPostRender;
    }
}
