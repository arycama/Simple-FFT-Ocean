#pragma warning disable 0108

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
    [SerializeField, Tooltip("Number of Voxels per Axis")]
    private int3 voxelResolution = new int3(2, 2, 2);

    [SerializeField, Tooltip("Upwards force from water. (Default is waterDensity * gravity)")]
    private float buoyancy = 9810f;

    [SerializeField, Tooltip("Ratio of underwater to above water drag")]
    private float underwaterDrag = 3;

    private float originalDrag, originalAngularDrag, totalVolume;

    private float[,,] voxelHeights, voxelVolumes;
    private float3[,,] voxelCenters;
    private Rigidbody rigidbody;

    private void OnEnable()
    {
        var collider = GetComponent<Collider>();
        rigidbody = GetComponent<Rigidbody>();

        originalDrag = rigidbody.drag;
        originalAngularDrag = rigidbody.angularDrag;

        // Prepare mesh
        var boundsMin = (float3)collider.LocalBounds().min;
        var size = (float3)collider.LocalBounds().size;

        var voxelCorners = new float3[voxelResolution.x + 1, voxelResolution.y + 1, voxelResolution.z + 1];
        voxelCenters = new float3[voxelResolution.x, voxelResolution.y, voxelResolution.z];
        voxelVolumes = new float[voxelResolution.x, voxelResolution.y, voxelResolution.z];
        voxelHeights = new float[voxelResolution.x, voxelResolution.y, voxelResolution.z];

        for (var z = 0; z <= voxelResolution.z; z++)
        {
            for (var y = 0; y <= voxelResolution.y; y++)
            {
                for (var x = 0; x <= voxelResolution.x; x++)
                {
                    var worldP = transform.TransformPoint(boundsMin + float3(x, y, z) / voxelResolution * size);

                    var closest = collider.ClosestPoint(worldP);
                    voxelCorners[x, y, z] = transform.InverseTransformPoint(closest);
                }
            }
        }

        // calculate voxel centers and volumes from voxel corners
        totalVolume = 0f;
        for (var z = 0; z < voxelResolution.z; z++)
        {
            for (var y = 0; y < voxelResolution.y; y++)
            {
                for (var x = 0; x < voxelResolution.x; x++)
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
        float3 force = 0, torque = 0;
        var totalSubmergedVolume = 0f;
        var centerOfMass = rigidbody.worldCenterOfMass;

        for (var z = 0; z < voxelResolution.z; z++)
        {
            for (var y = 0; y < voxelResolution.y; y++)
            {
                for (var x = 0; x < voxelResolution.x; x++)
                {
                    var voxelCenter = transform.TransformPoint(voxelCenters[x, y, z]);
                    var waterLevel = Ocean.Instance.GetOceanHeight(voxelCenter);

                    // Value between 0 and 1, representing how much the voxel is submerged
                    var submergedAmount = saturate((waterLevel - voxelCenter.y) / voxelHeights[x, y, z] + 0.5f);

                    if (submergedAmount > 0)
                    {
                        var submergedVolume = voxelVolumes[x, y, z] * submergedAmount;
                        var buoyancy = float3(0, this.buoyancy * submergedVolume, 0);
                        totalSubmergedVolume += submergedVolume;

                        force += buoyancy;
                        torque += cross(voxelCenter - centerOfMass, buoyancy);
                    }
                }
            }
        }

        // Set drag/angular drag depending on ratio of submerged voxels
        var ratio = saturate(totalSubmergedVolume / totalVolume);
        rigidbody.drag = lerp(originalDrag, originalDrag * underwaterDrag, ratio);
        rigidbody.angularDrag = lerp(originalAngularDrag, originalAngularDrag * underwaterDrag, ratio);

        rigidbody.AddForce(force);
        rigidbody.AddTorque(torque);
    }
}