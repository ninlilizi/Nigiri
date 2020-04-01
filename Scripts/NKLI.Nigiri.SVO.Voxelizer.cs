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
        public int Max_Depth { get; private set; }

        // Read-only buffer properties

        // Compute
        readonly private ComputeShader Shader_VoxelEncocder;
        readonly private ComputeShader Shader_SVOSplitter;

        /// <summary>
        /// Constructor
        /// </summary>
        public Voxelizer(Tree SVO, float emissiveIntensity, float shadowStrength, float occlusionGain, float giAreaSize, int maxDepth)
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
            Shader_VoxelEncocder = Resources.Load("NKLI_Nigiri_SVOVoxelizer") as ComputeShader;
            if (Shader_VoxelEncocder == null) throw new Exception("[Nigiri] failed to load compute shader 'NKLI_Nigiri_SVOVoxelizer'");

            // Load splitter shader
            Shader_SVOSplitter = Resources.Load("NKLI_Nigiri_SVOSplitter") as ComputeShader;
            if (Shader_SVOSplitter == null) throw new Exception("[Nigiri] failed to load compute shader 'NKLI_Nigiri_SVOSplitter'");


            // Binds to SVO
            SVO_Tree = SVO;

            // Sets values
            Emissive_Intensity = emissiveIntensity;
            Shadow_Strength = shadowStrength;
            Occlusion_Gain = occlusionGain;
            GI_Area_Size = giAreaSize;
            Max_Depth = maxDepth;

        }

        /// <summary>
        /// Voxelizes the scene
        /// </summary>
        public bool VoxelizeScene(int sampleCount, RenderTexture positionTexture, RenderTexture lightingTexture, RenderTexture lightingTexture2, ComputeBuffer maskBuffer)
        {
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

            // Set counter buffer to initial values
            SVO_Tree.SetCounterBuffer();


            // Set buffers
            Shader_VoxelEncocder.SetBuffer(0, "_SVO", SVO_Tree.Buffer_SVO);
            Shader_VoxelEncocder.SetBuffer(0, "_SVO_Counters", SVO_Tree.Buffer_Counters);
            Shader_VoxelEncocder.SetBuffer(0, "_SVO_SplitQueue", SVO_Tree.Buffer_SplitQueue);
            Shader_VoxelEncocder.SetBuffer(0, "_maskBuffer", maskBuffer);

            // Set textures
            Shader_VoxelEncocder.SetTexture(0, "positionTexture", positionTexture);
            Shader_VoxelEncocder.SetTexture(0, "lightingTexture", lightingTexture);
            Shader_VoxelEncocder.SetTexture(0, "lightingTexture2", lightingTexture2);

            // Set values
            Shader_VoxelEncocder.SetFloat("_emissiveIntensity", Emissive_Intensity);
            Shader_VoxelEncocder.SetFloat("_shadowStrength", Shadow_Strength);
            Shader_VoxelEncocder.SetFloat("_occlusionGain", Occlusion_Gain);
            Shader_VoxelEncocder.SetFloat("_giAreaSize", GI_Area_Size);
            Shader_VoxelEncocder.SetInt("_maxDepth", Max_Depth);

            // Dispatch
            Shader_VoxelEncocder.Dispatch(0, sampleCount / 16, 1, 1);

            // We're done here
            return true;
        }

        public bool SplitNodes()
        {
            // Only if a successful readback has been completed and flagged for action
            if (SVO_Tree.AbleToSplit)
            {
                // Send buffer to GPU
                SVO_Tree.Buffer_SplitQueue.SetData(SVO_Tree.SplitQueueSparse);

                // Rounds split queue length to nearest mul of 8 
                //  to match dispatch thread group size
                int queueLength = ((SVO_Tree.SplitQueueSparseCount + 8 - (8 / Math.Abs(8))) / 8) * 8;

                // Set buffers
                Shader_SVOSplitter.SetBuffer(0, "_SVO", SVO_Tree.Buffer_SVO);
                Shader_SVOSplitter.SetBuffer(0, "_SVO_Counters", SVO_Tree.Buffer_Counters);
                Shader_SVOSplitter.SetBuffer(0, "_SVO_Counters_Internal", SVO_Tree.Buffer_Counters_Internal);
                Shader_SVOSplitter.SetBuffer(0, "_SVO_SplitQueue", SVO_Tree.Buffer_SplitQueue);

                // Set values
                Shader_SVOSplitter.SetFloat("_SVO_MaxNodes", SVO_Tree.Buffer_SVO_Count);

                // Dispatch
                Shader_SVOSplitter.Dispatch(0, queueLength / 8, 1, 1);

                // We don't want to run again till there is something to do
                SVO_Tree.AbleToSplit = false;

                // We're done here
                return true;
            }
            else return false;
        }



        /// <summary>
        /// Destructor
        /// </summary>
        public void Dispose()
        {

        }

    }
}
