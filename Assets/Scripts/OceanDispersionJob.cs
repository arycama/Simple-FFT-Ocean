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

        // These could almost be precomputed.. not sure if fetching would take more than computing them again though
        // We possibly multiply a bunch of stuff by it anyway.. look into soon
        var waveVectorX = Mathf.PI * (2 * x - resolution) / patchSize;
        var waveVectorZ = Mathf.PI * (2.0f * y - resolution) / patchSize;

        var waveLength = Mathf.Sqrt(waveVectorX * waveVectorX + waveVectorZ * waveVectorZ);

        float omegat = dispersionTable[index] * time;

        float cos = Mathf.Cos(omegat);
        float sin = Mathf.Sin(omegat);

        var spec = spectrum[index];
        var c0a = spec.x * cos - spec.y * sin;
        var c0b = spec.x * sin + spec.y * cos;
        var c1a = spec.z * cos - spec.w * sin;
        var c1b = spec.z * -sin + spec.w * -cos;

        var c = new Vector2(c0a + c1a, c0b + c1b);

        heightBuffer[index] = c;

        if (waveLength == 0)
        {
            displacementBuffer[index] = new Vector4(0, 0, 0, 0);
        }
        else
        {
            displacementBuffer[index] = float4(-c.y * -(waveVectorX / waveLength), c.x * -(waveVectorX / waveLength), -c.y * -(waveVectorZ / waveLength), c.x * -(waveVectorZ / waveLength));
        }
    }
}
