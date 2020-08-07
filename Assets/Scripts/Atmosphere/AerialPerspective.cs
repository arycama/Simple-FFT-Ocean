using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using MinAttribute = UnityEngine.Rendering.PostProcessing.MinAttribute;

[Serializable, PostProcess(typeof(AerialPerspectiveRenderer), PostProcessEvent.BeforeTransparent, "Custom/Aerial Perspective")]
public class AerialPerspective : PostProcessEffectSettings
{
    [SerializeField, Range(0, 2)]
    public IntParameter downsample = new IntParameter() { value = 1 };

    [SerializeField, Range(2, 32)]
    public IntParameter volumeWidth = new IntParameter() { value = 8 };

    [SerializeField, Range(2, 32)]
    public IntParameter volumeHeight = new IntParameter() { value = 8 };

    [SerializeField, Range(2, 128)]
    public IntParameter volumeDepth = new IntParameter() { value = 64 };

    [SerializeField]
    public BoolParameter volumetricShadows = new BoolParameter() { value = false };

    [SerializeField, Range(1, 128)]
    public IntParameter shadowSamples = new IntParameter() { value = 32 };
}
