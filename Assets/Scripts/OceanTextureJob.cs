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
    private NativeArray<float4> butterflyLookupTable;

    [ReadOnly]
    private NativeArray<float2> heightSource;

    [ReadOnly]
    private NativeArray<float4> displacementSource;

    [WriteOnly]
    private NativeArray<half> heightPixels;

    [WriteOnly]
    private NativeArray<half2> displacementPixels;

    public OceanTextureJob(int resolution, int passIndex, NativeArray<float4> butterflyLookupTable, NativeArray<float2> heightSource, NativeArray<float4> displacementSource, NativeArray<half> heightPixels, NativeArray<half2> displacementPixels)
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
        return input1 + w.xyxy * input2.xxzz + w.yxyx * float4(-1, 1, -1, 1) * input2.yyww;
    }

    float2 FFT(float2 w, float2 input1, float2 input2)
    {
        return input1 + w * input2.xx + w.yx * float2(-1, 1) * input2.yy;
    }

    void IJobParallelFor.Execute(int index)
    {
        var x = index % resolution;
        var y = index / resolution;

        var bftIdx = y + passIndex * resolution;
        var butterfly = butterflyLookupTable[bftIdx];

        var heightResult = FFT(butterfly.zw, heightSource[x + (int)butterfly.x * resolution], heightSource[x + (int)butterfly.y * resolution]);
        var dispResult = FFT(butterfly.zw, displacementSource[x + (int)butterfly.x * resolution], displacementSource[x + (int)butterfly.y * resolution]);

        var sign = ((x + y) & 1) == 0 ? 1 : -1;
        heightPixels[index] = (half)(heightResult.x * sign);

        var dispX = (half)(-dispResult.x * sign);
        var dispZ = (half)(-dispResult.z * sign);
        displacementPixels[index] = half2(dispX, dispZ);
    }
}
