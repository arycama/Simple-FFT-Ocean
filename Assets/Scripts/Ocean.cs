using UnityEngine;
using Random = UnityEngine.Random;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class Ocean : MonoBehaviour
{
    [SerializeField, Tooltip("Height factor for the waves")]
    private float amplitude = 0.0002f;

    [SerializeField, Min(0), Tooltip("Speed of the wind in meters/sec")]
    private float windSpeed = 32;

    [SerializeField, Range(0, 1)]
    private float windAngle = 0.125f;

    [SerializeField, Range(0, 1)]
    private float choppyness = 1;

    [SerializeField]
    private float repeatTime = 200;

    [SerializeField, Pow2(1024)]
    private int resolution = 64;

    [SerializeField]
    private float patchSize = 64;

    [SerializeField]
    private float gravity = 9.81f;

    private Vector2[] heightBufferA, heightBufferB;
    private Vector4[] displacementBufferA, displacementBufferB;

    private Vector4[] spectrum;
    private float[] dispersionTable, butterflyLookupTable;

    private Texture2D heightMap, normalMap, displacementMap;

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
        dispersionTable = new float[resolution * resolution];
        heightBufferA = new Vector2[resolution * resolution];
        heightBufferB = new Vector2[resolution * resolution];

        displacementBufferA = new Vector4[resolution * resolution];
        displacementBufferB = new Vector4[resolution * resolution];

        spectrum = new Vector4[resolution * resolution];

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
        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                // These could almost be precomputed.. not sure if fetching would take more than computing them again though
                // We possibly multiply a bunch of stuff by it anyway.. look into soon
                var waveVectorX = Mathf.PI * (2 * x - resolution) / patchSize;
                var waveVectorZ = Mathf.PI * (2.0f * y - resolution) / patchSize;

                var waveLength = Mathf.Sqrt(waveVectorX * waveVectorX + waveVectorZ * waveVectorZ);
                var index = x + y * resolution;

                float omegat = dispersionTable[index] * Time.timeSinceLevelLoad;

                float cos = Mathf.Cos(omegat);
                float sin = Mathf.Sin(omegat);

                var spec = spectrum[index];
                var c0a = spec.x * cos - spec.y * sin;
                var c0b = spec.x * sin + spec.y * cos;
                var c1a = spec.z * cos - spec.w * sin;
                var c1b = spec.z * -sin + spec.w * -cos;

                var c = new Vector2(c0a + c1a, c0b + c1b);

                heightBufferB[index] = c;

                if (waveLength == 0)
                {
                    displacementBufferB[index] = new Vector4(0, 0, 0, 0);
                }
                else
                {
                    displacementBufferB[index].x = -c.y * -(waveVectorX / waveLength);
                    displacementBufferB[index].y = c.x * -(waveVectorX / waveLength);
                    displacementBufferB[index].z = -c.y * -(waveVectorZ / waveLength);
                    displacementBufferB[index].w = c.x * -(waveVectorZ / waveLength);
                }
            }
        }

        var passes = (int)(Mathf.Log(resolution) / Mathf.Log(2.0f));

        for (var i = 0; i < passes; i++)
        {
            var heightSrc = i % 2 == 1 ? heightBufferA : heightBufferB;
            var heightDst = i % 2 == 1 ? heightBufferB : heightBufferA;
            var dispSrc = i % 2 == 1 ? displacementBufferA : displacementBufferB;
            var dispDst = i % 2 == 1 ? displacementBufferB : displacementBufferA;

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var bftIdx = 4 * (x + i * resolution);

                    var X = (int)butterflyLookupTable[bftIdx + 0];
                    var Y = (int)butterflyLookupTable[bftIdx + 1];
                    Vector2 w;
                    w.x = butterflyLookupTable[bftIdx + 2];
                    w.y = butterflyLookupTable[bftIdx + 3];

                    heightDst[x + y * resolution] = FFT(w, heightSrc[X + y * resolution], heightSrc[Y + y * resolution]);
                    dispDst[x + y * resolution] = FFT(w, dispSrc[X + y * resolution], dispSrc[Y + y * resolution]);
                }
            }
        }

        for (var i = 0; i < passes - 1; i++)
        {
            var heightSrc = i % 2 == 0 ? heightBufferA : heightBufferB;
            var heightDst = i % 2 == 0 ? heightBufferB : heightBufferA;
            var dispSrc = i % 2 == 0 ? displacementBufferA : displacementBufferB;
            var dispDst = i % 2 == 0 ? displacementBufferB : displacementBufferA;

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var bftIdx = 4 * (y + i * resolution);

                    var X = (int)butterflyLookupTable[bftIdx + 0];
                    var Y = (int)butterflyLookupTable[bftIdx + 1];
                    Vector2 w;
                    w.x = butterflyLookupTable[bftIdx + 2];
                    w.y = butterflyLookupTable[bftIdx + 3];

                    heightDst[x + y * resolution] = FFT(w, heightSrc[x + X * resolution], heightSrc[x + Y * resolution]);
                    dispDst[x + y * resolution] = FFT(w, dispSrc[x + X * resolution], dispSrc[x + Y * resolution]);
                }
            }
        }

        // Final pass, write directly to textures
        var heightPixels = heightMap.GetRawTextureData<half>();
        var displacementPixels = displacementMap.GetRawTextureData<half2>();

        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                // Final FFT
                var bftIdx = 4 * (y + (passes - 1) * resolution);

                var X = (int)butterflyLookupTable[bftIdx + 0];
                var Y = (int)butterflyLookupTable[bftIdx + 1];
                Vector2 w;
                w.x = butterflyLookupTable[bftIdx + 2];
                w.y = butterflyLookupTable[bftIdx + 3];

                var heightResult = FFT(w, heightBufferA[x + X * resolution], heightBufferA[x + Y * resolution]);
                var dispResult = FFT(w, displacementBufferA[x + X * resolution], displacementBufferA[x + Y * resolution]);

                var index = y * resolution + x;
                var sign = ((x + y) & 1) == 0 ? 1 : -1;

                heightPixels[index] = (half)(heightResult.x * sign);

                var dispX = (half)(-dispResult.x * choppyness * sign);
                var dispZ = (half)(-dispResult.z * choppyness * sign);
                displacementPixels[index] = half2(dispX, dispZ);
            }
        }

        var normalPixels = normalMap.GetRawTextureData<int>();

        // Need to do this after above loop, as it accesses neighboring pixels in the arrays
        // Could even do this on the GPU... not like we need it on the cpu. (Though folding would be handy for spray, but..)
        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var index = y * resolution + x;

                // Calculate normal from displacement
                var left = MathUtils.Wrap(x - 1, resolution) + y * resolution;
                var right = MathUtils.Wrap(x + 1, resolution) + y * resolution;
                var down = x + MathUtils.Wrap(y - 1, resolution) * resolution;
                var up = x + MathUtils.Wrap(y + 1, resolution) * resolution;

                // Use central diff, then try with finite to see if quality is similar
                var delta = resolution / patchSize;

                var xSlope = heightPixels[left] - heightPixels[right];
                var zSlope = heightPixels[down] - heightPixels[up];

                // Store foam (folding) in w
                var dx = (float2)displacementPixels[left] - (float2)displacementPixels[right];
                var dz = (float2)displacementPixels[down] - (float2)displacementPixels[right];

                var jx = -dx * delta;
                var jz = -dz * delta;

                var jacobian = (1 + jx.x) * (1 + jz.y) - jx.y * jz.x;

                var normalFolding = int4((float4(normalize(float3(xSlope * delta, 2, zSlope * delta)) * 0.5f + 0.5f, saturate(jacobian))) * 255);

                normalPixels[index] = normalFolding.x | normalFolding.y << 8 | normalFolding.z << 16 | normalFolding.w << 24;
            }
        }

        heightMap.Apply();
        displacementMap.Apply();
        normalMap.Apply();
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
        butterflyLookupTable = new float[resolution * passes * 4];

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
