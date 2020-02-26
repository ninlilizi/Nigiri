/// <summary>
/// NKLI     : Nigiri - RenderTextures
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using UnityEngine;

namespace NKLI.Nigiri
{
    public class RenderTextures : ScriptableObject, IDisposable
    {
        // Descriptors
        private RenderTextureDescriptor voxelGridDescriptorFloat4;

        // Textures
        public RenderTexture voxelGrid1;
        public RenderTexture voxelGrid2;
        public RenderTexture voxelGrid3;
        public RenderTexture voxelGrid4;
        public RenderTexture voxelGrid5;
        public RenderTexture voxelGridCascade1;
        public RenderTexture voxelGridCascade2;

        /// <summary>
        /// Creates all textures
        /// </summary>
        public void Create(int resolution)
        {
            // Attempt to release any existing textures
            DisposeTextures(false);

            // Create voxel grid textures
            CreateVoxelGrid(resolution);

            // Create render textures
        }

        /// <summary>
        /// create voxel grid textures
        /// </summary>
        private void CreateVoxelGrid(int resolution)
        {

            voxelGridDescriptorFloat4 = new RenderTextureDescriptor();
            voxelGridDescriptorFloat4.bindMS = false;
            voxelGridDescriptorFloat4.colorFormat = RenderTextureFormat.ARGBHalf;
            voxelGridDescriptorFloat4.depthBufferBits = 0;
            voxelGridDescriptorFloat4.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            voxelGridDescriptorFloat4.enableRandomWrite = true;
            voxelGridDescriptorFloat4.width = resolution;
            voxelGridDescriptorFloat4.height = resolution;
            voxelGridDescriptorFloat4.volumeDepth = resolution;
            voxelGridDescriptorFloat4.msaaSamples = 1;
            voxelGridDescriptorFloat4.sRGB = true;

            voxelGrid1 = new RenderTexture(voxelGridDescriptorFloat4);

            voxelGridDescriptorFloat4.width = resolution / 2;
            voxelGridDescriptorFloat4.height = resolution / 2;
            voxelGridDescriptorFloat4.volumeDepth = resolution / 2;

            voxelGrid2 = new RenderTexture(voxelGridDescriptorFloat4);
            voxelGridCascade1 = new RenderTexture(voxelGridDescriptorFloat4);

            voxelGridDescriptorFloat4.width = resolution / 4;
            voxelGridDescriptorFloat4.height = resolution / 4;
            voxelGridDescriptorFloat4.volumeDepth = resolution / 4;

            voxelGrid3 = new RenderTexture(voxelGridDescriptorFloat4);
            voxelGridCascade2 = new RenderTexture(voxelGridDescriptorFloat4);

            voxelGridDescriptorFloat4.width = resolution / 8;
            voxelGridDescriptorFloat4.height = resolution / 8;
            voxelGridDescriptorFloat4.volumeDepth = resolution / 8;

            voxelGrid4 = new RenderTexture(voxelGridDescriptorFloat4);

            voxelGridDescriptorFloat4.width = resolution / 16;
            voxelGridDescriptorFloat4.height = resolution / 16;
            voxelGridDescriptorFloat4.volumeDepth = resolution / 16;

            voxelGrid5 = new RenderTexture(voxelGridDescriptorFloat4);

            voxelGridCascade1.Create();
            voxelGridCascade2.Create();
            voxelGrid1.Create();
            voxelGrid2.Create();
            voxelGrid3.Create();
            voxelGrid4.Create();
            voxelGrid5.Create();
        }

        /// <summary>
        /// Disposes all textures, optionally also Destroys
        /// </summary>
        public void DisposeTextures(bool destroy)
        {
            // Dispose voxel grid textures
            DisposeTextureRef(ref voxelGridCascade1, destroy);
            DisposeTextureRef(ref voxelGridCascade2, destroy);
            DisposeTextureRef(ref voxelGrid1, destroy);
            DisposeTextureRef(ref voxelGrid2, destroy);
            DisposeTextureRef(ref voxelGrid3, destroy);
            DisposeTextureRef(ref voxelGrid4, destroy);
            DisposeTextureRef(ref voxelGrid5, destroy);

            // Dispose render textures
        }

        /// <summary>
        /// Release and optionally destroy a texture
        /// </summary>
        /// <param name="rt"></param>
        public void DisposeTextureRef(ref RenderTexture rt, bool destroy)
        {
            if (rt != null)
            {
                rt.Release();
                if (destroy) DestroyImmediate(rt);
            }
        }

        #region IDisposable + Unity Scriped Destruction support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Attempt to dispose any existing textures
                    DisposeTextures(true);
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
        #endregion
    }
}
