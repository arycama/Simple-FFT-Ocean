using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[ExecuteAlways]
public class AtmosphereManager : MonoBehaviour
{
    public static event Action<AtmosphereProfile> OnProfileUpdated;
    public static bool AtmosphereInitialized { get; private set; }

    public Light light;

    [SerializeField]
    private AtmosphereProfile atmosphereProfile = null;

    [Header("Precomputation")]
    [SerializeField, Range(1, 128)]
    private int transmittanceSamples = 32;

    [SerializeField, Range(1, 256)]
    private int scatterSamples = 64;

    [SerializeField, Range(1, 8)]
    private int scatteringOrders = 4;

    [SerializeField, Range(1, 32)]
    private int multiScatterSamples = 8;

    [SerializeField, Range(1, 128)]
    private int ambientSteps = 8;

    [SerializeField]
    private Vector2Int transmittanceResolution = new Vector2Int(32, 128);

    [SerializeField]
    private Vector3Int scatterResolution = new Vector3Int(32, 128, 32);

    [SerializeField]
    private Vector2Int gatherResolution = new Vector2Int(64, 64);

    [SerializeField]
    private Vector2Int ambientResolution = new Vector2Int(128, 128);

    [SerializeField]
    private Vector2Int lightResolution = new Vector2Int(256, 256);

    private Material skybox;
    private RenderTexture atmosphereScatter, atmosphereAmbient;
    private ComputeShader computeTransmittance, computeInScatter, computeGather, computeMultiScatter, computeAmbient;
    private Texture2D directionalLightLookup;

    public AtmosphereProfile AtmosphereProfile => atmosphereProfile;
    public RenderTexture AtmosphereTransmittance { get; private set; }
    public RenderTexture AtmosphereGather { get; private set; }

    public Color GetDirectionalLightColor(float angle, float height)
    {
        var normalizedHeight = Mathf.Clamp01(height / atmosphereProfile.AtmosphereHeight);
        var normalizedAngle = 1.0f - Mathf.Clamp01(0.5f * angle + 0.5f);

        return directionalLightLookup.GetPixelBilinear(normalizedHeight, normalizedAngle);
    }

    private void CalculateDirectionalLightLookup()
    {
        directionalLightLookup = new Texture2D(lightResolution.x, lightResolution.y, TextureFormat.ARGB32, false, true)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        // Precalculate some variables
        Vector4 extinction;
        extinction.x = atmosphereProfile.AirScatter.x + atmosphereProfile.AirAbsorption.x;
        extinction.y = atmosphereProfile.AirScatter.y + atmosphereProfile.AirAbsorption.y;
        extinction.z = atmosphereProfile.AirScatter.z + atmosphereProfile.AirAbsorption.z;
        extinction.w = atmosphereProfile.AerosolScatter + atmosphereProfile.AerosolAbsorption;

        var scaleHeights = new Vector2(1f / atmosphereProfile.AirAverageHeight, 1f / atmosphereProfile.AerosolAverageHeight);

        var lightPixels = new Color[lightResolution.x * lightResolution.y];
        for (var y = 0; y < lightResolution.y; y++)
        {
            for(var x = 0; x < lightResolution.x; x++)
            {
                var uv = new Vector2(x / (lightResolution.x - 1.0f), y / (lightResolution.y - 1.0f));

                var startHeight = atmosphereProfile.AtmosphereHeight * uv.x;
                var angle = 2.0f * uv.y - 1.0f;

                var origin = new Vector3(0, startHeight, 0);
                var direction = new Vector3(Mathf.Sqrt(1.0f - angle * angle), angle, 0);
                var center = new Vector3(0, -atmosphereProfile.PlanetRadius, 0);
                var radius = atmosphereProfile.PlanetRadius + atmosphereProfile.AtmosphereHeight;

                // Intersect outer atmosphere to find distance
                var a = Vector3.Dot(direction, direction);
                var oc = origin - center;
                var b = 2.0f * Vector3.Dot(oc, direction);
                var c = Vector3.Dot(oc, oc) - radius * radius;
                var d = b * b - 4.0f * a * c;

                var distance = (-b - Mathf.Sqrt(d)) / (2.0f * a);

                var end = origin + direction * distance;
                var step = (end - origin) / transmittanceSamples;

                var opticalDepth = new Vector2(0.0f, 0.0f);

                for(var i = 0; i < transmittanceSamples; i++)
                {
                    var position = origin + step * i;
                    var height = Vector3.Distance(center, position) - atmosphereProfile.PlanetRadius;
                    var airDensity = Mathf.Exp(-height * scaleHeights.x);
                    var aerosolDensity = Mathf.Exp(-height * scaleHeights.y);

                    // Smooth out the first and last sample
                    if(i < 1 || i > transmittanceSamples - 2)
                    {
                        airDensity *= 0.5f;
                        aerosolDensity *= 0.5f;
                    }

                    opticalDepth += new Vector2(airDensity, aerosolDensity);
                }

                // Apply extinction coefficients and multiply by step length
                var scaledDepth = Vector4.Scale(extinction, new Vector4(opticalDepth.x, opticalDepth.x, opticalDepth.x, opticalDepth.y)) * step.magnitude;

                Color result;
                result.r = Mathf.Exp(-scaledDepth.x - scaledDepth.w);
                result.g = Mathf.Exp(-scaledDepth.y - scaledDepth.w);
                result.b = Mathf.Exp(-scaledDepth.z - scaledDepth.w);
                result.a = 1;

                lightPixels[x + y * lightResolution.x] = result;
            }
        }

        directionalLightLookup.SetPixels(lightPixels);
        directionalLightLookup.Apply(false, false);
    }

