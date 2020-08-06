using System.IO;
using UnityEngine;
using UnityEditor;
using Boo.Lang;
using System;
using UnityEngine.Rendering;

public static class ShaderToolsConfig
{
	private const string fileName = "shadertoolsconfig";
	private const string extension = "json";
    #if UNITY_EDITOR_WIN
	private const string unityFile = "Unity.exe";
    #elif UNITY_EDITOR_OSX
	private const string unityFile = "Unity.app";
    #endif
	private const string cgIncPath = "Data/CGIncludes";

	[InitializeOnLoadMethod]
	private static void CheckForConfig()
	{
		var projectPath = Path.GetDirectoryName(Application.dataPath);
		var path = $"{projectPath}/{fileName}.{extension}";

		var appPath = EditorApplication.applicationPath;
		var exeIndex = appPath.IndexOf(unityFile);

		var cgIncludePath = appPath.Substring(0, exeIndex) + cgIncPath;
		var additionalIncludeDirectories = new string[] { ".", cgIncludePath };

		// Defines
		var defines = new List<string>();
		var builtinDefines = Enum.GetValues(typeof(BuiltinShaderDefine));
		foreach(BuiltinShaderDefine define in builtinDefines)
		{
			if(GraphicsSettings.HasShaderDefine(define))
			{
				defines.Add(define.ToString());
			}
		}

		var hlslConfig = new HlslConfig(null, additionalIncludeDirectories);
		var json = EditorJsonUtility.ToJson(hlslConfig, true);

		// Replace underscores with dots
		var replacedJson = json.Replace("__", ".");
		File.WriteAllText(path, replacedJson);
	}
}