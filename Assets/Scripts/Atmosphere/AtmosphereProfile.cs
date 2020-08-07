using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Atmosphere Profile")]
public class AtmosphereProfile : ScriptableObject
{
    public event Action OnProfileChanged;

    [Header("Air Properties")]
    [SerializeField]
    private Vector3 airScatter = new Vector3(5.8e-6f, 1.35e-5f, 3.31e-5f);

    [SerializeField]
    private Vector3 airAbsorption = new Vector3(2.0556e-06f, 4.9788e-06f, 2.136e-07f);

    [SerializeField, Min(0)]
    private float airAverageHeight = 7994;

    [Header("Aerosol Properties")]
    [SerializeField, Range(0, 0.01f)]
    private float aerosolScatter = 3.996e-6f;

    [SerializeField, Range(0, 0.1f)]
    private float aerosolAbsorption = 4.4e-6f;

    [SerializeField, Range(-1, 1)]
    private float aerosolAnisotropy = 0.73f;

    [SerializeField, Min(0)]
    private float aerosolAverageHeight = 1.2e+3f;

    [Header("Planet Properties")]
    [SerializeField, Min(0)]
    private float planetRadius = 6.36e+6f;

    [SerializeField, Min(0)]
    private float atmosphereHeight = 6e+4f;

    [SerializeField]
    private Color groundColor = new Color(0.4809999f, 0.4554149f, 0.4451807f);

    [Header("Night Sky")]
    [SerializeField]
    private Cubemap starTexture = null;

    [SerializeField]
    private float starSpeed = 0.01f;

    [SerializeField]
    private Color starColor = Color.white;

    [SerializeField]
    private Vector3 starAxis = Vector3.right;

    public Vector3 AirScatter => airScatter;
    public Vector3 AirAbsorption => airAbsorption;
    public float AirAverageHeight => airAverageHeight;
    public float AerosolScatter => aerosolScatter;
    public float AerosolAbsorption => aerosolAbsorption;
    public float AerosolAverageHeight => aerosolAverageHeight;
    public float AerosolAnisotropy => aerosolAnisotropy;
    public float AtmosphereHeight => atmosphereHeight;
    public float PlanetRadius => planetRadius;

    public Cubemap StarTexture => starTexture;
    public Color StarColor => starColor;

    public void ProfileChanged()
    {
        OnProfileChanged?.Invoke();
    }

    public void SetSkyboxVariables(Material material)
    {
        material.SetColor("_GroundColor", groundColor);

        if (starTexture) material.SetTexture("_StarMap", starTexture);
        material.SetColor("_StarColor", starColor);
        material.SetFloat("_StarSpeed", starSpeed);
        material.SetVector("_StarAxis", starAxis);
    }

    [ContextMenu("Reset Values")]
    private void Reset()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Reset Atmosphere Profile");
#endif

        airScatter = new Vector3(5.8e-6f, 1.35e-5f, 3.31e-5f);
        airAbsorption = new Vector3(2.0556e-06f, 4.9788e-06f, 2.136e-07f);
        airAverageHeight = 7994;
        aerosolScatter = 3e-6f;
        aerosolAbsorption = 1.11f;
        aerosolAnisotropy = 0.87f;
        aerosolAverageHeight = 1.2e+3f;
        planetRadius = 6.36e+6f;
        atmosphereHeight = 6e+4f;
        groundColor = new Color(0.4809999f, 0.4554149f, 0.4451807f);

        ProfileChanged();
    }
}