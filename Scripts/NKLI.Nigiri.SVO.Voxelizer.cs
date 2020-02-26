/// <summary>
/// NKLI     : Nigiri - SVO Voxelization
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

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

            // Load shader
            Shader_VoxelEncocder = Resources.Load("NKLI_Nigiri_SVOVoxelizer") as ComputeShader;

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



        /// <summary>
        /// Destructor
        /// </summary>
        public void Dispose()
        {

        }

    }
}
