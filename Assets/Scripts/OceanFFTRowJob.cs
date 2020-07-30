using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct OceanFFTRowJob : IJobParallelFor
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
    private NativeArray<float2> heightResult;

    [WriteOnly]
    private NativeArray<float4> displacementResult;

    public OceanFFTRowJob(int resolution, int passIndex, NativeArray<float> butterflyLookupTable, NativeArray<float2> heightSource, NativeArray<float4> displacementSource, NativeArray<float2> heightResult, NativeArray<float4> displacementResult)
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

        var bftIdx = 4 * (x + passIndex * resolution);

        var X = (int)butterflyLookupTable[bftIdx + 0];
        var Y = (int)butterflyLookupTable[bftIdx + 1];
        float2 w;
        w.x = butterflyLookupTable[bftIdx + 2];
        w.y = butterflyLookupTable[bftIdx + 3];

        heightResult[x + y * resolution] = FFT(w, heightSource[X + y * resolution], heightSource[Y + y * resolution]);
        displacementResult[x + y * resolution] = FFT(w, displacementSource[X + y * resolution], displacementSource[Y + y * resolution]);
    }
}
