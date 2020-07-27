using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(MeshFilter))]
public class PlaneGenerator : MonoBehaviour
{
    [SerializeField]
    private int divisions = 32;

    [SerializeField]
    private float size = 32;

    private void OnEnable()
    {
        var vertexCount = (divisions + 1) * (divisions + 1);
        var vertices = new Vector3[vertexCount];
        var interval = size / divisions;
        for (var z = 0; z <= divisions; z++)
        {
            for (var x = 0; x <= divisions; x++)
            {
                var index = x + z * (divisions + 1);
                vertices[index] = new Vector3(x * interval, 0, z * interval);
            }
        }
        var triangles = new int[divisions * divisions * 6];
        for (var z = 0; z < divisions; z++)
        {
            for (var x = 0; x < divisions; x++)
            {
                var cellIndex = x + z * divisions;
                var startIndex = cellIndex * 6;
                var vertexIndex = cellIndex + z;
                triangles[startIndex + 0] = vertexIndex;
                triangles[startIndex + 1] = vertexIndex + divisions + 1;
                triangles[startIndex + 2] = vertexIndex + 1;
                triangles[startIndex + 3] = vertexIndex + 1;
                triangles[startIndex + 4] = vertexIndex + divisions + 1;
                triangles[startIndex + 5] = vertexIndex + divisions + 2;
            }
        }
        var bounds = new Bounds(Vector3.zero, Vector3.one * size * 2);
        var mesh = new Mesh()
        {
            name = "Plane",
            vertices = vertices,
            triangles = triangles,
            bounds = bounds
        };

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}