    [ContextMenu("Recalculate")]
    public void Recalculate()
    {
        AtmosphereTransmittance.Clear(false, true, Color.clear);
        this.atmosphereScatter.Clear(false, true, Color.clear);
        AtmosphereGather.Clear(false, true, Color.clear);
        atmosphereAmbient.Clear(false, true, Color.clear);

        var factor = Math.Log(Math.E, 2);

        Vector4 extinction;
        extinction.x = (float)((atmosphereProfile.AirScatter.x + atmosphereProfile.AirAbsorption.x) * factor);
        extinction.y = (float)((atmosphereProfile.AirScatter.y + atmosphereProfile.AirAbsorption.y) * factor);
        extinction.z = (float)((atmosphereProfile.AirScatter.z + atmosphereProfile.AirAbsorption.z) * factor);
        extinction.w = (float)((atmosphereProfile.AerosolScatter + atmosphereProfile.AerosolAbsorption) * factor);

        Vector2 scaleHeights;
        scaleHeights.x = (float)(1.0 / atmosphereProfile.AirAverageHeight * factor);
        scaleHeights.y = (float)(1.0 / atmosphereProfile.AerosolAverageHeight * factor);

        Vector4 atmosphereScatter;
        atmosphereScatter.x = atmosphereProfile.AirScatter.x;
        atmosphereScatter.y = atmosphereProfile.AirScatter.y;
        atmosphereScatter.z = atmosphereProfile.AirScatter.z;
        atmosphereScatter.w = atmosphereProfile.AerosolScatter;

        Vector3 rayleighToMie;
        rayleighToMie.x = (atmosphereProfile.AerosolScatter / atmosphereProfile.AirScatter.x) * (atmosphereProfile.AirScatter.x / atmosphereProfile.AerosolScatter);
        rayleighToMie.y = (atmosphereProfile.AerosolScatter / atmosphereProfile.AirScatter.y) * (atmosphereProfile.AirScatter.x / atmosphereProfile.AerosolScatter);
        rayleighToMie.z = (atmosphereProfile.AerosolScatter / atmosphereProfile.AirScatter.z) * (atmosphereProfile.AirScatter.x / atmosphereProfile.AerosolScatter);

        // Set any globals
        Shader.SetGlobalTexture("_AtmosphereTransmittance", AtmosphereTransmittance);
        Shader.SetGlobalTexture("_SkyScatter", this.atmosphereScatter);
        Shader.SetGlobalTexture("_SkyAmbient", atmosphereAmbient);

        Shader.SetGlobalVector("_AtmosphereExtinction", extinction);
        Shader.SetGlobalVector("_AtmosphereScaleHeights", scaleHeights);
        Shader.SetGlobalVector("_AtmosphereScatter", atmosphereScatter);
        Shader.SetGlobalVector("_RayleighToMie", rayleighToMie);

        Shader.SetGlobalFloat("_AtmosphereHeight", atmosphereProfile.AtmosphereHeight);
        Shader.SetGlobalFloat("_PlanetRadius", atmosphereProfile.PlanetRadius);
        Shader.SetGlobalFloat("_MiePhase", atmosphereProfile.AerosolAnisotropy);

        // Calculate transmittance
        computeTransmittance.SetTexture(0, "_Result", AtmosphereTransmittance);
        computeTransmittance.SetFloat("_Samples", transmittanceSamples);
        computeTransmittance.DispatchNormalized(0, transmittanceResolution.x, transmittanceResolution.y, 1);

        // Calculate scatter
        computeInScatter.SetTexture(0, "_Result", this.atmosphereScatter);
        computeInScatter.SetFloat("_Samples", scatterSamples);
        computeInScatter.DispatchNormalized(0, scatterResolution.x, scatterResolution.y, scatterResolution.z);

        if (scatteringOrders > 1)
        {
            // Compute first multi-scatter order
            var gatherTemp = RenderTexture.GetTemporary(AtmosphereGather.descriptor).Created();
            var scatterTemp = RenderTexture.GetTemporary(this.atmosphereScatter.descriptor).Created();

            // Gather the scattering from surrounding pixels into the current
            computeGather.SetTexture(0, "_CurrentGather", gatherTemp);
            computeGather.SetTexture(0, "_PreviousScatter", this.atmosphereScatter);
            computeGather.SetTexture(0, "_TotalGather", AtmosphereGather);
            computeGather.SetFloat("_Samples", multiScatterSamples);

            // Set multi scatter properties
            computeMultiScatter.SetTexture(0, "_CurrentGather", gatherTemp);
            computeMultiScatter.SetTexture(0, "_CurrentScatter", scatterTemp);
            computeMultiScatter.SetTexture(0, "_Result", this.atmosphereScatter);
            computeMultiScatter.SetFloat("_Samples", scatterSamples);

            // Multi scattering
            for (var i = 1; i < scatteringOrders; i++)
            {
                computeGather.DispatchNormalized(0, gatherResolution.x, gatherResolution.y, 1);
                computeMultiScatter.DispatchNormalized(0, scatterResolution.x, scatterResolution.y, scatterResolution.z);

                // This only needs to be done after the first scatter order, as the first order will use the in-scatter texture directly
                computeGather.SetTexture(0, "_PreviousScatter", scatterTemp);
            }

            RenderTexture.ReleaseTemporary(scatterTemp);
            RenderTexture.ReleaseTemporary(gatherTemp);
        }

        // Ambient lookup
        computeAmbient.SetTexture(0, "_SkyScatter", this.atmosphereScatter);
        computeAmbient.SetTexture(0, "_Result", atmosphereAmbient);
        computeAmbient.SetFloat("_Samples", ambientSteps);
        computeAmbient.DispatchNormalized(0, ambientResolution.x, ambientResolution.y, 1);

        // Directional light lookup
        CalculateDirectionalLightLookup();
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        // Editor-only.. stops some scene modifications happening
#if UNITY_EDITOR
        if (!Application.isPlaying && newScene != gameObject.scene)
            return;
#endif

        RenderSettings.skybox = skybox;
        OnProfileUpdated?.Invoke(atmosphereProfile);
    }

