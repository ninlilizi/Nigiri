/// <summary>
/// NKLI     : Nigiri - SVO Voxelization
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using UnityEngine;

namespace NKLI.Nigiri.SVO
{
    /// <summary>
    /// SVO Voxelization
    /// </summary>
    public class Voxelizer
    {
        // Read-only properties
        public Tree SVO_Tree { get; private set; }
        public float Emissive_Intensity { get; private set; }
        public float Shadow_Strength { get; private set; }
        public float Occlusion_Gain { get; private set; }
        public float GI_Area_Size { get; private set; }
        public Vector3 GridOffset { get; private set; }

        // Read-Write properties
        public bool Debug_Filtering { get; set; }

        // Let a few frames render before kicking off,
        //  Dirty fix for the built-in depth texture being
        //  unabsilable till after a camera tender
        //private uint WarmUp = 0;

        // Max depth of SVO
        private int max_depth;

        // Compute
        readonly private ComputeShader Shader_VoxelEncoder;
        readonly private ComputeShader Shader_SVOSplitter;
        readonly private ComputeShader Shader_SVOMipmapper;

        /// <summary>
        /// Constructor
        /// </summary>
        public Voxelizer(Tree SVO, float emissiveIntensity, float shadowStrength, float occlusionGain, float giAreaSize)
        {
            // Sanity check input
            if (SVO == null)
            {
                // Provide explanation
                Debug.LogError("[Nigiri] <NKLI.Nigiri.SVO.Voxelizer> SVO == null PTR");

                // Throw exception
                throw new System.Exception("[Nigiri] <NKLI.Nigiri.SVO.Voxelizer> Null PTRs detected");
            }

            // Load encode shader
            Shader_VoxelEncoder = Resources.Load("NKLI_Nigiri_SVOVoxelizer") as ComputeShader;
            if (Shader_VoxelEncoder == null) throw new Exception("[Nigiri] failed to load compute shader 'NKLI_Nigiri_SVOVoxelizer'");

            // Load splitter shader
            Shader_SVOSplitter = Resources.Load("NKLI_Nigiri_SVOSplitter") as ComputeShader;
            if (Shader_SVOSplitter == null) throw new Exception("[Nigiri] failed to load compute shader 'NKLI_Nigiri_SVOSplitter'");

            // Load mipmapper shader
            Shader_SVOMipmapper = Resources.Load("NKLI_Nigiri_SVOMipmapper") as ComputeShader;
            if (Shader_SVOMipmapper == null) throw new Exception("[Nigiri] failed to load compute shader 'NKLI_Nigiri_SVOMipmapper'");

            // Binds to SVO
            SVO_Tree = SVO;

            GridOffset = new Vector3(-(giAreaSize / 2), -(giAreaSize / 2), -(giAreaSize / 2));

            // Sets values
            Emissive_Intensity = emissiveIntensity;
            Shadow_Strength = shadowStrength;
            Occlusion_Gain = occlusionGain;
            GI_Area_Size = giAreaSize;
            max_depth = (int)SVO_Tree.MaxDepth;

        }

        /// <summary>
        /// Updates voxelization parameters
        /// </summary>
        /// <param name="emissiveIntensity"></param>
        /// <param name="shadowStrength"></param>
        /// <param name="occlusionGain"></param>
        public void UpdateParameters(float emissiveIntensity, float shadowStrength, float occlusionGain)
        {
            Emissive_Intensity = emissiveIntensity;
            Shadow_Strength = shadowStrength;
            Occlusion_Gain = occlusionGain;
        }

        /// <summary>
        /// Voxelizes the scene
        /// </summary>
        public bool VoxelizeScene(int sampleCount, RenderTexture positionTexture, RenderTexture lightingTexture, RenderTexture lightingTexture2, ComputeBuffer maskBuffer)
        {
            //if (WarmUp > 4)
            //{
                // Sanity check inputs
                if (SVO_Tree == null || positionTexture == null || lightingTexture == null || lightingTexture2 == null)
                {
                    // Provide explanation
                    if (SVO_Tree == null) Debug.LogError("[Nigiri] <NKLI.Nigiri.SVO.Voxelizer.VoxelizeScene> SVO == null PTR");
                    if (positionTexture == null) Debug.LogError("[Nigiri] <NKLI.Nigiri.SVO.Voxelizer.VoxelizeScene> Position Texture == null PTR");
                    if (lightingTexture == null) Debug.LogError("[Nigiri] <NKLI.Nigiri.SVO.Voxelizer.VoxelizeScene> Lighting Texture == null PTR");
                    if (lightingTexture2 == null) Debug.LogError("[Nigiri] <NKLI.Nigiri.SVO.Voxelizer.VoxelizeScene> Lighting Texture 2 == null PTR");

                    // Throw exception
                    throw new System.Exception("[Nigiri] <NKLI.Nigiri.SVO.Voxelizer.VoxelizeScene> Null PTRs detected");
                }

                // If SVO worker threads are suspended,
                // then wake then up.
                SVO_Tree.ResumeWorkers();

                // Set counter buffer to initial values
                SVO_Tree.SetCounterBuffer();

                // Set buffers
                Shader_VoxelEncoder.SetBuffer(0, "_SVO", SVO_Tree.Buffer_SVO);
                Shader_VoxelEncoder.SetBuffer(0, "_SVO_Counters", SVO_Tree.Buffer_Counters);
                Shader_VoxelEncoder.SetBuffer(0, "_SVO_SplitQueue", SVO_Tree.Buffer_Queue_Split);
                Shader_VoxelEncoder.SetBuffer(0, "_SVO_MipmapQueue", SVO_Tree.Buffer_Queue_Mipmap);
                Shader_VoxelEncoder.SetBuffer(0, "_maskBuffer", maskBuffer);

                // Set textures
                Shader_VoxelEncoder.SetTextureFromGlobal(0, "_CameraDepthTexture", "_CameraDepthTexture");
                Shader_VoxelEncoder.SetTexture(0, "positionTexture", positionTexture);
                Shader_VoxelEncoder.SetTexture(0, "lightingTexture", lightingTexture);
                Shader_VoxelEncoder.SetTexture(0, "lightingTexture2", lightingTexture2);

                // Set values
                Shader_VoxelEncoder.SetInt("_mipmapQueueEmpty", SVO_Tree.MipmapQueueEmpty ? 1 : 0);
                Shader_VoxelEncoder.SetFloat("_emissiveIntensity", Emissive_Intensity);
                Shader_VoxelEncoder.SetFloat("_shadowStrength", Shadow_Strength);
                Shader_VoxelEncoder.SetFloat("_occlusionGain", Occlusion_Gain);
                Shader_VoxelEncoder.SetFloat("_giAreaSize", GI_Area_Size);
                Shader_VoxelEncoder.SetInt("_maxDepth", max_depth);

                // Dispatch
                Shader_VoxelEncoder.Dispatch(0, sampleCount / 1024, 1, 1);

            //}
            //else WarmUp++;

            // We're done here
            return true;
        }

