#pragma warning disable 0108

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
    [SerializeField, Tooltip("Number of Voxels per Axis")]
    private int3 voxelCount = new int3(2, 2, 2);

    [SerializeField]
    private float density = 1000f;

    [SerializeField]
    private float gravity = 9.81f;

    [SerializeField]
    private float underwaterDrag = 3;


    public float3 buoyantForce;
    private List<float3> forces; // For drawing force gizmos
    private Collider collider;
    private Rigidbody rigidbody;
    private float3[,,] voxelCorners;
    private float3[,,] voxelCenters;
    private float[,,] voxelVolumes;
    private float[,,] voxelHeights;

    private float originalDrag, originalAngularDrag, totalVolume;

    private void OnEnable()
    {
        collider = GetComponent<Collider>();
        rigidbody = GetComponent<Rigidbody>();

        originalDrag = rigidbody.drag;
        originalAngularDrag = rigidbody.angularDrag;

        // Prepare mesh
        forces = new List<float3>(); // For drawing force gizmos
        var boundsMin = (float3)collider.LocalBounds().min;
        var size = (float3)collider.LocalBounds().size;

        voxelCorners = new float3[voxelCount.x + 1, voxelCount.y + 1, voxelCount.z + 1];
        voxelCenters = new float3[voxelCount.x, voxelCount.y, voxelCount.z];
        voxelVolumes = new float[voxelCount.x, voxelCount.y, voxelCount.z];
        voxelHeights = new float[voxelCount.x, voxelCount.y, voxelCount.z];

        for (var z = 0; z <= voxelCount.z; z++)
        {
            for (var y = 0; y <= voxelCount.y; y++)
            {
                for (var x = 0; x <= voxelCount.x; x++)
                {
                    var worldP = transform.TransformPoint(boundsMin + float3(x, y, z) / voxelCount * size);

                    var closest = collider.ClosestPoint(worldP);
                    voxelCorners[x, y, z] = transform.InverseTransformPoint(closest);
                }
            }
        }

        // calculate voxel centers and volumes from voxel corners
        totalVolume = 0f;
        for (var z = 0; z < voxelCount.z; z++)
        {
            for (var y = 0; y < voxelCount.y; y++)
            {
                for (var x = 0; x < voxelCount.x; x++)
                {
                    var corner000 = voxelCorners[x + 0, y + 0, z + 0];
                    var corner100 = voxelCorners[x + 1, y + 0, z + 0];
                    var corner010 = voxelCorners[x + 0, y + 1, z + 0];
                    var corner110 = voxelCorners[x + 1, y + 1, z + 0];
                    var corner001 = voxelCorners[x + 0, y + 0, z + 1];
                    var corner101 = voxelCorners[x + 1, y + 0, z + 1];
                    var corner011 = voxelCorners[x + 0, y + 1, z + 1];
                    var corner111 = voxelCorners[x + 1, y + 1, z + 1];

                    var bottomQuad = (corner000 + corner100 + corner001 + corner101) / 4;
                    var topQuad = (corner010 + corner110 + corner011 + corner111) / 4;

                    voxelCenters[x, y, z] = (bottomQuad + topQuad) / 2;

                    // Volume of a tetrahedron
                    var volume = TetrahedronVolume(corner000, corner100, corner010, corner001);
                    volume += TetrahedronVolume(corner110, corner100, corner010, corner111);
                    volume += TetrahedronVolume(corner011, corner001, corner010, corner111);
                    volume += TetrahedronVolume(corner101, corner001, corner100, corner111);
                    volume += TetrahedronVolume(corner100, corner010, corner001, corner111);

                    voxelVolumes[x, y, z] = volume;

                    // Also track total volume for submerged percentage calculations
                    totalVolume += volume;

                    // Save height of voxel for submerged calculations
                    voxelHeights[x, y, z] = abs(bottomQuad.y - topQuad.y);
                }
            }
        }
    }

    private static float TetrahedronVolume(float3 a, float3 b, float3 c, float3 d)
    {
        return abs(dot(a - d, cross(b - d, c - d))) / 6;
    }

    private void OnDisable()
    {
        rigidbody.drag = originalDrag;
        rigidbody.angularDrag = originalAngularDrag;
    }

    private void FixedUpdate()
    {
        forces.Clear(); // For drawing force gizmos

        float3 force = 0, torque = 0;
        var submergedVolume = 0f;
        var centerOfMass = rigidbody.worldCenterOfMass;

        for (var z = 0; z < voxelCount.z; z++)
        {
            for (var y = 0; y < voxelCount.y; y++)
            {
                for (var x = 0; x < voxelCount.x; x++)
                {
                    var point = voxelCenters[x, y, z];
                    var height = voxelHeights[x, y, z];
                    var volume = voxelVolumes[x, y, z];

                    var voxelCenter = transform.TransformPoint(point);
                    var waterLevel = Ocean.Instance.GetOceanHeight(voxelCenter);

                    // Value between 0 and 1, representing how much the voxel is submerged
                    var submergedAmount = Mathf.Clamp01((waterLevel - voxelCenter.y) / height + 0.5f);

                    if (submergedAmount > 0)
                    {
                        var buoyancy = float3(0, density * gravity * volume * submergedAmount, 0);
                        //rigidbody.AddForceAtPosition(buoyancy, voxelCenter);
                        forces.Add(voxelCenter); // For drawing force gizmos
                        submergedVolume += volume * submergedAmount;

                        force += buoyancy;
                        torque += cross(voxelCenter - centerOfMass, buoyancy);
                    }
                }
            }
        }

        buoyantForce = force;

        // Set drag/angular drag depending on ratio of submerged voxels
        var ratio = submergedVolume / totalVolume;
        rigidbody.drag = lerp(originalDrag, originalDrag * underwaterDrag, ratio);
        rigidbody.angularDrag = lerp(originalAngularDrag, originalAngularDrag * underwaterDrag, ratio);

        rigidbody.AddForce(force);
        rigidbody.AddTorque(torque);
    }

    private void OnDrawGizmos()
    {
        const float gizmoSize = 0.05f;
        Gizmos.color = Color.yellow;

        foreach (var p in voxelCenters)
        {
            Gizmos.DrawSphere(transform.TransformPoint(p.xyz), gizmoSize);
        }

        Gizmos.color = Color.cyan;

        foreach (var force in forces)
        {
            Gizmos.DrawSphere(force, gizmoSize);
        }

        Gizmos.color = new Color(1, 1, 1, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        for (var z = 0; z < voxelCount.z; z++)
        {
            for (var y = 0; y < voxelCount.y; y++)
            {
                for (var x = 0; x < voxelCount.x; x++)
                {
                    var corner000 = voxelCorners[x + 0, y + 0, z + 0];
                    var corner100 = voxelCorners[x + 1, y + 0, z + 0];
                    var corner010 = voxelCorners[x + 0, y + 1, z + 0];
                    var corner110 = voxelCorners[x + 1, y + 1, z + 0];
                    var corner001 = voxelCorners[x + 0, y + 0, z + 1];
                    var corner101 = voxelCorners[x + 1, y + 0, z + 1];
                    var corner011 = voxelCorners[x + 0, y + 1, z + 1];
                    var corner111 = voxelCorners[x + 1, y + 1, z + 1];

                    Gizmos.DrawLine(corner000, corner100);
                    Gizmos.DrawLine(corner100, corner110);
                    Gizmos.DrawLine(corner110, corner010);
                    Gizmos.DrawLine(corner010, corner000);

                    Gizmos.DrawLine(corner001, corner101);
                    Gizmos.DrawLine(corner101, corner111);
                    Gizmos.DrawLine(corner111, corner011);
                    Gizmos.DrawLine(corner011, corner001);

                    Gizmos.DrawLine(corner000, corner001);
                    Gizmos.DrawLine(corner100, corner101);
                    Gizmos.DrawLine(corner010, corner011);
                    Gizmos.DrawLine(corner110, corner111);
                }
            }
        }
    }
}