    private void OnEnable()
    {
        if (!atmosphereProfile)
        {
            enabled = false;
            return;
        }

        atmosphereProfile.OnProfileChanged += Recalculate;

        computeTransmittance = Resources.Load<ComputeShader>("PrecomputeTransmittance");
        computeInScatter = Resources.Load<ComputeShader>("PrecomputeInScatter");
        computeGather = Resources.Load<ComputeShader>("PrecomputeGather");
        computeMultiScatter = Resources.Load<ComputeShader>("PrecomputeMultiScatter");
        computeAmbient = Resources.Load<ComputeShader>("AmbientSkyLookup");

        skybox = new Material(Shader.Find("Sky/Physical Skybox"));

#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene() == gameObject.scene)
#endif
            RenderSettings.skybox = skybox;

        AtmosphereTransmittance = new RenderTexture(transmittanceResolution.x, transmittanceResolution.y, 0, RenderTextureFormat.RGB111110Float)
        {
            enableRandomWrite = true,
            name = "Atmosphere Transmittance"
        }.Created();

        atmosphereScatter = new RenderTexture(scatterResolution.x, scatterResolution.y, 0, RenderTextureFormat.ARGBHalf)
        {
            name = "Atmosphere Scatter",
            dimension = TextureDimension.Tex3D,
            enableRandomWrite = true,
            volumeDepth = scatterResolution.z
        }.Created();

        AtmosphereGather = new RenderTexture(gatherResolution.x, gatherResolution.y, 0, RenderTextureFormat.ARGBHalf)
        {
            name = "Atmosphere Gather",
            enableRandomWrite = true
        }.Created();

        atmosphereAmbient = new RenderTexture(ambientResolution.x, ambientResolution.y, 0, RenderTextureFormat.RGB111110Float)
        {
            name = "Atmosphere Ambient",
            enableRandomWrite = true
        }.Created();

        Recalculate();

        // Set skybox params.. so that anythign listening to the update event can update correctly
        atmosphereProfile.SetSkyboxVariables(skybox);
        skybox.SetTexture("_SkyScatter", atmosphereScatter);

        AtmosphereInitialized = true;

        Shader.EnableKeyword("ATMOSPHERIC_SCATTERING_ON");
        OnProfileUpdated?.Invoke(atmosphereProfile);

        // activeSceneChanged is not called in editor mode... because of course it isn't
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        else
#endif
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        if (atmosphereProfile)
        {
            atmosphereProfile.OnProfileChanged -= Recalculate;
        }

        AtmosphereInitialized = false;
        Shader.DisableKeyword("ATMOSPHERIC_SCATTERING_ON");

        // activeSceneChanged is not called in editor mode... because of course it isn't
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        else
#endif
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void Update()
    {
        if(light != null)
        {
            light.color = GetDirectionalLightColor(-light.transform.forward.y, 64);
        }

       // Recalculate();
        atmosphereProfile.SetSkyboxVariables(skybox);
        skybox.SetTexture("_SkyScatter", atmosphereScatter);
    }
}