#pragma warning disable 0108

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
	[SerializeField]
	private Mesh mesh = null;

	[SerializeField]
	private float voxelSpacing = 1f;

	[SerializeField]
	private float buoyancy = 1500f;

	[SerializeField]
	private float underwaterDrag = 3;

	private List<float3> voxels;
	private List<float3> forces; // For drawing force gizmos

	private Mesh convexMesh;
	private MeshCollider meshCollider;
	private Rigidbody rigidbody;

	private float originalDrag, originalAngularDrag, totalVoxels;

	private void OnEnable()
	{
		meshCollider = GetComponent<MeshCollider>();
		rigidbody = GetComponent<Rigidbody>();

		originalDrag = rigidbody.drag;
		originalAngularDrag = rigidbody.angularDrag;

		// Prepare mesh
		convexMesh = Instantiate(mesh);
		Physics.BakeMesh(convexMesh.GetInstanceID(), true);
		meshCollider.convex = true;
		meshCollider.sharedMesh = convexMesh;

		forces = new List<float3>(); // For drawing force gizmos
		voxels = new List<float3>();

		var bounds = convexMesh.bounds;
		var boundsMin = (float3)bounds.min;
		var size = (float3)bounds.size;
		var slices = max(1, ceil(size / voxelSpacing));
		totalVoxels = slices.x + slices.y + slices.z;

		for (var z = 0; z <= slices.z; z++)
		{
			for (var y = 0; y <= slices.y; y++)
			{
				for (var x = 0; x <= slices.x; x++)
				{
					var p = boundsMin + size * (float3(x, y, z) / slices);

					var worldP = transform.TransformPoint(p);
					var closest = meshCollider.ClosestPoint(worldP);
					var localP = transform.InverseTransformPoint(closest);

					voxels.Add(localP);
				}
			}
		}
	}

	private void OnDisable()
	{
		rigidbody.drag = originalDrag;
		rigidbody.angularDrag = originalAngularDrag;
	}

	private void FixedUpdate()
	{
		forces.Clear(); // For drawing force gizmos

		var voxelVolume = pow(voxelSpacing, 3);

		//float3 force = 0, torque = 0;
		var submergedVoxels = 0;
		foreach (var point in voxels)
		{
			var worldPosition = (float3)transform.TransformPoint(point);
			var waterLevel = Ocean.Instance.GetOceanHeight(worldPosition);
			var depth = waterLevel - worldPosition.y;

			if (depth > 0)
			{
				var buoyancy = float3(0, this.buoyancy * depth * voxelVolume, 0);

				rigidbody.AddForceAtPosition(buoyancy, worldPosition);

				forces.Add(worldPosition); // For drawing force gizmos
				submergedVoxels++;
			}
		}

		// Set drag/angular drag depending on ratio of submerged voxels
		var ratio = submergedVoxels / totalVoxels;
		rigidbody.drag = lerp(originalDrag, originalDrag * underwaterDrag, ratio);
		rigidbody.angularDrag = lerp(originalAngularDrag, originalAngularDrag * underwaterDrag, ratio);
	}

	/// <summary>
	/// Draws gizmos.
	/// </summary>
	private void OnDrawGizmos()
	{

		if (voxels == null || forces == null)
		{
			return;
		}

		const float gizmoSize = 0.05f;
		Gizmos.color = Color.yellow;

		foreach (var p in voxels)
		{
			Gizmos.DrawSphere(transform.TransformPoint(p), gizmoSize);
		}

		Gizmos.color = Color.cyan;

		foreach (var force in forces)
		{
			Gizmos.DrawSphere(force, gizmoSize);
		}
	}
}