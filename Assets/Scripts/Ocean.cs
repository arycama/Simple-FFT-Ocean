using UnityEngine;
using Random = UnityEngine.Random;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections;

public class Ocean : MonoBehaviour
{
    [Header("Wind")]
    [SerializeField, Min(0), Tooltip("Speed of the wind in meters/sec")]
    private float windSpeed = 32;

    [SerializeField, Range(0, 1)]
    private float windAngle = 0.125f;

    [SerializeField, Range(0, 1)]
    private float directionality = 0.875f;

    [SerializeField, Tooltip("Height factor for the waves")]
    private float amplitude = 0.0002f;

    [SerializeField]
    private float repeatTime = 200;

    [SerializeField]
    private float patchSize = 64;

    [SerializeField]
    private float gravity = 9.81f;

    [SerializeField, Pow2(1024)]
    private int resolution = 64;

    [SerializeField, Pow2(4096)]
    private int batchCount = 64;

    private bool isInitialized;

    private NativeArray<float4> displacementBufferA, displacementBufferB, butterflyLookupTable, spectrum;
    private NativeArray<float2> heightBufferA, heightBufferB;
    private NativeArray<float> dispersionTable;

    private Texture2D heightMap, normalMap, displacementMap;
    private JobHandle jobHandle;

    public void Recalculate()
    {
        jobHandle.Complete();
        ComputeButterflyLookupTable();
        Random.InitState(0);

        var windRadians = 2 * PI * windAngle;
        var windDirection = new float2(cos(windRadians), sin(windRadians));
        var maxWaveHeight = windSpeed * windSpeed / gravity;
        var minWaveLength = 0.001f;
        var rand = new Unity.Mathematics.Random(1);

        // Init spectrum and dispersion tables
        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var index = x + y * resolution;

                var waveVector = PI * float2(2 * x - resolution, 2 * y - resolution) / patchSize;
                var waveLength = length(waveVector);

                var dispersion = 2 * PI / repeatTime;
                dispersionTable[index] = floor(sqrt(gravity * waveLength) / dispersion) * dispersion;

                if (waveLength == 0)
                {
                    spectrum[index] = 0;
                    continue;
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
                var u = 2 * PI * rand.NextFloat2();
                var v = sqrt(-2 * log(rand.NextFloat2()));
                var r = float4(v * cos(u), v * sin(u)).xzyw;

                spectrum[index] = 1 / sqrt(2) * r * result.xxyy;
            }
        }
    }

    private void OnEnable()
    {
        dispersionTable = new NativeArray<float>(resolution * resolution, Allocator.Persistent);
        spectrum = new NativeArray<float4>(resolution * resolution, Allocator.Persistent);

        heightMap = new Texture2D(resolution, resolution, TextureFormat.RHalf, false) { filterMode = FilterMode.Point };
        displacementMap = new Texture2D(resolution, resolution, TextureFormat.RGHalf, false) { filterMode = FilterMode.Point };
        normalMap = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true);

        Shader.SetGlobalTexture("_OceanHeight", heightMap);
        Shader.SetGlobalTexture("_OceanNormal", normalMap);
        Shader.SetGlobalTexture("_OceanDisplacement", displacementMap);
        Shader.SetGlobalFloat("_OceanScale", patchSize);

        Recalculate();
    }

    private void OnDisable()
    {
        jobHandle.Complete();
        dispersionTable.Dispose();
        spectrum.Dispose();
        butterflyLookupTable.Dispose();
        isInitialized = false;
    }

    private void Update()
    {
        jobHandle.Complete();

        if (isInitialized)
        {
            heightBufferA.Dispose();
            heightBufferB.Dispose();
            displacementBufferA.Dispose();
            displacementBufferB.Dispose();
        }

        // Apply previous changes and start a new calculation
        heightMap.Apply();
        displacementMap.Apply();
        normalMap.Apply();

        var length = resolution * resolution;
        heightBufferA = new NativeArray<float2>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        heightBufferB = new NativeArray<float2>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        displacementBufferA = new NativeArray<float4>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        displacementBufferB = new NativeArray<float4>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        isInitialized = true;

        var dispersion = new OceanDispersionJob(dispersionTable, spectrum, heightBufferB, displacementBufferB, resolution, patchSize, Time.timeSinceLevelLoad);

        jobHandle = dispersion.Schedule(length, batchCount);

        var passes = (int)Mathf.Log(resolution, 2);

        int j = 0;
        for (var i = 0; i < passes; i++, j++)
        {
            var heightSrc = j % 2 == 1 ? heightBufferA : heightBufferB;
            var heightDst = j % 2 == 1 ? heightBufferB : heightBufferA;
            var dispSrc = j % 2 == 1 ? displacementBufferA : displacementBufferB;
            var dispDst = j % 2 == 1 ? displacementBufferB : displacementBufferA;

            var fftJob = new OceanFFTRowJob(resolution, i, butterflyLookupTable, heightSrc, dispSrc, heightDst, dispDst);
            jobHandle = fftJob.Schedule(length, batchCount, jobHandle);
        }

        for (var i = 0; i < passes - 1; i++, j++)
        {
            var heightSrc = j % 2 == 1 ? heightBufferA : heightBufferB;
            var heightDst = j % 2 == 1 ? heightBufferB : heightBufferA;
            var dispSrc = j % 2 == 1 ? displacementBufferA : displacementBufferB;
            var dispDst = j % 2 == 1 ? displacementBufferB : displacementBufferA;

            var fftJob = new OceanFFTColumnJob(resolution, i, butterflyLookupTable, heightSrc, dispSrc, heightDst, dispDst);
            jobHandle = fftJob.Schedule(length, batchCount, jobHandle);
        }

        // Final pass, write to textures
        var heightPixels = heightMap.GetRawTextureData<half>();
        var displacementPixels = displacementMap.GetRawTextureData<half2>();

        var textureJob = new OceanTextureJob(resolution, passes - 1, butterflyLookupTable, heightBufferA, displacementBufferA, heightPixels, displacementPixels);
        jobHandle = textureJob.Schedule(length, batchCount, jobHandle);

        // Generate normal and folding maps
        var normalPixels = normalMap.GetRawTextureData<int>();

        var normalFoldJob = new OceanNormalFoldingJob(resolution, patchSize, heightPixels, displacementPixels, normalPixels);
        jobHandle = normalFoldJob.Schedule(length, batchCount, jobHandle);
    }

    int BitReverse(int i)
    {
        int j = i;
        int Sum = 0;
        int W = 1;
        int M = resolution / 2;
        while (M != 0)
        {
            j = ((i & M) > M - 1) ? 1 : 0;
            Sum += j * W;
            W *= 2;
            M /= 2;
        }
        return Sum;
    }

    void ComputeButterflyLookupTable()
    {
        var passes = (int)Mathf.Log(resolution, 2);
        butterflyLookupTable = new NativeArray<float4>(resolution * passes, Allocator.Persistent);

        for (var i = 0; i < passes; i++)
        {
            int nBlocks = (int)Mathf.Pow(2, passes - 1 - i);
            int nHInputs = (int)Mathf.Pow(2, i);

            for (var j = 0; j < nBlocks; j++)
            {
                for (int k = 0; k < nHInputs; k++)
                {
                    int i1, i2, j1, j2;
                    if (i == 0)
                    {
                        i1 = j * nHInputs * 2 + k;
                        i2 = j * nHInputs * 2 + nHInputs + k;
                        j1 = BitReverse(i1);
                        j2 = BitReverse(i2);
                    }
                    else
                    {
                        i1 = j * nHInputs * 2 + k;
                        i2 = j * nHInputs * 2 + nHInputs + k;
                        j1 = i1;
                        j2 = i2;
                    }

                    float wr = Mathf.Cos(2.0f * Mathf.PI * (k * nBlocks) / resolution);
                    float wi = Mathf.Sin(2.0f * Mathf.PI * (k * nBlocks) / resolution);

                    int offset1 = (i1 + i * resolution);
                    butterflyLookupTable[offset1] = float4(j1, j2, wr, wi);

                    int offset2 = (i2 + i * resolution);
                    butterflyLookupTable[offset2] = float4(j1, j2, -wr, -wi);
                }
            }
        }
    }
}
