﻿/// <summary>
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
        // Read-only properties
        public long RAM_Usage { get; private set; } // RAM usage
        public long VRAM_Usage { get; private set; } // VRAM usage

        public bool maskGenerated = false;

        // Buffers
        public ComputeBuffer RenderCountBuffer;
        public ComputeBuffer voxelUpdateMaskBuffer;

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

            // Keep track of if the mask buffers been populated
            maskGenerated = false;

            // Voxel mask buffer
            if (voxelUpdateMaskBuffer != null) voxelUpdateMaskBuffer.Release();
            voxelUpdateMaskBuffer = new ComputeBuffer(injectionTextureResolution.x * injectionTextureResolution.y, sizeof(uint), ComputeBufferType.Default);
            VRAM_Usage += Convert.ToInt64(injectionTextureResolution.x * injectionTextureResolution.y * sizeof(uint) * 8);

            // Render counters
            if (RenderCountBuffer != null) RenderCountBuffer.Release();
            RenderCountBuffer = new ComputeBuffer(renderCounterMax, sizeof(int), ComputeBufferType.IndirectArguments);
            VRAM_Usage += Convert.ToInt64(renderCounterMax * sizeof(int) * 8);
        }

        /// <summary>
        /// Disposes all buffers
        /// </summary>
        public void DisposeBuffers()
        {
            // Zero VRAM usage meter
            VRAM_Usage = 0;

            // Release buffers
            Helpers.ReleaseBufferRef(ref voxelUpdateMaskBuffer);
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
