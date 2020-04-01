/// <summary>
/// NKLI     : Nigiri - ComputeBuffers
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using UnityEngine;

namespace NKLI.Nigiri
{
    /// <summary>
    /// Holds all render textures.
    /// Robust implementation of ScriptableObject destruction and IDisposable to ensure memory leak-proof lifetime management.
    /// </summary>
    public class ComputeBuffers : ScriptableObject, IDisposable
    {
        // Buffers
        public ComputeBuffer RenderCountBuffer;

        public ComputeBuffer voxelUpdateSampleCount;
        public ComputeBuffer voxelUpdateMaskBufferNaive;
        public ComputeBuffer voxelUpdateMaskBuffer;
        public ComputeBuffer voxelUpdateSampleBuffer;
        public ComputeBuffer voxelUpdateSampleCountBuffer;

        /// <summary>
        /// Generic create method
        /// </summary>
        /// <param name="resolution"></param>
        /// <param name="injectionTextureResolution"></param>
        /// <param name="renderCounterMax"></param>
        public void Create(int resolution, Vector2Int injectionTextureResolution, int renderCounterMax)
        {
            // Create buffers
            CreateComputeBuffers(resolution, injectionTextureResolution, renderCounterMax);
        }

        /// <summary>
        /// Creates compute buffers
        /// </summary>
        /// <param name="resolution"></param>
        /// <param name="injectionTextureResolution"></param>
        /// <param name="renderCounterMax"></param>
        public void CreateComputeBuffers(int resolution, Vector2Int injectionTextureResolution, int renderCounterMax)
        {
            // Release any existing buffers
            DisposeBuffers();

            // Voxel Primary
            if (voxelUpdateSampleCount != null) voxelUpdateSampleCount.Release();
            voxelUpdateSampleCount = new ComputeBuffer(resolution * resolution * resolution, 4, ComputeBufferType.Default);

            if (voxelUpdateMaskBufferNaive != null) voxelUpdateMaskBufferNaive.Release();
            voxelUpdateMaskBufferNaive = new ComputeBuffer(injectionTextureResolution.x * injectionTextureResolution.y, sizeof(uint), ComputeBufferType.Append);

            if (voxelUpdateMaskBuffer != null) voxelUpdateMaskBuffer.Release();
            voxelUpdateMaskBuffer = new ComputeBuffer(injectionTextureResolution.x * injectionTextureResolution.y, sizeof(uint), ComputeBufferType.Default);

            if (voxelUpdateSampleBuffer != null) voxelUpdateSampleBuffer.Release();
            voxelUpdateSampleBuffer = new ComputeBuffer(resolution * resolution * resolution, sizeof(float) * 4, ComputeBufferType.Default);

            if (voxelUpdateSampleCountBuffer != null) voxelUpdateSampleCountBuffer.Release();
            voxelUpdateSampleCountBuffer = new ComputeBuffer(resolution * resolution * resolution, sizeof(uint), ComputeBufferType.Default);
            ///END Voxel Primary

            if (RenderCountBuffer != null) RenderCountBuffer.Release();
            RenderCountBuffer = new ComputeBuffer(renderCounterMax, sizeof(int), ComputeBufferType.IndirectArguments);

            //if (MaskCountBuffer != null) MaskCountBuffer.Release();
            //MaskCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
            ///END Counters
        }

        /// <summary>
        /// Disposes all buffers
        /// </summary>
        public void DisposeBuffers()
        {
            Helpers.ReleaseBufferRef(ref voxelUpdateSampleCount);
            Helpers.ReleaseBufferRef(ref voxelUpdateMaskBufferNaive);
            Helpers.ReleaseBufferRef(ref voxelUpdateMaskBuffer);
            Helpers.ReleaseBufferRef(ref voxelUpdateSampleBuffer);
            Helpers.ReleaseBufferRef(ref voxelUpdateSampleCountBuffer);
            Helpers.ReleaseBufferRef(ref RenderCountBuffer);
        }

        #region IDisposable + Unity Scriped Destruction support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Attempt to dispose any existing buffers
                    DisposeBuffers();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        public void OnDestroy()
        {
            Dispose();
        }

        ~ComputeBuffers()
        {
            Dispose();
        }
        #endregion
    }
}
