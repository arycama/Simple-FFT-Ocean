// Created by Ben Sims 27/07/20

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FoxieGames
{
	[CustomEditor(typeof(QuadtreeRenderer))]
	public class OceanMeshEditor : Editor
	{
		private MaterialEditor materialEditor;

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();
			EditorGUI.BeginChangeCheck();

			var materialProperty = serializedObject.FindProperty("material");
			if (materialProperty.objectReferenceValue != null && materialEditor == null)
			{
				// Create a new instance of the default MaterialEditor
				materialEditor = (MaterialEditor)CreateEditor(materialProperty.objectReferenceValue);
			}

			if (materialEditor != null)
			{
				// Draw the material's foldout and the material shader field
				// Required to call _materialEditor.OnInspectorGUI ();
				materialEditor.DrawHeader();

				//  We need to prevent the user to edit Unity default materials
				var isDefaultMaterial = !AssetDatabase.GetAssetPath(materialProperty.objectReferenceValue).StartsWith("Assets");
				using (new EditorGUI.DisabledGroupScope(isDefaultMaterial))
				{
					// Draw the material properties
					// Works only if the foldout of _materialEditor.DrawHeader () is open
					materialEditor.OnInspectorGUI();
				}
			}
		}
	}
}