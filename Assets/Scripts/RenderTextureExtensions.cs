using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class RenderTextureExtensions
{
    public static void Clear(this RenderTexture renderTexture, bool clearDepth, bool clearColor, Color backgroundColor, float depth = 1.0f)
    {
        Graphics.SetRenderTarget(renderTexture);
        GL.Clear(clearDepth, clearColor, backgroundColor, depth);
    }

    public static Texture2D ToTexture2D(this RenderTexture renderTexture)
    {
        var texture = new Texture2D(renderTexture.width, renderTexture.height, renderTexture.graphicsFormat, renderTexture.useMipMap ? TextureCreationFlags.MipChain : TextureCreationFlags.None);

        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        RenderTexture.active = null;

        return texture;
    }
}