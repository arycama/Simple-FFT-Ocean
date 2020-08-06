#pragma warning disable 0108

using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(Camera))]
public class ReflectionCamera : MonoBehaviour
{
	[SerializeField]
	private float clipPlaneOffset = 0.07f;

	[SerializeField, Range(0, 3)]
	private int downsample = 0;

	[SerializeField]
	private string textureProperty = "_ReflectionTexture";

	[SerializeField]
	private Camera targetCamera = null;

	private Camera camera;
	private RenderTexture targetTexture;

    private void InitializeTexture()
	{
		camera = GetComponent<Camera>();

		var targetWidth = Screen.currentResolution.width / (2 << downsample);
		var targetHeight = Screen.currentResolution.height / (2 << downsample);

		targetTexture = new RenderTexture(targetWidth, targetHeight, 16, RenderTextureFormat.ARGB32);
		targetTexture.name = "Planar Reflection";
		camera.targetTexture = targetTexture;

		Shader.SetGlobalTexture(textureProperty, targetTexture);
	}

	private void OnEnable()
	{
		Shader.EnableKeyword("_PLANAR_REFLECTIONS_ON");
		camera = GetComponent<Camera>();
		InitializeTexture();
		Camera.onPreRender += RenderReflections;
	}

	private void OnDisable()
	{
		Shader.DisableKeyword("_PLANAR_REFLECTIONS_ON");
		Camera.onPreRender -= RenderReflections;
	}

	private void RenderReflections(Camera targetCamera)
	{
		if (targetCamera != this.targetCamera)
		{
#if UNITY_EDITOR
			var sceneView = UnityEditor.SceneView.currentDrawingSceneView;
			if (!sceneView || targetCamera != sceneView.camera)
			{
				return;
			}
#else
			return;
#endif
		}

		var targetEuler = targetCamera.transform.eulerAngles;
		transform.eulerAngles = new Vector3(-targetEuler.x, targetEuler.y, targetEuler.z);
		transform.position = targetCamera.transform.position;

		var reflectionMatrix = CalculateReflectionMatrix(new Vector4(0, 1, 0, -clipPlaneOffset));
		var worldReflectionMatrix = targetCamera.worldToCameraMatrix * reflectionMatrix;
		camera.worldToCameraMatrix = worldReflectionMatrix;

		var cpos = camera.worldToCameraMatrix.MultiplyPoint(new Vector3(0, 1, 0) * clipPlaneOffset);
		var cnormal = camera.worldToCameraMatrix.MultiplyVector(new Vector3(0, 1, 0)).normalized;

		var clipPlane = new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));

        camera.projectionMatrix = targetCamera.CalculateObliqueMatrix(clipPlane);
		transform.position = reflectionMatrix.MultiplyPoint(transform.position);
		transform.eulerAngles = new Vector3(-targetEuler.x, targetEuler.y, targetEuler.z);

		var fogEnabled = RenderSettings.fog;
		var originalShadows = QualitySettings.shadows;

		GL.invertCulling = true;
		RenderSettings.fog = false;
		QualitySettings.shadows = ShadowQuality.Disable;

		camera.Render();

		GL.invertCulling = false;
		RenderSettings.fog = fogEnabled;
		QualitySettings.shadows = originalShadows;
	}

	private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
	{
		Matrix4x4 reflectionMat;

		reflectionMat.m00 = 1 - 2 * plane.x * plane.x;
		reflectionMat.m01 = -2 * plane.x * plane.y;
		reflectionMat.m02 = -2 * plane.x * plane.z;
		reflectionMat.m03 = -2 * plane.w * plane.x;

		reflectionMat.m10 = -2 * plane.y * plane.x;
		reflectionMat.m11 = 1 - 2 * plane.y * plane.y;
		reflectionMat.m12 = -2 * plane.y * plane.z;
		reflectionMat.m13 = -2 * plane.w * plane.y;

		reflectionMat.m20 = -2 * plane.z * plane.x;
		reflectionMat.m21 = -2 * plane.z * plane.y;
		reflectionMat.m22 = 1 - 2 * plane.z * plane.z;
		reflectionMat.m23 = -2 * plane.w * plane.z;

		reflectionMat.m30 = 0;
		reflectionMat.m31 = 0;
		reflectionMat.m32 = 0;
		reflectionMat.m33 = 1;

		return reflectionMat;
	}
}
