// Created by Ben Sims 23/07/20

using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace FoxieGames
{
    public struct CullingJob : IJob
    {
        private BatchCullingContext cullingContext;
        private NativeArray<Vector4> positions;
        private int batchIndex;
        private Vector3 cameraPosition;

        public CullingJob(BatchCullingContext cullingContext, NativeArray<Vector4> positions, int batchIndex, Vector3 cameraPosition)
        {
            this.cullingContext = cullingContext;
            this.positions = positions;
            this.batchIndex = batchIndex;
            this.cameraPosition = cameraPosition;
        }

        void IJob.Execute()
        {
            var batchVisibility = cullingContext.batchVisibility[batchIndex];
            var visibleCount = 0;
            var planes = cullingContext.cullingPlanes;

            for (var i = 0; i < batchVisibility.instancesCount; i++)
            {
                var positionScale = positions[i];
                var position = (Vector3)positionScale + cameraPosition;
                var scale = positionScale.w;

                var corner00 = position;
                var corner01 = position + new Vector3(scale, 0, 0);
                var corner10 = position + new Vector3(0, 0, scale);
                var corner11 = position + new Vector3(scale, 0, scale);

                var isVisible = true;
                for (var j = 0; j < planes.Length; j++)
                {
                    var plane = planes[j];

                    if (!plane.GetSide(corner00) && !plane.GetSide(corner01) && !plane.GetSide(corner10) && !plane.GetSide(corner11))
                    {
                        isVisible = false;
                        break;
                    }
                }

                if (isVisible)
                {
                    cullingContext.visibleIndices[batchVisibility.offset + visibleCount] = i;
                    visibleCount++;
                }
            }

            batchVisibility.visibleCount = visibleCount;
            cullingContext.batchVisibility[batchIndex] = batchVisibility;
        }
    }
}