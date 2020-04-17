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

        // Render Textures
        //public RenderTexture lightingTexture;
        //public RenderTexture texture_GBuffer0;
        //public RenderTexture lightingTextureMono;
        //public RenderTexture lightingTexture2Mono;
        public RenderTexture positionTexture;
        public RenderTexture depthTexture;
        public RenderTexture blur;
        public RenderTexture gi;

        /// <summary>
        /// Generic create method
        /// </summary>
        /// <param name="localCam"></param>
        /// <param name="injectionTextureResolution"></param>
        /// <param name="subsamplingRatio"></param>
        public void Create(Camera localCam, Vector2Int injectionTextureResolution, int subsamplingRatio)
        {
            // Create render textures
            CreateRenderTextures(localCam, injectionTextureResolution, subsamplingRatio);
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

            //if (lightingTexture != null) lightingTexture.Release();
            //if (texture_GBuffer0 != null) texture_GBuffer0.Release();
            if (depthTexture != null) depthTexture.Release();
            if (gi != null) gi.Release();
            if (blur != null) blur.Release();

            //lightingTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
            //texture_GBuffer0 = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
            if (localCam.stereoEnabled) positionTexture = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
            else positionTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
            if (localCam.stereoEnabled) depthTexture = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.RHalf);
            else depthTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.RHalf);
            gi = new RenderTexture(injectionTextureResolution.x * subsamplingRatio, injectionTextureResolution.y * subsamplingRatio, 0, RenderTextureFormat.ARGBHalf);
            blur = new RenderTexture(injectionTextureResolution.x * subsamplingRatio, injectionTextureResolution.y * subsamplingRatio, 0, RenderTextureFormat.ARGBHalf);
            //lightingTexture.filterMode = FilterMode.Bilinear;
            //texture_GBuffer0.filterMode = FilterMode.Bilinear;

            depthTexture.filterMode = FilterMode.Bilinear;
            blur.filterMode = FilterMode.Bilinear;
            gi.filterMode = FilterMode.Bilinear;

            if (localCam.stereoEnabled)
            {
                //We cut the injection images in half to avoid duplicate work in stereo
                //lightingTextureMono = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
                //lightingTexture2Mono = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);

                //lightingTextureMono.vrUsage = VRTextureUsage.None;
                //lightingTexture2Mono.vrUsage = VRTextureUsage.None;
                positionTexture.vrUsage = VRTextureUsage.None; // We disable this because it needs to not be stereo so the voxer does'nt do double the work
                //lightingTexture.vrUsage = VRTextureUsage.TwoEyes;
                //texture_GBuffer0.vrUsage = VRTextureUsage.TwoEyes;
                blur.vrUsage = VRTextureUsage.TwoEyes;
                gi.vrUsage = VRTextureUsage.TwoEyes;
                depthTexture.vrUsage = VRTextureUsage.TwoEyes; // Might cause regression with voxelization
                //lightingTextureMono.Create();
                //lightingTexture2Mono.Create();
            }

            //lightingTexture.Create();
            //texture_GBuffer0.Create();
            positionTexture.Create();
            depthTexture.Create();
            blur.Create();
            gi.Create();



            // VRAM estimation
            //if (lightingTexture != null)
            //    VRAM_Usage += lightingTexture.width * lightingTexture.height * bitValue(lightingTexture);

            //if (texture_GBuffer0 != null)
            //    VRAM_Usage += texture_GBuffer0.width * texture_GBuffer0.height * bitValue(texture_GBuffer0);

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
            //Helpers.DisposeTextureRef(ref lightingTexture, destroy);
            //Helpers.DisposeTextureRef(ref texture_GBuffer0, destroy);
            //Helpers.DisposeTextureRef(ref lightingTextureMono, destroy);
            //Helpers.DisposeTextureRef(ref lightingTexture2Mono, destroy);
            Helpers.DisposeTextureRef(ref positionTexture, destroy);
            Helpers.DisposeTextureRef(ref depthTexture, destroy);
            Helpers.DisposeTextureRef(ref gi, destroy);
            Helpers.DisposeTextureRef(ref blur, destroy);

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
