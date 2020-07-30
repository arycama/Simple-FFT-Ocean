using UnityEngine;
using Random = UnityEngine.Random;

public class Ocean : MonoBehaviour
{
    [SerializeField, Tooltip("Height factor for the waves")]
    private float amplitude = 0.0002f;

    [SerializeField, Min(0), Tooltip("Speed of the wind in meters/sec")]
    private float windSpeed = 32;

    [SerializeField, Range(0, 1)]
    private float windAngle;

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

    private Vector2[,] heightBuffer;
    private Vector4[,] slopeBuffer, displacementBuffer;
    private Vector2[] spectrum, spectruconj;
    private float[] dispersionTable;
    private float[] butterflyLookupTable = null;

    private Texture2D heightMap, normalMap, displacementMap;

    public void Recalculate()
    {
        ComputeButterflyLookupTable();

        for (var y = 0; y <= resolution; y++)
        {
            for (var x = 0; x <= resolution; x++)
            {
                var index = x + y * (resolution + 1);
                var w_0 = 2.0f * Mathf.PI / repeatTime;
                var kx = Mathf.PI * (2 * x - resolution) / patchSize;
                var kz = Mathf.PI * (2 * y - resolution) / patchSize;
                dispersionTable[index] = Mathf.Floor(Mathf.Sqrt(gravity * Mathf.Sqrt(kx * kx + kz * kz)) / w_0) * w_0;
            }
        }

        Random.InitState(0);
        for (var y = 0; y <= resolution; y++)
        {
            for (var x = 0; x <= resolution; x++)
            {
                var index = y * (resolution + 1) + x;
                spectrum[index] = GetSpectrum(x, y);
                spectruconj[index] = GetSpectrum(-x, -y);
                spectruconj[index].y *= -1.0f;
            }
        }
    }

    private void OnEnable()
    {
        dispersionTable = new float[(resolution + 1) * (resolution + 1)];
        heightBuffer = new Vector2[2, resolution * resolution];
        slopeBuffer = new Vector4[2, resolution * resolution];
        displacementBuffer = new Vector4[2, resolution * resolution];

        spectrum = new Vector2[(resolution + 1) * (resolution + 1)];
        spectruconj = new Vector2[(resolution + 1) * (resolution + 1)];

        heightMap = new Texture2D(resolution, resolution, TextureFormat.RHalf, false) { filterMode = FilterMode.Point };
        displacementMap = new Texture2D(resolution, resolution, TextureFormat.RGHalf, false) { filterMode = FilterMode.Point };
        normalMap = new Texture2D(resolution, resolution, TextureFormat.RG16, false);

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
            var kz = Mathf.PI * (2.0f * y - resolution) / patchSize;

            for (var x = 0; x < resolution; x++)
            {
                var kx = Mathf.PI * (2 * x - resolution) / patchSize;
                var len = Mathf.Sqrt(kx * kx + kz * kz);
                var index = y * resolution + x;

                // Init spectrum
                int iindex = y * (resolution + 1) + x;

                float omegat = dispersionTable[iindex] * Time.timeSinceLevelLoad;

                float cos = Mathf.Cos(omegat);
                float sin = Mathf.Sin(omegat);

                float c0a = spectrum[iindex].x * cos - spectrum[iindex].y * sin;
                float c0b = spectrum[iindex].x * sin + spectrum[iindex].y * cos;

                float c1a = spectruconj[iindex].x * cos - spectruconj[iindex].y * -sin;
                float c1b = spectruconj[iindex].x * -sin + spectruconj[iindex].y * cos;

                var c = new Vector2(c0a + c1a, c0b + c1b);

                heightBuffer[1, index] = c;
                slopeBuffer[1, index] = new Vector4(-c.y * kx, c.x * kx, -c.y * kz, c.x * kz);

                if (len == 0)
                {
                    displacementBuffer[1, index] = new Vector4(0, 0, 0, 0);
                }
                else
                {
                    displacementBuffer[1, index].x = -c.y * -(kx / len);
                    displacementBuffer[1, index].y = c.x * -(kx / len);
                    displacementBuffer[1, index].z = -c.y * -(kz / len);
                    displacementBuffer[1, index].w = c.x * -(kz / len);
                }
            }
        }

