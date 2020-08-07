using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AtmosphereManager))]
public class AtmosphereManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();
        var changed = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            var atmosphereManager = target as AtmosphereManager;
            atmosphereManager.Recalculate();
        }
    }
}