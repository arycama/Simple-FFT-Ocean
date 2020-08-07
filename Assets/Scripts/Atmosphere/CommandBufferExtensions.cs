using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class CommandBufferExtensions
{
    public static void DispatchNormalized(this CommandBuffer commandBuffer, ComputeShader computeShader, int kernelIndex, int threadGroupsX, int threadGroupsY, int threadGroupsZ)
    {
        computeShader.GetKernelThreadGroupSizes(kernelIndex, out var x, out var y, out var z);

        var xThreads = (int)((threadGroupsX - 1) / x) + 1;
        var yThreads = (int)((threadGroupsY - 1) / y) + 1;
        var zThreads = (int)((threadGroupsZ - 1) / z) + 1;

        commandBuffer.DispatchCompute(computeShader, kernelIndex, xThreads, yThreads, zThreads);
    }
}