        PeformFFT();

        var heightPixels = heightMap.GetRawTextureData<ushort>();
        var normalPixels = normalMap.GetRawTextureData<ushort>();
        var displacementPixels = displacementMap.GetRawTextureData<uint>();

        // Apply to mesh (Or textures)
        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var index = y * resolution + x;
                var sign = ((x + y) & 1) == 0 ? 1 : -1;

                // Textures
                var dispX = Mathf.FloatToHalf(-displacementBuffer[1, index].x * choppyness * sign);
                var dispZ = Mathf.FloatToHalf(-displacementBuffer[1, index].z * choppyness * sign);

                var n = new Vector3(-slopeBuffer[1, index].x * sign, 1.0f, -slopeBuffer[1, index].z * sign).normalized;
                var normalX = (int)((n.x * 0.5f + 0.5f) * 255);
                var normalY = (int)((n.z * 0.5f + 0.5f) * 255);
                normalPixels[index] = (ushort)(normalX | normalY << 8);
                heightPixels[index] = Mathf.FloatToHalf(heightBuffer[1, index].x * sign);
                displacementPixels[index] = (ushort)(dispX | dispZ << 16);
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
        var windFactor = Mathf.Pow(Vector2.Dot(waveDirection, windDirection), 6);
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

    private void PeformFFT()
    {
        var passes = (int)(Mathf.Log(resolution) / Mathf.Log(2.0f));

        int j = 0;
        for (var i = 0; i < passes; i++, j++)
        {
            var idx = j % 2;
            var idx1 = (j + 1) % 2;

            for (var x = 0; x < resolution; x++)
            {
                for (var y = 0; y < resolution; y++)
                {
                    var bftIdx = 4 * (x + i * resolution);

                    var X = (int)butterflyLookupTable[bftIdx + 0];
                    var Y = (int)butterflyLookupTable[bftIdx + 1];
                    Vector2 w;
                    w.x = butterflyLookupTable[bftIdx + 2];
                    w.y = butterflyLookupTable[bftIdx + 3];

                    heightBuffer[idx, x + y * resolution] = FFT(w, heightBuffer[idx1, X + y * resolution], heightBuffer[idx1, Y + y * resolution]);
                    slopeBuffer[idx, x + y * resolution] = FFT(w, slopeBuffer[idx1, X + y * resolution], slopeBuffer[idx1, Y + y * resolution]);
                    displacementBuffer[idx, x + y * resolution] = FFT(w, displacementBuffer[idx1, X + y * resolution], displacementBuffer[idx1, Y + y * resolution]);
                }
            }
        }

        for (var i = 0; i < passes; i++, j++)
        {
            var idx = j % 2;
            var idx1 = (j + 1) % 2;

            for (var x = 0; x < resolution; x++)
            {
                for (var y = 0; y < resolution; y++)
                {
                    var bftIdx = 4 * (y + i * resolution);

                    var X = (int)butterflyLookupTable[bftIdx + 0];
                    var Y = (int)butterflyLookupTable[bftIdx + 1];
                    Vector2 w;
                    w.x = butterflyLookupTable[bftIdx + 2];
                    w.y = butterflyLookupTable[bftIdx + 3];

                    heightBuffer[idx, x + y * resolution] = FFT(w, heightBuffer[idx1, x + X * resolution], heightBuffer[idx1, x + Y * resolution]);
                    slopeBuffer[idx, x + y * resolution] = FFT(w, slopeBuffer[idx1, x + X * resolution], slopeBuffer[idx1, x + Y * resolution]);
                    displacementBuffer[idx, x + y * resolution] = FFT(w, displacementBuffer[idx1, x + X * resolution], displacementBuffer[idx1, x + Y * resolution]);
                }
            }
        }
    }
}
