using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct OceanDispersionJob : IJobParallelFor
{
    private int resolution;
    private float patchSize, time;

    [ReadOnly]
    private NativeArray<float> dispersionTable;

    [ReadOnly]
    private NativeArray<float4> spectrum;

    [WriteOnly]
    private NativeArray<float2> heightBuffer;

    [WriteOnly]
    private NativeArray<float4> displacementBuffer;

    public OceanDispersionJob(NativeArray<float> dispersionTable, NativeArray<float4> spectrum, NativeArray<float2> heightBuffer, NativeArray<float4> displacementBuffer, int resolution, float patchSize, float time)
    {
        this.dispersionTable = dispersionTable;
        this.spectrum = spectrum;
        this.heightBuffer = heightBuffer;
        this.displacementBuffer = displacementBuffer;
        this.resolution = resolution;
        this.patchSize = patchSize;
        this.time = time;
    }

    void IJobParallelFor.Execute(int index)
    {
        var x = index % resolution;
        var y = index / resolution;

        var waveVector = PI * (2 * float2(x, y) - resolution) / patchSize;
        var waveLength = length(waveVector);

        float omegat = dispersionTable[index] * time;
        float2 direction = float2(0);
        sincos(omegat, out direction.y, out direction.x);

        var spec = spectrum[index];
        var c0a = spec.x * direction.x - spec.y * direction.y;
        var c0b = spec.x * direction.y + spec.y * direction.x;
        var c1a = spec.z * direction.x - spec.w * direction.y;
        var c1b = spec.z * -direction.y + spec.w * -direction.x;

        var c = new float2(c0a + c1a, c0b + c1b);

        heightBuffer[index] = c;

        if (waveLength == 0)
        {
            displacementBuffer[index] = 0;
        }
        else
        {
            displacementBuffer[index] = float4(-c.y * -(waveVector.x / waveLength), c.x * -(waveVector.x / waveLength), -c.y * -(waveVector.y / waveLength), c.x * -(waveVector.y / waveLength));
        }
    }
}
