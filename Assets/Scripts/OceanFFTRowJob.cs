using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using static Unity.Mathematics.math;

[BurstCompile]
public struct OceanFFTRowJob : IJobParallelFor
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
    private NativeArray<float2> heightResult;

    [WriteOnly]
    private NativeArray<float4> displacementResult;

    public OceanFFTRowJob(int resolution, int passIndex, NativeArray<float4> butterflyLookupTable, NativeArray<float2> heightSource, NativeArray<float4> displacementSource, NativeArray<float2> heightResult, NativeArray<float4> displacementResult)
    {
        this.resolution = resolution;
        this.passIndex = passIndex;
        this.butterflyLookupTable = butterflyLookupTable;
        this.heightSource = heightSource;
        this.displacementSource = displacementSource;
        this.heightResult = heightResult;
        this.displacementResult = displacementResult;
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

        var bftIdx = x + passIndex * resolution;
        var butterfly = butterflyLookupTable[bftIdx];

        heightResult[index] = FFT(butterfly.zw, heightSource[(int)butterfly.x + y * resolution], heightSource[(int)butterfly.y + y * resolution]);
        displacementResult[index] = FFT(butterfly.zw, displacementSource[(int)butterfly.x + y * resolution], displacementSource[(int)butterfly.y + y * resolution]);
    }
}
