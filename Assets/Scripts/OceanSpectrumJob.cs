using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections;

public struct OceanSpectrumJob : IJobParallelFor
{
    private float amplitude, directionality, gravity, maxWaveHeight, minWaveLength, patchSize, repeatTime;
    private int resolution;
    private float2 windDirection;
    private Unity.Mathematics.Random random;

    [WriteOnly]
    private NativeArray<float> dispersionTable;

    [WriteOnly]
    private NativeArray<float4> spectrum;

    public OceanSpectrumJob(float amplitude, float directionality, float gravity, float maxWaveHeight, float minWaveLength, float patchSize, float repeatTime, int resolution, float2 windDirection, Unity.Mathematics.Random random, NativeArray<float> dispersionTable, NativeArray<float4> spectrum)
    {
        this.amplitude = amplitude;
        this.directionality = directionality;
        this.gravity = gravity;
        this.maxWaveHeight = maxWaveHeight;
        this.minWaveLength = minWaveLength;
        this.patchSize = patchSize;
        this.repeatTime = repeatTime;
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

        var dispersion = 2 * PI / repeatTime;
        dispersionTable[index] = floor(sqrt(gravity * waveLength) / dispersion) * dispersion;

        if (waveLength == 0)
        {
            spectrum[index] = 0;
            return;
        }

        var fftNorm = pow(resolution, -0.25f);
        var philNorm = E / patchSize;

        var baseHeight = exp(-1 / pow(waveLength * maxWaveHeight, 2)) / pow(waveLength, 4);
        var waveDirection = waveVector / waveLength;
        var windFactor = float2(dot(waveDirection, windDirection), dot(-waveDirection, windDirection));

        // Remove waves facing away from wind direction
        var result = amplitude * fftNorm * philNorm * windFactor * float2(sqrt(baseHeight));

        // Remove waves perpendicular to wind
        // Move waves in wind direction
        result *= select(1, -sqrt(1 - directionality), windFactor < 0);

        // Remove small wavelengths
        result *= exp(-pow(waveLength * minWaveLength, 2));

        // Gaussian 
        var u = 2 * PI * random.NextFloat2();
        var v = sqrt(-2 * log(random.NextFloat2()));
        var r = float4(v * cos(u), v * sin(u)).xzyw;

        spectrum[index] = 1 / sqrt(2) * r * result.xxyy;
    }
}