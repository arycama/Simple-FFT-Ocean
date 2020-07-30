using UnityEngine;
using Random = UnityEngine.Random;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections;

public class Ocean : MonoBehaviour
{
    [SerializeField, Tooltip("Height factor for the waves")]
    private float amplitude = 0.0002f;

    [SerializeField, Min(0), Tooltip("Speed of the wind in meters/sec")]
    private float windSpeed = 32;

    [SerializeField, Range(0, 1)]
    private float windAngle = 0.125f;

    [SerializeField]
    private float repeatTime = 200;

    [SerializeField, Pow2(1024)]
    private int resolution = 64;

    [SerializeField]
    private float patchSize = 64;

    [SerializeField]
    private float gravity = 9.81f;

    private NativeArray<float2> heightBufferA, heightBufferB;
    private NativeArray<float4> displacementBufferA, displacementBufferB, spectrum;
    private NativeArray<float> dispersionTable, butterflyLookupTable;

    private Texture2D heightMap, normalMap, displacementMap;
    private JobHandle jobHandle;

    public void Recalculate()
    {
        ComputeButterflyLookupTable();

        Random.InitState(0);

        // Init spectrum and dispersion tables
        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var index = x + y * resolution;
                var w_0 = 2.0f * Mathf.PI / repeatTime;
                var kx = Mathf.PI * (2 * x - resolution) / patchSize;
                var kz = Mathf.PI * (2 * y - resolution) / patchSize;
                dispersionTable[index] = Mathf.Floor(Mathf.Sqrt(gravity * Mathf.Sqrt(kx * kx + kz * kz)) / w_0) * w_0;

                var spec = GetSpectrum(x, y);
                var negSpec = GetSpectrum(-x, -y);
                spectrum[index] = new Vector4(spec.x, spec.y, negSpec.x, negSpec.y);
            }
        }
    }

    private void OnEnable()
    {
        dispersionTable = new NativeArray<float>(resolution * resolution, Allocator.Persistent);
        heightBufferA = new NativeArray<float2>(resolution * resolution, Allocator.Persistent);
        heightBufferB = new NativeArray<float2>(resolution * resolution, Allocator.Persistent);

        displacementBufferA = new NativeArray<float4>(resolution * resolution, Allocator.Persistent);
        displacementBufferB = new NativeArray<float4>(resolution * resolution, Allocator.Persistent);

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

    private void Update()
    {
        jobHandle.Complete();
        heightMap.Apply();
        displacementMap.Apply();
        normalMap.Apply();
    }

    private void LateUpdate()
    {
        var dispersion = new OceanDispersionJob(dispersionTable, spectrum, heightBufferB, displacementBufferB, resolution, patchSize, Time.timeSinceLevelLoad);
        var length = resolution * resolution;
        var batchCount = 64;

        jobHandle = dispersion.Schedule(length, batchCount);

        var passes = (int)(Mathf.Log(resolution) / Mathf.Log(2.0f));

        for (var i = 0; i < passes; i++)
        {
            var heightSrc = i % 2 == 1 ? heightBufferA : heightBufferB;
            var heightDst = i % 2 == 1 ? heightBufferB : heightBufferA;
            var dispSrc = i % 2 == 1 ? displacementBufferA : displacementBufferB;
            var dispDst = i % 2 == 1 ? displacementBufferB : displacementBufferA;

            var fftJob = new OceanFFTRowJob(resolution, i, butterflyLookupTable, heightSrc, dispSrc, heightDst, dispDst);
            jobHandle = fftJob.Schedule(length, batchCount, jobHandle);
        }

        for (var i = 0; i < passes - 1; i++)
        {
            var heightSrc = i % 2 == 0 ? heightBufferA : heightBufferB;
            var heightDst = i % 2 == 0 ? heightBufferB : heightBufferA;
            var dispSrc = i % 2 == 0 ? displacementBufferA : displacementBufferB;
            var dispDst = i % 2 == 0 ? displacementBufferB : displacementBufferA;

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

    Vector2 GetSpectrum(int x, int y)
    {
        var waveVector = new Vector2(Mathf.PI * (2 * x - resolution) / patchSize, Mathf.PI * (2 * y - resolution) / patchSize);
        var waveLength = waveVector.magnitude;
        if (waveLength == 0)
        {
            return Vector2.zero;
        }

        //  Base height
        var maxWaveHeight = windSpeed * windSpeed / gravity;
        var spectrum = amplitude * Mathf.Exp(-1.0f / Mathf.Pow(waveLength * maxWaveHeight, 2)) / Mathf.Pow(waveLength, 4);

        // Remove waves perpendicular to wind
        var windRadians = 2.0f * Mathf.PI * windAngle;
        var windDirection = new Vector2(Mathf.Cos(windRadians), Mathf.Sin(windRadians));
        var waveDirection = waveVector / waveLength;
        var windFactor = Mathf.Pow(Vector2.Dot(waveDirection, windDirection), 6); // Phillips spectrum is pow2, but this looks better
        spectrum *= windFactor;

        // Remove small wavelengths
        var minWaveLength = 0.001f;
        spectrum *= Mathf.Exp(-Mathf.Pow(waveLength * minWaveLength, 2));

        // Gaussian 
        var u = 2 * Mathf.PI * Random.value;
        var v = Mathf.Sqrt(-2 * Mathf.Log(Random.value));
        var r = new Vector2(v * Mathf.Cos(u), v * Mathf.Sin(u));

        return r * Mathf.Sqrt(spectrum / 2.0f);
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
        var passes = (int)(Mathf.Log(resolution) / Mathf.Log(2.0f));
        butterflyLookupTable = new NativeArray<float>(resolution * passes * 4, Allocator.Persistent);

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

                    int offset1 = 4 * (i1 + i * resolution);
                    butterflyLookupTable[offset1 + 0] = j1;
                    butterflyLookupTable[offset1 + 1] = j2;
                    butterflyLookupTable[offset1 + 2] = wr;
                    butterflyLookupTable[offset1 + 3] = wi;

                    int offset2 = 4 * (i2 + i * resolution);
                    butterflyLookupTable[offset2 + 0] = j1;
                    butterflyLookupTable[offset2 + 1] = j2;
                    butterflyLookupTable[offset2 + 2] = -wr;
                    butterflyLookupTable[offset2 + 3] = -wi;
                }
            }
        }
    }

    Vector4 FFT(Vector2 w, Vector4 input1, Vector4 input2)
    {
        input1.x += w.x * input2.x - w.y * input2.y;
        input1.y += w.y * input2.x + w.x * input2.y;
        input1.z += w.x * input2.z - w.y * input2.w;
        input1.w += w.y * input2.z + w.x * input2.w;

        return input1;
    }

    Vector2 FFT(Vector2 w, Vector2 input1, Vector2 input2)
    {
        input1.x += w.x * input2.x - w.y * input2.y;
        input1.y += w.y * input2.x + w.x * input2.y;

        return input1;
    }
}