        /// <summary>
        /// Processes split queue
        /// </summary>
        /// <returns></returns>
        public bool SplitNodes()
        {
            // Only if a successful readback has been completed and flagged for action
            if (SVO_Tree.AbleToSplit && (SVO_Tree.SplitQueueSparseCount > 0))
            {
                // We don't want to run again till there is something to do
                SVO_Tree.AbleToSplit = false;

                // Send buffer to GPU
                SVO_Tree.Buffer_Queue_Split.SetData(SVO_Tree.queue_Split_Sparse);

                // Rounds split queue length to nearest mul of 8 
                //  to match dispatch thread group size
                int queueLength = Math.Max(((SVO_Tree.SplitQueueSparseCount + 8 - (8 / Math.Abs(8))) / 8) * 8, 8);

                // Set buffers
                Shader_SVOSplitter.SetBuffer(0, "_SVO", SVO_Tree.Buffer_SVO);
                Shader_SVOSplitter.SetBuffer(0, "_SVO_Counters", SVO_Tree.Buffer_Counters);
                Shader_SVOSplitter.SetBuffer(0, "_SVO_Counters_Internal", SVO_Tree.Buffer_Counters_Internal);
                Shader_SVOSplitter.SetBuffer(0, "_SVO_SplitQueue", SVO_Tree.Buffer_Queue_Split);

                // Set values
                Shader_SVOSplitter.SetFloat("_SVO_MaxNodes", SVO_Tree.Buffer_SVO_Count);

                // Dispatch
                Shader_SVOSplitter.Dispatch(0, queueLength / 8, 1, 1);

                //Debug.Log("Nodes split successfully!");

                // Resume thread
                SVO_Tree.ResumeNodeWorker();

                // We're done here
                return true;
            }
            else
            {
                // Resume thread
                SVO_Tree.ResumeNodeWorker();

                return false;
            }
        }

        /// <summary>
        /// Processes mipmap queue
        /// </summary>
        /// <returns></returns>
        public bool MipmapNodes()
        {
            // Only if a successful readback has been completed and flagged for action
            if (SVO_Tree.AbleToMipmap && (SVO_Tree.MipmapQueueSparseCount > 0))
            {
                // We don't want to run again till there is something to do
                SVO_Tree.AbleToMipmap = false;

                // Send buffer to GPU
                SVO_Tree.Buffer_Queue_Mipmap.SetData(SVO_Tree.queue_Mipmap_Sparse);

                // Rounds mipmap queue length to nearest mul of 8 
                //  to match dispatch thread group size
                int queueLength = Math.Max(((SVO_Tree.MipmapQueueSparseCount + 8 - (8 / Math.Abs(8))) / 8) * 8, 8);

                // Set buffers
                Shader_SVOMipmapper.SetBuffer(0, "_SVO", SVO_Tree.Buffer_SVO);
                Shader_SVOMipmapper.SetBuffer(0, "_SVO_Counters", SVO_Tree.Buffer_Counters);
                Shader_SVOMipmapper.SetBuffer(0, "_SVO_MipmapQueue", SVO_Tree.Buffer_Queue_Mipmap);

                // Set values
                Shader_SVOMipmapper.SetInt("_debugFiltering", Debug_Filtering ? 1 : 0);

                // Dispatch
                Shader_SVOMipmapper.Dispatch(0, queueLength / 8, 1, 1);

                // Resume thread
                SVO_Tree.ResumeMipmapWorker();

                // We're done here
                return true;
            }
            else
            {
                // Resume thread
                SVO_Tree.ResumeMipmapWorker();

                // We're done here
                return false;
            }
        }



        /// <summary>
        /// Destructor
        /// </summary>
        public void Dispose()
        {

        }

    }
}
