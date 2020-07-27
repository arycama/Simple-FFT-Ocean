using UnityEngine;
using Random = UnityEngine.Random;

public class Ocean : MonoBehaviour
{
    [SerializeField]
    private float amplitude = 0.0002f;

    [SerializeField]
    private Vector2 windSpeed = new Vector2(32.0f, 32.0f);

    [SerializeField, Range(-2, 2)]
    private float choppyness = -1.0f;

    [SerializeField]
    private float repeatTime = 200;

    [SerializeField]
    private int resolution = 64;

    [SerializeField]
    private float patchSize = 64;

    [SerializeField]
    private float gravity = 9.81f;

    [SerializeField]
    private Material material = null;

    [SerializeField]
    private int numGridsX = 2;

    [SerializeField]
    private int numGridsZ = 2;

    private GameObject[] oceanGrid;
    private Mesh mesh;
    private Vector2[,] heightBuffer;
    private Vector4[,] slopeBuffer, displacementBuffer;
    private Vector2[] spectrum, spectruconj;
    private Vector3[] position, vertices, normals;
    private float[] dispersionTable;
    private float[] butterflyLookupTable = null;

    public Texture2D heightMap, normalMap, displacementMap;
    private Color[] displacementPixels, normalPixels;
    private Color[] heightPixels;

    private void OnEnable()
    {
        ComputeButterflyLookupTable();

        dispersionTable = new float[(resolution + 1) * (resolution + 1)];

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

        heightBuffer = new Vector2[2, resolution * resolution];
        slopeBuffer = new Vector4[2, resolution * resolution];
        displacementBuffer = new Vector4[2, resolution * resolution];

        spectrum = new Vector2[(resolution + 1) * (resolution + 1)];
        spectruconj = new Vector2[(resolution + 1) * (resolution + 1)];

        position = new Vector3[(resolution + 1) * (resolution + 1)];
        vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        normals = new Vector3[(resolution + 1) * (resolution + 1)];

        MakeMesh();
        oceanGrid = new GameObject[numGridsX * numGridsZ];

        for (var x = 0; x < numGridsX; x++)
        {
            for (var z = 0; z < numGridsZ; z++)
            {
                var idx = x + z * numGridsX;

                oceanGrid[idx] = new GameObject("Ocean grid " + idx.ToString());
                oceanGrid[idx].AddComponent<MeshFilter>();
                oceanGrid[idx].AddComponent<MeshRenderer>();
                oceanGrid[idx].GetComponent<Renderer>().material = material;
                oceanGrid[idx].GetComponent<MeshFilter>().mesh = mesh;
                oceanGrid[idx].transform.Translate(new Vector3(x * patchSize - numGridsX * patchSize / 2, 0.0f, z * patchSize - numGridsZ * patchSize / 2));
                oceanGrid[idx].transform.parent = this.transform;
            }
        }

        Random.InitState(0);
        var meshVertices = mesh.vertices;

        for (int y = 0; y < (resolution + 1); y++)
        {
            for (int x = 0; x < (resolution + 1); x++)
            {
                int index = y * (resolution + 1) + x;

                spectrum[index] = GetSpectrum(x, y);
                spectruconj[index] = GetSpectrum(-x, -y);
                spectruconj[index].y *= -1.0f;

                position[index].x = meshVertices[index].x = x * patchSize / resolution;
                position[index].y = meshVertices[index].y = 0.0f;
                position[index].z = meshVertices[index].z = y * patchSize / resolution;
            }
        }

        mesh.vertices = meshVertices;
        mesh.RecalculateBounds();

        heightMap = new Texture2D(resolution, resolution, TextureFormat.RFloat, false) { filterMode = FilterMode.Point };
        displacementMap = new Texture2D(resolution, resolution, TextureFormat.RGFloat, false) { filterMode = FilterMode.Point };
        normalMap = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false);

        heightPixels = new Color[resolution * resolution];
        normalPixels = new Color[resolution * resolution];
        displacementPixels = new Color[resolution * resolution];

