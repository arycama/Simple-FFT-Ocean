// Created by Ben Sims 23/07/20

using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace FoxieGames
{
    [ExecuteAlways]
    public class QuadtreeRenderer : MonoBehaviour
    {
        [SerializeField]
        private float size = 32;

        [SerializeField]
        private Material material = null;

        [SerializeField]
        private int resolution = 128;

        [SerializeField, Min(1)]
        private int maxDivisions = 8;

        private List<Vector4>[] matrixMeshArrays;
        private NativeArray<Vector4>[] meshPositions;
        private BatchRendererGroup rendererGroup;
        private Mesh mesh;
        private JobHandle jobHandle;

        private void OnEnable()
        {
            if (material == null)
            {
                return;
            }

            matrixMeshArrays = new List<Vector4>[16];
            mesh = new Mesh();
            mesh.name = "Ocean Quad";

            var size = resolution + 1;
            var vertices = new Vector3[size * size];
            var xDelta = 1f / resolution;
            var yDelta = 1f / resolution;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    Vector3 vertex;
                    vertex.x = x * xDelta;
                    vertex.y = 0;
                    vertex.z = y * yDelta;
                    vertices[x + y * size] = vertex;
                }
            }

            mesh.vertices = vertices;
            mesh.subMeshCount = 16;

            for (var i = 0; i < 16; i++)
            {
                // Indices need some special handling
                var flags = (NeighborFlags)i;
                var indices = new List<int>();

                // Iterate four cells at a time, to simplify creation logic
                for (var y = 0; y < (size - 1) / 2; y++)
                {
                    for (var x = 0; x < (size - 1) / 2; x++)
                    {

                        // Flags for this current patch of 4 quads
                        var patchFlags = NeighborFlags.None;
                        if (flags.HasFlag(NeighborFlags.Left) && x == 0) patchFlags |= NeighborFlags.Left;
                        if (flags.HasFlag(NeighborFlags.Right) && x == (size - 1) / 2 - 1) patchFlags |= NeighborFlags.Right;
                        if (flags.HasFlag(NeighborFlags.Down) && y == 0) patchFlags |= NeighborFlags.Down;
                        if (flags.HasFlag(NeighborFlags.Up) && y == (size - 1) / 2 - 1) patchFlags |= NeighborFlags.Up;

                        var edges = edgeIndices[(int)patchFlags];
                        var index = (x + y * size) * 2;
                        foreach (var edge in edges)
                        {
                            indices.Add(index + edge[0] + edge[1] * size);
                            indices.Add(index + edge[2] + edge[3] * size);
                            indices.Add(index + edge[4] + edge[5] * size);
                        }
                    }
                }

                mesh.SetTriangles(indices, i, false);
                matrixMeshArrays[i] = new List<Vector4>();
            }

            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            var quadTree = new QuadTree<Vector3>(Vector3.zero);
            var qrtSize = this.size / 4;

            quadTree.lowerLeft = new QuadTree<Vector3>(new Vector3(-qrtSize, 0, -qrtSize));
            CheckQuadtree(quadTree.lowerLeft, NeighborFlags.DownLeft, 1, this.size / 2, new Vector3(-qrtSize, 0, -qrtSize));

            quadTree.lowerRight = new QuadTree<Vector3>(new Vector3(qrtSize, 0, -qrtSize));
            CheckQuadtree(quadTree.lowerRight, NeighborFlags.DownRight, 1, this.size / 2, new Vector3(qrtSize, 0, -qrtSize));

            quadTree.upperLeft = new QuadTree<Vector3>(new Vector3(-qrtSize, 0, qrtSize));
            CheckQuadtree(quadTree.upperLeft, NeighborFlags.UpperLeft, 1, this.size / 2, new Vector3(-qrtSize, 0, qrtSize));

            quadTree.upperRight = new QuadTree<Vector3>(new Vector3(qrtSize, 0, qrtSize));
            CheckQuadtree(quadTree.upperRight, NeighborFlags.UpperRight, 1, this.size / 2, new Vector3(qrtSize, 0, qrtSize));

            rendererGroup = new BatchRendererGroup(OnCull);

            meshPositions = new NativeArray<Vector4>[16];
            for (var i = 0; i < matrixMeshArrays.Length; i++)
            {
                rendererGroup.AddBatch(mesh, i, material, gameObject.layer, ShadowCastingMode.Off, false, false, default, matrixMeshArrays[i].Count, null, gameObject);
                meshPositions[i] = new NativeArray<Vector4>(matrixMeshArrays[i].ToArray(), Allocator.Persistent);
            }
        }

        private void OnDisable()
        {
            rendererGroup?.Dispose();
            matrixMeshArrays = null;
        }

        private JobHandle OnCull(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext)
        {
            var cameraPosition = cullingContext.lodParameters.cameraPosition;

            // Figure out which cell this camera is in
            var cellSize = size / Mathf.Pow(2, maxDivisions - 1) / 4;
            var offset = new Vector3(cellSize / 2, 0, cellSize / 2);
            var newCell = Vector3Int.FloorToInt((cameraPosition + offset) / cellSize);

            var newPosition = new Vector3(newCell.x * cellSize, transform.position.y, newCell.z * cellSize);
            for (var i = 0; i < cullingContext.batchVisibility.Length; i++)
            {
                var matrices = rendererGroup.GetBatchMatrices(i);
                var batchVisibility = cullingContext.batchVisibility[i];

                var bounds = new Bounds(cameraPosition, Vector3.one * size * 2);
                rendererGroup.SetBatchBounds(i, bounds);

                var matrixList = matrixMeshArrays[i];
                for (var j = 0; j < matrixList.Count; j++)
                {
                    cullingContext.visibleIndices[batchVisibility.offset + j] = j;

                    var matrix = matrixList[j];
                    var position = matrix;// + (Vector4)newPosition;
                    matrices[j] = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(position.w, position.w, position.w));
                }
            }

            jobHandle.Complete();
            for (var i = 0; i < cullingContext.batchVisibility.Length; i++)
            {
                var job = new CullingJob(cullingContext, meshPositions[i], i, newPosition);
                jobHandle = job.Schedule(jobHandle);
            }

            return jobHandle;
        }

        private void CheckQuadtree(QuadTree<Vector3> quadTree, NeighborFlags flags, int subdivision, float size, Vector3 position)
        {
            var qrtSize = size / 4;

            quadTree.lowerLeft = new QuadTree<Vector3>(new Vector3(-qrtSize, 0, -qrtSize) + position);
            quadTree.lowerRight = new QuadTree<Vector3>(new Vector3(qrtSize, 0, -qrtSize) + position);
            quadTree.upperLeft = new QuadTree<Vector3>(new Vector3(-qrtSize, 0, qrtSize) + position);
            quadTree.upperRight = new QuadTree<Vector3>(new Vector3(qrtSize, 0, qrtSize) + position);

            // Check distance to each quadtree
            var closestIndex = -1;
            var closestDistance = float.MaxValue;
            for (var i = 0; i < 4; i++)
            {
                var qt = quadTree[i];
                var dist = ManhattanDistance(qt.Value, Vector3.zero);
                if (dist < closestDistance)
                {
                    closestIndex = i;
                    closestDistance = dist;
                }
            }

            for (var i = 0; i < 4; i++)
            {
                var qt = quadTree[i];

                // Set the flags depending on the index 
                var newFlags = NeighborFlags.None;
                switch (flags)
                {
                    case NeighborFlags.DownLeft:
                        {
                            switch (i)
                            {
                                case 0:
                                    newFlags = NeighborFlags.DownLeft;
                                    break;
                                case 1:
                                    newFlags = NeighborFlags.Down;
                                    break;
                                case 2:
                                    newFlags = NeighborFlags.Left;
                                    break;
                                case 3:
                                    break;
                            }
                            break;
                        }
                    case NeighborFlags.DownRight:
                        {
                            switch (i)
                            {
                                case 0:
                                    newFlags = NeighborFlags.Down;
                                    break;
                                case 1:
                                    newFlags = NeighborFlags.DownRight;
                                    break;
                                case 2:
                                    break;
                                case 3:
                                    newFlags = NeighborFlags.Right;
                                    break;
                            }
                            break;
                        }
                    case NeighborFlags.UpperLeft:
                        {
                            switch (i)
                            {
                                case 0:
                                    newFlags = NeighborFlags.Left;
                                    break;
                                case 1:
                                    break;
                                case 2:
                                    newFlags = NeighborFlags.UpperLeft;
                                    break;
                                case 3:
                                    newFlags = NeighborFlags.Up;
                                    break;
                            }
                            break;
                        }
                    case NeighborFlags.UpperRight:
                        {
                            switch (i)
                            {
                                case 0:
                                    break;
                                case 1:
                                    newFlags = NeighborFlags.Right;
                                    break;
                                case 2:
                                    newFlags = NeighborFlags.Up;
                                    break;
                                case 3:
                                    newFlags = NeighborFlags.UpperRight;
                                    break;
                            }
                            break;
                        }
                }

                if (closestIndex == i && subdivision < maxDivisions)
                {
                    CheckQuadtree(qt, flags, subdivision + 1, size / 2, qt.Value);
                }
                else
                {
                    matrixMeshArrays[(int)newFlags].Add(new Vector4(qt.Value.x - size / 4, 0, qt.Value.z - size / 4, size / 2));
                }
            }
        }

        private static float ManhattanDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(b.x - a.x) + Mathf.Abs(b.y - a.y) + Mathf.Abs(b.z - a.z);
        }

        // Each index is a pair of three x/y offsets, indexed by the neighbor flags
        private static readonly int[][][] edgeIndices =
        {
			// Each inner int[] is three vector2's, representing the horizontal and vertical offsets 
			// Coordinates 
			// None 0
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{0, 0, 1, 1, 1, 0 },

                new int[]{1, 0, 1, 1, 2, 1 },
                new int[]{1, 0, 2, 1, 2, 0 },

                new int[]{0, 1, 0, 2, 1, 2 },
                new int[]{0, 1, 1, 2, 1, 1 },

                new int[]{1, 1, 1, 2, 2, 2 },
                new int[]{1, 1, 2, 2, 2, 1 },
            },

			// Right 1
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{0, 0, 1, 1, 1, 0 },

                new int[]{0, 1, 0, 2, 1, 2 },
                new int[]{0, 1, 1, 2, 1, 1 },

                new int[]{1, 0, 1, 1, 2, 0 },
                new int[]{2, 0, 1, 1, 2, 2 },
                new int[]{1, 1, 1, 2, 2, 2 },
            },
			
			// Up 2
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{0, 0, 1, 1, 1, 0 },

                new int[]{1, 0, 1, 1, 2, 1 },
                new int[]{1, 0, 2, 1, 2, 0 },

                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{1, 1, 2, 2, 2, 1 },

                new int[]{0, 1, 0, 2, 1, 1 },
            },

			// Up Right 3
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{0, 0, 1, 1, 1, 0 },

                new int[]{1, 0, 1, 1, 2, 0 },
                new int[]{2, 0, 1, 1, 2, 2 },

                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{0, 1, 0, 2, 1, 1 },
            },

			// Left 4
			new int[][]
            {
                new int[]{0, 0, 1, 1, 1, 0 },

                new int[]{1, 0, 1, 1, 2, 1 },
                new int[]{1, 0, 2, 1, 2, 0 },

                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 1, 2 },

                new int[]{1, 1, 1, 2, 2, 2 },
                new int[]{1, 1, 2, 2, 2, 1 },
            },

			// Left and Right? 5
			new int[][] { },

			// Upper left 6
			new int[][]
            {
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{0, 0, 1, 1, 1, 0 },

                new int[]{1, 0, 1, 1, 2, 0 },
                new int[]{2, 0, 1, 1, 2, 1 },

                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{1, 1, 2, 2, 2, 1 },

            },

			// Up Left Right 7 
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{0, 0, 1, 1, 1, 0 },

                new int[]{1, 0, 1, 1, 2, 1 },
                new int[]{1, 0, 2, 1, 2, 0 },

                new int[]{0, 1, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{1, 1, 2, 2, 2, 1 },
            },

			// Down 8
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{2, 0, 0, 0, 1, 1 },
                new int[]{1, 1, 2, 1, 2, 0 },
                new int[]{0, 1, 0, 2, 1, 2 },
                new int[]{0, 1, 1, 2, 1, 1 },
                new int[]{1, 1, 1, 2, 2, 2 },
                new int[]{1, 1, 2, 2, 2, 1 },
            },

			// Lower Right 9
			new int[][]
            {
                new int[]{1, 1, 0, 0, 0, 1 },
                new int[]{0, 1, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 1, 2 },
                new int[]{1, 2, 2, 2, 1, 1 },
                new int[]{1, 1, 2, 2, 2, 0 },
                new int[]{2, 0, 0, 0, 1, 1 },
            },

			// Up Down 10
			new int[][] { },

			// Up Down Right 11
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{0, 0, 1, 1, 2, 0 },

                new int[]{2, 0, 1, 1, 2, 2 },
                new int[]{1, 1, 0, 2, 2, 2 },

                new int[]{0, 1, 0, 2, 1, 1 },
            },

			// Down Left 12
			new int[][]
            {
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 1, 2 },
                new int[]{1, 2, 2, 2, 1, 1 },
                new int[]{1, 1, 2, 2, 2, 1 },
                new int[]{2, 1, 2, 0, 1, 1 },
                new int[]{1, 1, 2, 0, 0, 0 },
            },

			// Down Left Right 13
			new int[][]
            {
                new int[]{0, 0, 1, 1, 2, 0 },
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{2, 0, 1, 1, 2, 2 },
                new int[]{1, 1, 0, 2, 1, 2 },
                new int[]{1, 1, 1, 2, 2, 2 },
            },

			// Up Down Left 14
			new int[][]
            {
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{0, 0, 1, 1, 2, 0 },
                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{2, 0, 1, 1, 2, 1 },
                new int[]{1, 1, 2, 2, 2, 1 },
            },

			// All 15
			new int[][]
            {
                new int[]{0, 0, 1, 1, 2, 0 },
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{0, 2, 1, 1, 2, 2 },
            }
        };
    }
}