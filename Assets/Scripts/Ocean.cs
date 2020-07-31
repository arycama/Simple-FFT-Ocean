using UnityEngine;
using Random = UnityEngine.Random;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class Ocean : MonoBehaviour
{
    private const int batchCount = 64;

    [Header("Wind")]
    [SerializeField, Min(0), Tooltip("Speed of the wind in meters/sec. Affects wave size and speed")]
    private float windSpeed = 32;

    [SerializeField, Range(0, 1), Tooltip("Angle of the wind")]
    private float windAngle = 0.125f;

    [SerializeField, Range(0, 1), Tooltip("How aligned the waves are to the wind")]
    private float directionality = 0.875f;

    [SerializeField, Min(0), Tooltip("Size of the simulation in world space, smaller values give more detail, but look more repetitive")]
    private float patchSize = 64;

    [SerializeField, Min(0), Tooltip("Gravity in m/s, affects wave size to speed ratio")]
    private float gravity = 9.81f;

    [SerializeField, Pow2(1024), Tooltip("Resolution of the simulation, higher values produce more detail, but are more performance-intensive")]
    private int resolution = 64;

    private bool isInitialized;

    private NativeArray<float4> displacementBufferA, displacementBufferB, butterflyLookupTable, spectrum;
    private NativeArray<float2> heightBufferA, heightBufferB;
    private NativeArray<float> dispersionTable;

    private Texture2D heightMap, normalMap, displacementMap;
    private JobHandle jobHandle;

    /// <summary>
    /// Rebuilds all the tables
    /// </summary>
    public void ReInitialize()
    {
        dispersionTable = new NativeArray<float>(resolution * resolution, Allocator.Persistent);
        spectrum = new NativeArray<float4>(resolution * resolution, Allocator.Persistent);

        heightMap = new Texture2D(resolution, resolution, TextureFormat.RHalf, false);
        displacementMap = new Texture2D(resolution, resolution, TextureFormat.RGHalf, false);
        normalMap = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true);

        ComputeButterflyLookupTable();

        Shader.SetGlobalTexture("_OceanHeight", heightMap);
        Shader.SetGlobalTexture("_OceanNormal", normalMap);
        Shader.SetGlobalTexture("_OceanDisplacement", displacementMap);

        Recalculate();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.update += UpdateSimulation;
#endif
    }

    public void Cleanup()
    {
        jobHandle.Complete();
        dispersionTable.Dispose();
        spectrum.Dispose();
        butterflyLookupTable.Dispose();
        isInitialized = false;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.update -= UpdateSimulation;
#endif
    }

    public void Recalculate()
    {
        jobHandle.Complete();
        Random.InitState(0);

        var windRadians = 2 * PI * windAngle;
        var windDirection = new float2(cos(windRadians), sin(windRadians));
        var maxWaveHeight = windSpeed * windSpeed / gravity;
        var rand = new Unity.Mathematics.Random(1);

        var spectrumJob = new OceanSpectrumJob(directionality, gravity, maxWaveHeight, 0.001f, patchSize, resolution, windDirection, rand, dispersionTable, spectrum);
        jobHandle = spectrumJob.Schedule(resolution * resolution, 64);
        jobHandle.Complete();

        Shader.SetGlobalFloat("_OceanScale", patchSize);
    }

    private void OnEnable()
    {
        ReInitialize();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if(Application.isPlaying)
#endif
        UpdateSimulation();
    }

    private void UpdateSimulation()
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

        var time = Time.timeSinceLevelLoad;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            time = (float)EditorApplication.timeSinceStartup;
#endif

        var dispersion = new OceanDispersionJob(dispersionTable, spectrum, heightBufferB, displacementBufferB, resolution, patchSize, time);

        jobHandle = dispersion.Schedule(length, batchCount);

        var passes = (int)Mathf.Log(resolution, 2);

        var j = 0;
        for (var i = 0; i < passes; i++, j++)
        {
            OceanFFTRowJob fftJob;
            if (j % 2 == 0)
            {
                fftJob = new OceanFFTRowJob(resolution, i, butterflyLookupTable, heightBufferB, displacementBufferB, heightBufferA, displacementBufferA);
            }
            else
            {
                fftJob = new OceanFFTRowJob(resolution, i, butterflyLookupTable, heightBufferA, displacementBufferA, heightBufferB, displacementBufferB);
            }

            jobHandle = fftJob.Schedule(length, batchCount, jobHandle);
        }

        for (var i = 0; i < passes - 1; i++, j++)
        {
            OceanFFTColumnJob fftJob;
            if (j % 2 == 0)
            {
                fftJob = new OceanFFTColumnJob(resolution, i, butterflyLookupTable, heightBufferB, displacementBufferB, heightBufferA, displacementBufferA);
            }
            else
            {
                fftJob = new OceanFFTColumnJob(resolution, i, butterflyLookupTable, heightBufferA, displacementBufferA, heightBufferB, displacementBufferB);
            }

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