        Shader.SetGlobalTexture("_OceanHeight", heightMap);
        Shader.SetGlobalTexture("_OceanNormal", normalMap);
        Shader.SetGlobalTexture("_OceanDisplacement", displacementMap);
        Shader.SetGlobalFloat("_OceanScale", patchSize);
    }

    private void Update()
    {
        var t = Time.timeSinceLevelLoad;

        for (int y = 0; y < resolution; y++)
        {
            var kz = Mathf.PI * (2.0f * y - resolution) / patchSize;
            for (int x = 0; x < resolution; x++)
            {
                var kx = Mathf.PI * (2 * x - resolution) / patchSize;
                var len = Mathf.Sqrt(kx * kx + kz * kz);
                var index = y * resolution + x;

                // Init spectrum
                int iindex = y * (resolution + 1) + x;

                float omegat = dispersionTable[iindex] * t;

                float cos = Mathf.Cos(omegat);
                float sin = Mathf.Sin(omegat);

                float c0a = spectrum[iindex].x * cos - spectrum[iindex].y * sin;
                float c0b = spectrum[iindex].x * sin + spectrum[iindex].y * cos;

                float c1a = spectruconj[iindex].x * cos - spectruconj[iindex].y * -sin;
                float c1b = spectruconj[iindex].x * -sin + spectruconj[iindex].y * cos;

                var c = new Vector2(c0a + c1a, c0b + c1b);

                heightBuffer[1, index].x = c.x;
                heightBuffer[1, index].y = c.y;

                slopeBuffer[1, index].x = -c.y * kx;
                slopeBuffer[1, index].y = c.x * kx;

                slopeBuffer[1, index].z = -c.y * kz;
                slopeBuffer[1, index].w = c.x * kz;

                if (len < 0.000001f)
                {
                    displacementBuffer[1, index].x = 0.0f;
                    displacementBuffer[1, index].y = 0.0f;
                    displacementBuffer[1, index].z = 0.0f;
                    displacementBuffer[1, index].w = 0.0f;
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

        PeformFFT(heightBuffer, slopeBuffer, displacementBuffer);

        // Apply to mesh (Or textures)
        for (var y = 0; y < resolution; y++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var index = y * resolution + x;          // index into buffers
                var index1 = y * (resolution + 1) + x;    // index into vertices

                var sign = ((x + y) & 1) == 0 ? 1 : -1;

                // height
                vertices[index1].y = heightBuffer[1, index].x * sign;

                // displacement
                vertices[index1].x = position[index1].x + displacementBuffer[1, index].x * choppyness * sign;
                vertices[index1].z = position[index1].z + displacementBuffer[1, index].z * choppyness * sign;

                // normal
                var n = new Vector3(-slopeBuffer[1, index].x * sign, 1.0f, -slopeBuffer[1, index].z * sign);
                n.Normalize();

                normals[index1].x = n.x;
                normals[index1].y = n.y;
                normals[index1].z = n.z;

                // Textures
                var dispX = displacementBuffer[1, index].x * choppyness * sign;
                var dispZ =  displacementBuffer[1, index].z * choppyness * sign;

                heightPixels[index] = Color.red * heightBuffer[1, index].x * sign;
                displacementPixels[index] = new Color(dispX, dispZ, 0, 0);
                normalPixels[index] = new Color(n.x, n.y, n.z);

                // for tiling
                if (x == 0 && y == 0)
                {
                    vertices[index1 + resolution + (resolution + 1) * resolution].y = heightBuffer[1, index].x * sign;

                    vertices[index1 + resolution + (resolution + 1) * resolution].x = position[index1 + resolution + (resolution + 1) * resolution].x + displacementBuffer[1, index].x * choppyness * sign;
                    vertices[index1 + resolution + (resolution + 1) * resolution].z = position[index1 + resolution + (resolution + 1) * resolution].z + displacementBuffer[1, index].z * choppyness * sign;

                    normals[index1 + resolution + (resolution + 1) * resolution].x = n.x;
                    normals[index1 + resolution + (resolution + 1) * resolution].y = n.y;
                    normals[index1 + resolution + (resolution + 1) * resolution].z = n.z;
                }
                if (x == 0)
                {
                    vertices[index1 + resolution].y = heightBuffer[1, index].x * sign;

                    vertices[index1 + resolution].x = position[index1 + resolution].x + displacementBuffer[1, index].x * choppyness * sign;
                    vertices[index1 + resolution].z = position[index1 + resolution].z + displacementBuffer[1, index].z * choppyness * sign;

                    normals[index1 + resolution].x = n.x;
                    normals[index1 + resolution].y = n.y;
                    normals[index1 + resolution].z = n.z;
                }
                if (y == 0)
                {
                    vertices[index1 + (resolution + 1) * resolution].y = heightBuffer[1, index].x * sign;

                    vertices[index1 + (resolution + 1) * resolution].x = position[index1 + (resolution + 1) * resolution].x + displacementBuffer[1, index].x * choppyness * sign;
                    vertices[index1 + (resolution + 1) * resolution].z = position[index1 + (resolution + 1) * resolution].z + displacementBuffer[1, index].z * choppyness * sign;

                    normals[index1 + (resolution + 1) * resolution].x = n.x;
                    normals[index1 + (resolution + 1) * resolution].y = n.y;
                    normals[index1 + (resolution + 1) * resolution].z = n.z;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.RecalculateBounds();

        heightMap.SetPixels(heightPixels, 0);
        displacementMap.SetPixels(displacementPixels, 0);
        normalMap.SetPixels(normalPixels, 0);

        heightMap.Apply(false, false);
        displacementMap.Apply(false, false);
        normalMap.Apply(false, false);
    }

    Vector2 GetSpectrum(int x, int y)
    {
        // Gaussian Random Number
        float x1, x2, w;
        do
        {
            x1 = 2.0f * Random.value - 1.0f;
            x2 = 2.0f * Random.value - 1.0f;
            w = x1 * x1 + x2 * x2;
        }
        while (w >= 1.0f);

        w = Mathf.Sqrt((-2.0f * Mathf.Log(w)) / w);
        var r = new Vector2(x1 * w, x2 * w);

        Vector2 k = new Vector2(Mathf.PI * (2 * x - resolution) / patchSize, Mathf.PI * (2 * y - resolution) / patchSize);
        float k_length = k.magnitude;
        if (k_length < 0.000001f)
        {
            return Vector2.zero;
        }

        float k_length2 = k_length * k_length;
        float k_length4 = k_length2 * k_length2;

        k.Normalize();

        float k_dot_w = Vector2.Dot(k, windSpeed.normalized);
        float k_dot_w2 = k_dot_w * k_dot_w * k_dot_w * k_dot_w * k_dot_w * k_dot_w;

        float w_length = windSpeed.magnitude;
        float L = w_length * w_length / gravity;
        float L2 = L * L;

        float damping = 0.001f;
        float l2 = L2 * damping * damping;

        var spectrum = amplitude * Mathf.Exp(-1.0f / (k_length2 * L2)) / k_length4 * k_dot_w2 * Mathf.Exp(-k_length2 * l2);
        return r * Mathf.Sqrt(spectrum / 2.0f);
    }

    private void MakeMesh()
    {
        var size = resolution + 1;

        Vector3[] vertices = new Vector3[size * size];
        Vector2[] texcoords = new Vector2[size * size];
        Vector3[] normals = new Vector3[size * size];
        int[] indices = new int[size * size * 6];

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 uv = new Vector3((float)x / (float)(size - 1), (float)y / (float)(size - 1));
                Vector3 pos = new Vector3(x, 0.0f, y);
                Vector3 norm = new Vector3(0.0f, 1.0f, 0.0f);

                texcoords[x + y * size] = uv;
                vertices[x + y * size] = pos;
                normals[x + y * size] = norm;
            }
        }

        int num = 0;
        for (int x = 0; x < size - 1; x++)
        {
            for (int y = 0; y < size - 1; y++)
            {
                indices[num++] = x + y * size;
                indices[num++] = x + (y + 1) * size;
                indices[num++] = (x + 1) + y * size;

                indices[num++] = x + (y + 1) * size;
                indices[num++] = (x + 1) + (y + 1) * size;
                indices[num++] = (x + 1) + y * size;
            }
        }

        mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = texcoords;
        mesh.triangles = indices;
        mesh.normals = normals;
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

    private void PeformFFT(Vector2[,] data0, Vector4[,] data1, Vector4[,] data2)
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

                    data0[idx, x + y * resolution] = FFT(w, data0[idx1, X + y * resolution], data0[idx1, Y + y * resolution]);
                    data1[idx, x + y * resolution] = FFT(w, data1[idx1, X + y * resolution], data1[idx1, Y + y * resolution]);
                    data2[idx, x + y * resolution] = FFT(w, data2[idx1, X + y * resolution], data2[idx1, Y + y * resolution]);
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

                    data0[idx, x + y * resolution] = FFT(w, data0[idx1, x + X * resolution], data0[idx1, x + Y * resolution]);
                    data1[idx, x + y * resolution] = FFT(w, data1[idx1, x + X * resolution], data1[idx1, x + Y * resolution]);
                    data2[idx, x + y * resolution] = FFT(w, data2[idx1, x + X * resolution], data2[idx1, x + Y * resolution]);
                }
            }
        }
    }
}
