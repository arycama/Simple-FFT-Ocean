using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

public static class PostProcessUtils
{
    /// <summary>
    /// Checks if a RenderTexture matches a specified width, height and format, and recreates if not.
    /// </summary>
    /// <param name="renderTexture"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    public static RenderTexture CheckRenderTexture(RenderTexture renderTexture, int width, int height, RenderTextureFormat format, bool enableRandomWrite = false)
    {
        if (format == RenderTextureFormat.Default) format = RenderTextureFormat.ARGB32;
        if (format == RenderTextureFormat.DefaultHDR) format = RenderTextureFormat.ARGBHalf;

        if (renderTexture != null && (renderTexture.width != width || renderTexture.height != height || renderTexture.format != format))
        {
            renderTexture.Release();
            renderTexture.width = width;
            renderTexture.height = height;
            renderTexture.format = format;
        }
        else if (renderTexture == null)
        {
            renderTexture = new RenderTexture(width, height, 0, format);
        }

        return renderTexture;
    }

    public static void CheckRenderTexture(ref RenderTexture renderTexture, RenderTextureDescriptor descriptor)
    {
        if(renderTexture == null)
        {
            renderTexture = new RenderTexture(descriptor);
        }
        else if(!renderTexture.descriptor.Equals(descriptor))
        {
            renderTexture.Release();
            renderTexture.descriptor = descriptor;
            renderTexture.Create();
        }
    }
}