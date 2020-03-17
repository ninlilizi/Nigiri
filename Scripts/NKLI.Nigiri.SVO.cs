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
        public int Buffer_SVO_ByteLength { get; private set; }
        public int Buffer_SVO_Count { get; private set; }
        public uint MaxDepth { get; private set; }
        public uint SplitQueueMaxLength { get; private set; }

        // Buffers
        public ComputeBuffer Buffer_SVO;
        public ComputeBuffer Buffer_Counters;
        public ComputeBuffer Buffer_SplitQueue;

        // static consts
        //public static readonly uint maxDepth = 8;
        //public static readonly int maxNodes = 128;
        //public static readonly uint split_MaxQueueLength = 10;
        public static readonly int Buffer_Counters_Count = 9;

        // Compute
        ComputeShader shader_SVOBuilder;

        // Constructor
        public void Create(uint maxDepth, int maxNodes, uint splitQueueMaxLength)
        {
            // Set properties
            MaxDepth = maxDepth;
            SplitQueueMaxLength = splitQueueMaxLength;

            // Load shader
            shader_SVOBuilder = Resources.Load("NKLI_Nigiri_SVOBuilder") as ComputeShader;

            // Output buffer to contain final SVO
            Buffer_SVO = new ComputeBuffer(maxNodes, sizeof(uint) * 8, ComputeBufferType.Default);
            Buffer_SVO_ByteLength = maxNodes * sizeof(uint) * 8;
            Buffer_SVO_Count = maxNodes;

            // Synchronisation counter buffer
            Buffer_Counters = new ComputeBuffer(Buffer_Counters_Count, sizeof(uint), ComputeBufferType.Default);
                
            // Temporary PTR storage buffer
            Buffer_SplitQueue = new ComputeBuffer(Convert.ToInt32(SplitQueueMaxLength), sizeof(uint), ComputeBufferType.Default);

            // Set counter buffer
            SetCounterBuffer();

            // Send root node to GPU
            SVONode rootNode = new SVONode(0, 0);
            List<SVONode> nodeList = new List<SVONode>(1)
            {
                rootNode
            };
            Buffer_SVO.SetData(nodeList, 0, 0, 1);

            // Assign to compute
            shader_SVOBuilder.SetBuffer(0, "buffer_Counters", Buffer_Counters);
            shader_SVOBuilder.SetBuffer(0, "buffer_SVO", Buffer_SVO);            


        }

        /// <summary>
        /// Sets counter buffer initial values and sends to GPU
        /// </summary>
        public void SetCounterBuffer()
        {
            // [0] Max depth
            // [1] Max split queue items
            // [2] Current split queue items

            // Set buffer variables
            uint[] Counters = new uint[Buffer_Counters_Count];
            Counters[0] = MaxDepth;
            Counters[1] = SplitQueueMaxLength;
            Counters[2] = 0;

            // Send buffer to GPU
            Buffer_Counters.SetData(Counters);
        }

        // Syncronous 'async' readback for Unit Testing.
        public void SyncGPUReadback(
            out AsyncGPUReadbackRequest req_Counters, 
            out AsyncGPUReadbackRequest req_SVO, 
            out AsyncGPUReadbackRequest req_SplitQueue)
        {
            req_Counters = AsyncGPUReadback.Request(Buffer_Counters);
            req_SVO = AsyncGPUReadback.Request(Buffer_SVO);
            req_SplitQueue = AsyncGPUReadback.Request(Buffer_SplitQueue);

            req_Counters.WaitForCompletion();
            req_SVO.WaitForCompletion();
            req_SplitQueue.WaitForCompletion();
        }

        /// <summary>
        /// Releases all buffers
        /// </summary>
        public void ReleaseBuffers()
        {
            Nigiri.Helpers.ReleaseBufferRef(ref Buffer_SplitQueue);
            Nigiri.Helpers.ReleaseBufferRef(ref Buffer_Counters);
            Nigiri.Helpers.ReleaseBufferRef(ref Buffer_SVO);
        }

        #region IDisposable + Unity Scriped Destruction support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Attempt to dispose any existing buffers
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

        ~Tree()
        {
            Dispose();
        }
        #endregion
    }
    #endregion
    //
      
}
