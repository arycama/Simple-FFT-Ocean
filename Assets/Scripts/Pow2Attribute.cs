using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class Pow2Attribute : PropertyAttribute
{
    public int MaxValue { get; }

    public Pow2Attribute(int maxValue)
    {
        MaxValue = maxValue;
    }
}
