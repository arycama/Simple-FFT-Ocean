using UnityEngine;
using System;

[Serializable]
public class HlslConfig
{
	[SerializeField]
	private string[] hlsl__preprocessorDefinitions;

	[SerializeField]
	private string[] hlsl__additionalIncludeDirectories;

	public HlslConfig(string[] hlsl_preprocessorDefinitions, string[] hlsl_additionalIncludeDirectories)
	{
		this.hlsl__preprocessorDefinitions = hlsl_preprocessorDefinitions;
		this.hlsl__additionalIncludeDirectories = hlsl_additionalIncludeDirectories;
	}
}
