/// <summary>
/// NKLI     : Nigiri - SVO
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NKLI.Nigiri.SVO
{
    /// <summary>
    /// Builds static sparse voxel octree from Morton ordered buffer
    /// </summary>
    #region Spase voxel builder
    public class Tree : ScriptableObject, IDisposable
    {
        // Read-only properties
        //public uint ThreadCount { get; private set; }
        //public uint VoxelCount { get; private set; }
        //public uint NodeCount { get; private set; }
        //public uint TreeDepth { get; private set; }
        public uint[] Counters { get; private set; }
        // [0] Max depth
        // [1] Max split queue items
        // [2] Current split queue items

        // Offset into counters buffer that boundary offsets begin
        public static readonly int boundariesOffset = 9;
        public static readonly uint boundariesOffsetU = 9;

        // Buffers
        public ComputeBuffer Buffer_SVO;
        public ComputeBuffer Buffer_Counters;
        public ComputeBuffer Buffer_SplitQueue;

        // static consts
        public static readonly uint maxDepth = 8;
        public static readonly int maxNodes = 128;
        public static readonly uint split_QueueLength = 10;

        // Compute
        ComputeShader shader_SVOBuilder;

        // Constructor
        public void Create()
        {
            // Load shader
            shader_SVOBuilder = Resources.Load("NKLI_Nigiri_SVOBuilder") as ComputeShader;

            // Calculate depth
            //TreeDepth = SVOHelper.GetDepth(gridWidth);

            // Calculate threadcount and depth index boundaries
            //ThreadCount = SVOHelper.GetThreadCount(gridWidth, TreeDepth, out uint[] Counters_boundaries);
            //NodeCount = SVOHelper.GetNodeCount(occupiedVoxels, gridWidth, TreeDepth);
            //int dispatchCount = Convert.ToInt32(ThreadCount);
            //int maxVoxels = Convert.ToInt32(gridWidth * gridWidth * gridWidth);

            // Assign instance variables
            //VoxelCount = occupiedVoxels;
            //NodeCount = SVOHelper.GetNodeCount(occupiedVoxels, gridWidth, TreeDepth) * 2;


            // Temporary PTR storage buffer
            //ComputeBuffer buffer_PTR = new ComputeBuffer(maxVoxels * 2, sizeof(uint), ComputeBufferType.Default);

            // Output buffer to contain final SVO
            Buffer_SVO = new ComputeBuffer(maxNodes, sizeof(uint) * 8, ComputeBufferType.Default);

            // Synchronisation counter buffer
            Buffer_Counters = new ComputeBuffer(boundariesOffset, sizeof(uint), ComputeBufferType.Default);
                
            // Temporary PTR storage buffer
            Buffer_SplitQueue = new ComputeBuffer(Convert.ToInt32(split_QueueLength), sizeof(uint), ComputeBufferType.Default);

            // Set buffer variables
            Counters = new uint[boundariesOffset];
            Counters[0] = maxDepth;
            Counters[1] = split_QueueLength;
            //Counters_boundaries[0] = boundariesOffsetU;
            //Counters_boundaries[3] = (uint)((Math.Ceiling(buffer_PTR.count / 8.0d) * 8) / 8) - 8;
            //Counters_boundaries[6] = ThreadCount;
            //Counters_boundaries[7] = TreeDepth;

            // Send buffer to GPU
            Buffer_Counters.SetData(Counters);

            // Send root node to GPU
            SVONode rootNode = new SVONode(0, 0);
            List<SVONode> nodeList = new List<SVONode>(1)
            {
                rootNode
            };
            Buffer_SVO.SetData(nodeList, 0, 0, 1);

            // Assign to compute
            shader_SVOBuilder.SetBuffer(0, "buffer_Counters", Buffer_Counters);
            //shader_SVOBuilder.SetBuffer(0, "buffer_Morton", buffer_Morton);
            //shader_SVOBuilder.SetBuffer(0, "buffer_PTR", buffer_PTR);
            shader_SVOBuilder.SetBuffer(0, "buffer_SVO", Buffer_SVO);            

            // Dispatch compute
            //shader_SVOBuilder.Dispatch(0, dispatchCount, 1, 1);

            // Cleanup
            //buffer_PTR.Dispose();
        }

        // Syncronous 'async' readback for Unit Testing.
        public void SyncGPUReadback(out AsyncGPUReadbackRequest req_Counters, out AsyncGPUReadbackRequest req_SVO)
        {
            req_Counters = AsyncGPUReadback.Request(Buffer_Counters);
            req_SVO = AsyncGPUReadback.Request(Buffer_SVO);

            req_Counters.WaitForCompletion();
            req_SVO.WaitForCompletion();
        }

        /// <summary>
        /// Releases all buffers
        /// </summary>
        public void ReleaseBuffers()
        {
            ReleaseBufferRef(ref Buffer_SplitQueue);
            ReleaseBufferRef(ref Buffer_Counters);
            ReleaseBufferRef(ref Buffer_SVO);
        }

        /// <summary>
        /// Release a compute buffer
        /// </summary>
        /// <param name="cb"></param>
        public void ReleaseBufferRef(ref ComputeBuffer cb)
        {
            if (cb != null)
            {
                cb.Release();
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
                    ReleaseBuffers();
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
    #endregion
    //
      
}
