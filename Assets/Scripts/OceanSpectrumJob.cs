using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections;

public struct OceanSpectrumJob : IJobParallelFor
{
    private float directionality, gravity, maxWaveHeight, minWaveLength, patchSize;
    private int resolution;
    private float2 windDirection;
    private Random random;

    [WriteOnly]
    private NativeArray<float> dispersionTable;

    [WriteOnly]
    private NativeArray<float4> spectrum;

    public OceanSpectrumJob(float directionality, float gravity, float maxWaveHeight, float minWaveLength, float patchSize, int resolution, float2 windDirection, Random random, NativeArray<float> dispersionTable, NativeArray<float4> spectrum)
    {
        this.directionality = directionality;
        this.gravity = gravity;
        this.maxWaveHeight = maxWaveHeight;
        this.minWaveLength = minWaveLength;
        this.patchSize = patchSize;
        this.resolution = resolution;
        this.windDirection = windDirection;
        this.random = random;
        this.dispersionTable = dispersionTable;
        this.spectrum = spectrum;
    }

    void IJobParallelFor.Execute(int index)
    {
        var x = index % resolution;
        var y = index / resolution;

        var waveVector = PI * float2(2 * x - resolution, 2 * y - resolution) / patchSize;
        var waveLength = length(waveVector);
        dispersionTable[index] = sqrt(gravity * waveLength);

        if (waveLength == 0)
        {
            spectrum[index] = 0;
            return;
        }

        var fftNorm = pow(resolution, -0.25f);
        var philNorm = E / patchSize;

        var waveDirection = waveVector / waveLength;
        var windFactor = float2(dot(waveDirection, windDirection), dot(-waveDirection, windDirection));
        var phillips = exp(-1 / pow(waveLength * maxWaveHeight, 2)) / pow(waveLength, 4) * pow(windFactor, 2);

        // Remove small wavelengths
        phillips *= exp(-pow(waveLength * minWaveLength, 2));

        // Move waves along wind direction
        var directionFactor = select(1, -sqrt(1 - directionality), windFactor < 0);

        // Gaussian 
        var u = 2 * PI * random.NextFloat2();
        var v = sqrt(-2 * log(random.NextFloat2()));
        var r = float4(v * cos(u), v * sin(u)).xzyw;

        spectrum[index] = 1 / sqrt(2) * r * (sqrt(phillips) * directionFactor * fftNorm * philNorm).xxyy;
    }
}