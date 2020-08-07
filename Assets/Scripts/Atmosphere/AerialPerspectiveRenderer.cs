using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public class AerialPerspectiveRenderer : PostProcessEffectRenderer<AerialPerspective>
{
    private Vector3[] frustumCorners = new Vector3[4];
    private RenderTexture cameraScatter, cameraTransmittance;

    private static ComputeShader computeShader;
    private AtmosphereManager atmosphere;

    public override void Render(PostProcessRenderContext context)
    {
        if (atmosphere == null)
        {
            atmosphere = Object.FindObjectOfType<AtmosphereManager>();
            if (atmosphere == null)
            {
                return;
            }
        }

        // Update 3D lookup tables
        var inScatterDescriptor = new RenderTextureDescriptor(settings.volumeWidth, settings.volumeHeight, RenderTextureFormat.ARGBHalf)
        {
            dimension = TextureDimension.Tex3D,
            enableRandomWrite = true,
            volumeDepth = settings.volumeDepth
        };

        var outScatterDescriptor = new RenderTextureDescriptor(settings.volumeWidth, settings.volumeHeight, RenderTextureFormat.RGB111110Float)
        {
            dimension = TextureDimension.Tex3D,
            enableRandomWrite = true,
            volumeDepth = settings.volumeDepth
        };

        if (!cameraScatter) cameraScatter = new RenderTexture(inScatterDescriptor) { name = "CameraInScatter" };
        if (!cameraTransmittance) cameraTransmittance = new RenderTexture(outScatterDescriptor) { name = "CameraOutScatter" };

        PostProcessUtils.CheckRenderTexture(ref cameraScatter, inScatterDescriptor);
        PostProcessUtils.CheckRenderTexture(ref cameraTransmittance, outScatterDescriptor);

        var camera = context.camera;
        camera.CalculateFrustumCorners(camera.rect, camera.farClipPlane, context.camera.stereoActiveEye, frustumCorners);

        for (var i = 0; i < 4; i++)
        {
            frustumCorners[i] = camera.transform.TransformPoint(frustumCorners[i]);
        }

        context.command.SetComputeTextureParam(computeShader, 0, "_AtmosphereGather", atmosphere.AtmosphereGather);
        context.command.SetComputeTextureParam(computeShader, 0, "_AtmosphereTransmittance", atmosphere.AtmosphereTransmittance);
        context.command.SetComputeTextureParam(computeShader, 0, "_CameraScatter", cameraScatter);
        context.command.SetComputeTextureParam(computeShader, 0, "_CameraTransmittance", cameraTransmittance);

        context.command.SetComputeVectorParam(computeShader, "_BottomLeftCorner", frustumCorners[0]);
        context.command.SetComputeVectorParam(computeShader, "_TopLeftCorner", frustumCorners[1]);
        context.command.SetComputeVectorParam(computeShader, "_TopRightCorner", frustumCorners[2]);
        context.command.SetComputeVectorParam(computeShader, "_BottomRightCorner", frustumCorners[3]);
        context.command.SetComputeVectorParam(computeShader, "_WorldSpaceLightPos0", -RenderSettings.sun.transform.forward);

        context.command.DispatchNormalized(computeShader, 0, settings.volumeWidth, settings.volumeHeight, 1);

        var sheet = context.propertySheets.Get(Shader.Find("Hidden/Aerial Perspective"));
        sheet.properties.SetFloat("_ShadowSamples", settings.shadowSamples);
        sheet.properties.SetVector("_WorldSpaceLightPos0", -RenderSettings.sun.transform.forward);
        sheet.properties.SetColor("_LightColor0", RenderSettings.sun.color * RenderSettings.sun.intensity);

        if (settings.volumetricShadows && QualitySettings.shadows != ShadowQuality.Disable)
            sheet.EnableKeyword("ATMOSPHERIC_SHADOWS_ON");
        else
            sheet.DisableKeyword("ATMOSPHERIC_SHADOWS_ON");

        if(QualitySettings.shadowProjection == ShadowProjection.StableFit)
            sheet.EnableKeyword("SHADOWS_SPLIT_SPHERES");
        else
            sheet.DisableKeyword("SHADOWS_SPLIT_SPHERES");

        if (QualitySettings.shadowCascades == 1)
            sheet.EnableKeyword("SHADOWS_SINGLE_CASCADE");
        else
            sheet.DisableKeyword("SHADOWS_SINGLE_CASCADE");

        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);

        context.command.SetGlobalTexture("_CameraScatter", cameraScatter);
        context.command.SetGlobalTexture("_CameraTransmittance", cameraTransmittance);
    }

    public override DepthTextureMode GetCameraFlags()
    {
        return DepthTextureMode.Depth;
    }

    public override void Init()
    {
        computeShader = Resources.Load<ComputeShader>("AerialPerspective");
        Assert.IsNotNull(computeShader);
    }

    public override void Release()
    {
        if (cameraScatter) cameraScatter.Release();
        if (cameraTransmittance) cameraTransmittance.Release();
    }
}