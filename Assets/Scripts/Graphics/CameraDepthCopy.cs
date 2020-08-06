#pragma warning disable 0108

using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class CameraDepthCopy : MonoBehaviour
{
    [SerializeField]
    private string propertyName = "_TestDepthTexture";

    public ComputeShader computeShader;

    private CommandBuffer commandBuffer;

    private void OnEnable()
    {
        commandBuffer = new CommandBuffer() { name = "CameraDepthCopy" };
        Camera.onPreRender += CameraOnPreRender;
        Camera.onPostRender += CameraOnPostRender;
    }

    private void CameraOnPreRender(Camera camera)
    {
        commandBuffer.Clear();

        // Ensure width/height/format is correct, incase any properties (eg window size) have changed
        var id = Shader.PropertyToID("_CameraDepthCopy");
        var descriptor = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, RenderTextureFormat.RFloat, 0) { enableRandomWrite = true };
        commandBuffer.GetTemporaryRT(id, descriptor);
        commandBuffer.SetComputeTextureParam(computeShader, 0, "Input", Graphics.activeDepthBuffer);
        commandBuffer.SetComputeTextureParam(computeShader, 0, "Result", id);
        commandBuffer.DispatchCompute(computeShader, 0, camera.pixelWidth, camera.pixelHeight, 1);
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