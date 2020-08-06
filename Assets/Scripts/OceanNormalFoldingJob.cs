using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct OceanNormalFoldingJob : IJobParallelFor
{
    private int resolution;
    private float patchSize;

    [ReadOnly]
    private NativeArray<half> heightPixels;

    [ReadOnly]
    private NativeArray<half2> displacementPixels;

    [WriteOnly]
    private NativeArray<int> normalPixels;

    public OceanNormalFoldingJob(int resolution, float patchSize, NativeArray<half> heightPixels, NativeArray<half2> displacementPixels, NativeArray<int> normalPixels)
    {
        this.resolution = resolution;
        this.patchSize = patchSize;
        this.heightPixels = heightPixels;
        this.displacementPixels = displacementPixels;
        this.normalPixels = normalPixels;
    }

    void IJobParallelFor.Execute(int index)
    {
        var x = index % resolution;
        var y = index / resolution;

        // Calculate normal from displacement
        var left = MathUtils.Wrap(x - 1, resolution) + y * resolution;
        var right = MathUtils.Wrap(x + 1, resolution) + y * resolution;
        var down = x + MathUtils.Wrap(y - 1, resolution) * resolution;
        var up = x + MathUtils.Wrap(y + 1, resolution) * resolution;

        // Use central diff, then try with finite to see if quality is similar
        var delta = resolution / patchSize;

        var xSlope = heightPixels[left] - heightPixels[right];
        var zSlope = heightPixels[down] - heightPixels[up];

        // Store foam (folding) in w
        var dx = (float2)displacementPixels[left] - (float2)displacementPixels[right];
        var dz = (float2)displacementPixels[down] - (float2)displacementPixels[right];

        var jx = -dx * delta;
        var jz = -dz * delta;

        var jacobian = (1 + jx.x) * (1 + jz.y) - jx.y * jz.x;

        var normalFolding = int4((float4(normalize(float3(xSlope * delta, 4, zSlope * delta)) * 0.5f + 0.5f, saturate(jacobian))) * 255);

        normalPixels[index] = normalFolding.x | normalFolding.y << 8 | normalFolding.z << 16 | normalFolding.w << 24;
    }
}
