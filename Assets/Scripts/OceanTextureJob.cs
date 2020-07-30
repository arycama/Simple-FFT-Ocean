using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct OceanTextureJob : IJobParallelFor
{
    private int resolution;
    private int passIndex;

    [ReadOnly]
    private NativeArray<float> butterflyLookupTable;

    [ReadOnly]
    private NativeArray<float2> heightSource;

    [ReadOnly]
    private NativeArray<float4> displacementSource;

    [WriteOnly]
    private NativeArray<half> heightPixels;

    [WriteOnly]
    private NativeArray<half2> displacementPixels;

    public OceanTextureJob(int resolution, int passIndex, NativeArray<float> butterflyLookupTable, NativeArray<float2> heightSource, NativeArray<float4> displacementSource, NativeArray<half> heightPixels, NativeArray<half2> displacementPixels)
    {
        this.resolution = resolution;
        this.passIndex = passIndex;
        this.butterflyLookupTable = butterflyLookupTable;
        this.heightSource = heightSource;
        this.displacementSource = displacementSource;
        this.heightPixels = heightPixels;
        this.displacementPixels = displacementPixels;
    }

    float4 FFT(float2 w, float4 input1, float4 input2)
    {
        input1.x += w.x * input2.x - w.y * input2.y;
        input1.y += w.y * input2.x + w.x * input2.y;
        input1.z += w.x * input2.z - w.y * input2.w;
        input1.w += w.y * input2.z + w.x * input2.w;

        return input1;
    }

    float2 FFT(float2 w, float2 input1, float2 input2)
    {
        input1.x += w.x * input2.x - w.y * input2.y;
        input1.y += w.y * input2.x + w.x * input2.y;

        return input1;
    }

    void IJobParallelFor.Execute(int index)
    {
        var x = index % resolution;
        var y = index / resolution;

        // Final FFT
        var bftIdx = 4 * (y + passIndex * resolution);

        var X = (int)butterflyLookupTable[bftIdx + 0];
        var Y = (int)butterflyLookupTable[bftIdx + 1];
        Vector2 w;
        w.x = butterflyLookupTable[bftIdx + 2];
        w.y = butterflyLookupTable[bftIdx + 3];

        var heightResult = FFT(w, heightSource[x + X * resolution], heightSource[x + Y * resolution]);
        var dispResult = FFT(w, displacementSource[x + X * resolution], displacementSource[x + Y * resolution]);

        var sign = ((x + y) & 1) == 0 ? 1 : -1;
        heightPixels[index] = (half)(heightResult.x * sign);

        var dispX = (half)(-dispResult.x * sign);
        var dispZ = (half)(-dispResult.z * sign);
        displacementPixels[index] = half2(dispX, dispZ);
    }
}
