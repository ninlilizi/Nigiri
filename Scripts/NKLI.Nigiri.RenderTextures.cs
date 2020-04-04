/// <summary>
/// NKLI     : Nigiri - RenderTextures
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
    public class RenderTextures : ScriptableObject, IDisposable
    {
        // Read-only properties
        public long RAM_Usage { get; private set; } // RAM usage
        public long VRAM_Usage { get; private set; } // VRAM usage

        // Descriptors
        private RenderTextureDescriptor voxelGridDescriptorFloat4;

        // Grid Textures
        //public RenderTexture voxelGrid1;
        //public RenderTexture voxelGrid2;
        //public RenderTexture voxelGrid3;
        //public RenderTexture voxelGrid4;
        //public RenderTexture voxelGrid5;

        // Render Textures
        public RenderTexture lightingTexture;
        public RenderTexture lightingTexture2;
        public RenderTexture lightingTextureMono;
        public RenderTexture lightingTexture2Mono;
        public RenderTexture positionTexture;
        public RenderTexture depthTexture;
        public RenderTexture blur;
        public RenderTexture gi;

        /// <summary>
        /// Generic create method
        /// </summary>
        /// <param name="resolution"></param>
        /// <param name="localCam"></param>
        /// <param name="injectionTextureResolution"></param>
        /// <param name="subsamplingRatio"></param>
        public void Create(int resolution, Camera localCam, Vector2Int injectionTextureResolution, int subsamplingRatio)
        {
            // Create voxel grid textures
            CreateVoxelGrid(resolution);

            // Create render textures
            CreateRenderTextures(localCam, injectionTextureResolution, subsamplingRatio);
        }

        /// <summary>
        /// /// Create voxel grid textures
        /// </summary>
        /// <param name="resolution"></param>
        private void CreateVoxelGrid(int resolution)
        {
            // First release any existing
            DisposeGridTextures(false);

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

            //voxelGrid1 = new RenderTexture(voxelGridDescriptorFloat4);

            voxelGridDescriptorFloat4.width = resolution / 2;
            voxelGridDescriptorFloat4.height = resolution / 2;
            voxelGridDescriptorFloat4.volumeDepth = resolution / 2;

            //voxelGrid2 = new RenderTexture(voxelGridDescriptorFloat4);

            voxelGridDescriptorFloat4.width = resolution / 4;
            voxelGridDescriptorFloat4.height = resolution / 4;
            voxelGridDescriptorFloat4.volumeDepth = resolution / 4;

            //voxelGrid3 = new RenderTexture(voxelGridDescriptorFloat4);

            voxelGridDescriptorFloat4.width = resolution / 8;
            voxelGridDescriptorFloat4.height = resolution / 8;
            voxelGridDescriptorFloat4.volumeDepth = resolution / 8;

            //voxelGrid4 = new RenderTexture(voxelGridDescriptorFloat4);

            voxelGridDescriptorFloat4.width = resolution / 16;
            voxelGridDescriptorFloat4.height = resolution / 16;
            voxelGridDescriptorFloat4.volumeDepth = resolution / 16;

            //voxelGrid5 = new RenderTexture(voxelGridDescriptorFloat4);

            //voxelGrid1.Create();
            //voxelGrid2.Create();
            //voxelGrid3.Create();
            //voxelGrid4.Create();
            //voxelGrid5.Create();
        }

        /// <summary>
        /// Create render texture
        /// </summary>
        /// <param name="localCam"></param>
        /// <param name="injectionTextureResolution"></param>
        /// <param name="subsamplingRatio"></param>
        public void CreateRenderTextures(Camera localCam, Vector2Int injectionTextureResolution, int subsamplingRatio)
        {
            // First release any existing
            DisposeRenderTextures(false);

            if (injectionTextureResolution.x == 0 || injectionTextureResolution.y == 0) injectionTextureResolution = new Vector2Int(1280, 720);

            if (lightingTexture != null) lightingTexture.Release();
            if (lightingTexture2 != null) lightingTexture2.Release();
            if (depthTexture != null) depthTexture.Release();
            if (gi != null) gi.Release();
            if (blur != null) blur.Release();

            lightingTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
            lightingTexture2 = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
            if (localCam.stereoEnabled) positionTexture = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
            else positionTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
            if (localCam.stereoEnabled) depthTexture = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.RHalf);
            else depthTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.RHalf);
            gi = new RenderTexture(injectionTextureResolution.x * subsamplingRatio, injectionTextureResolution.y * subsamplingRatio, 0, RenderTextureFormat.ARGBHalf);
            blur = new RenderTexture(injectionTextureResolution.x * subsamplingRatio, injectionTextureResolution.y * subsamplingRatio, 0, RenderTextureFormat.ARGBHalf);
            lightingTexture.filterMode = FilterMode.Bilinear;
            lightingTexture2.filterMode = FilterMode.Bilinear;

            depthTexture.filterMode = FilterMode.Bilinear;
            blur.filterMode = FilterMode.Bilinear;
            gi.filterMode = FilterMode.Bilinear;

            if (localCam.stereoEnabled)
            {
                //We cut the injection images in half to avoid duplicate work in stereo
                lightingTextureMono = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
                lightingTexture2Mono = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);

                lightingTextureMono.vrUsage = VRTextureUsage.None;
                lightingTexture2Mono.vrUsage = VRTextureUsage.None;
                positionTexture.vrUsage = VRTextureUsage.None; // We disable this because it needs to not be stereo so the voxer does'nt do double the work
                lightingTexture.vrUsage = VRTextureUsage.TwoEyes;
                lightingTexture2.vrUsage = VRTextureUsage.TwoEyes;
                blur.vrUsage = VRTextureUsage.TwoEyes;
                gi.vrUsage = VRTextureUsage.TwoEyes;
                depthTexture.vrUsage = VRTextureUsage.TwoEyes; // Might cause regression with voxelization
                lightingTextureMono.Create();
                lightingTexture2Mono.Create();
            }

            lightingTexture.Create();
            lightingTexture2.Create();
            positionTexture.Create();
            depthTexture.Create();
            blur.Create();
            gi.Create();



            // VRAM estimation
            if (lightingTexture != null)
                VRAM_Usage += lightingTexture.width * lightingTexture.height * bitValue(lightingTexture);

            if (lightingTexture2 != null)
                VRAM_Usage += lightingTexture2.width * lightingTexture2.height * bitValue(lightingTexture2);

            if (positionTexture != null)
                VRAM_Usage += positionTexture.width * positionTexture.height * bitValue(positionTexture);

            if (depthTexture != null)
                VRAM_Usage += depthTexture.width * depthTexture.height * bitValue(depthTexture);

            if (blur != null)
                VRAM_Usage += blur.width * blur.height * bitValue(blur);

            if (gi != null)
                VRAM_Usage += gi.width * gi.height * bitValue(gi);
        }

        int bitValue(RenderTexture x)
        {

            int bit = 0;
            switch (x.format)
            {
                case RenderTextureFormat.ARGBHalf:
                    bit = 16 * 4;
                    break;
                case RenderTextureFormat.RFloat:
                    bit = 32;
                    break;
                case RenderTextureFormat.RHalf:
                    bit = 16;
                    break;
                default:
                    break;
            }
            if (bit == 0)
                Debug.Log(bit + " " + x.name + " bit Value is 0, resolve");
            return bit;
        }

        /// <summary>
        /// Disposes all textures, optionally also Destroys
        /// </summary>
        /// <param name="destroy"></param>
        public void DisposeTextures(bool destroy)
        {
            DisposeRenderTextures(destroy);
            DisposeGridTextures(destroy);
        }

        /// <summary>
        /// Disposes all render textures, optionally also Destroys
        /// </summary>
        /// <param name="destroy"></param>
        public void DisposeRenderTextures(bool destroy)
        {
            // Zero VRAM usage meter
            VRAM_Usage = 0;

            // Dispose render textures
            Helpers.DisposeTextureRef(ref lightingTexture, destroy);
            Helpers.DisposeTextureRef(ref lightingTexture2, destroy);
            Helpers.DisposeTextureRef(ref lightingTextureMono, destroy);
            Helpers.DisposeTextureRef(ref lightingTexture2Mono, destroy);
            Helpers.DisposeTextureRef(ref positionTexture, destroy);
            Helpers.DisposeTextureRef(ref depthTexture, destroy);
            Helpers.DisposeTextureRef(ref gi, destroy);
            Helpers.DisposeTextureRef(ref blur, destroy);

        }

        /// <summary>
        /// Disposes all grid textures, optionally also Destroys
        /// </summary>
        /// <param name="destroy"></param>
        public void DisposeGridTextures(bool destroy)
        {
            // Dispose voxel grid textures
            //Helpers.DisposeTextureRef(ref voxelGrid1, destroy);
            //Helpers.DisposeTextureRef(ref voxelGrid2, destroy);
            //Helpers.DisposeTextureRef(ref voxelGrid3, destroy);
            //Helpers.DisposeTextureRef(ref voxelGrid4, destroy);
            //Helpers.DisposeTextureRef(ref voxelGrid5, destroy);
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

        ~RenderTextures()
        {
            Dispose();
        }
        #endregion
    }
}
