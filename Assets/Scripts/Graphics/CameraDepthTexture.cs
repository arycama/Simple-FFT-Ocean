#pragma warning disable 0108

using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(Camera))]
public class CameraDepthTexture : MonoBehaviour
{
    [SerializeField]
    private DepthTextureMode depthTextureMode = DepthTextureMode.Depth;

    private Camera camera;

    private void OnEnable()
    {
        camera = GetComponent<Camera>();
        camera.depthTextureMode |= depthTextureMode;
    }

    private void OnDisable()
    {
        camera.depthTextureMode ^= depthTextureMode;
    }
}