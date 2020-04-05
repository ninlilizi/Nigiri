﻿/// <summary>
/// NKLI     : Nigiri - SVO
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using System.Collections.Generic;
using System.Threading;
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
        public long RAM_Usage { get; private set; } // RAM usage
        public long VRAM_Usage { get; private set; } // VRAM usage
        public int Buffer_SVO_ByteLength { get; private set; } // Byte length of buffer
        public int Buffer_SVO_Count { get; private set; } // Max possible nodes
        public uint MaxDepth { get; private set; } // Default starting depth TTL of the tree
        public uint SplitQueueMaxLength { get; private set; } // Max length of the spit queue
        public uint MipmapQueueMaxLength { get; private set; } // Max length of the spit queue
        public byte[] SplitQueueSparse { get; private set; } // Processed list of nodes to split
        public byte[] MipmapQueueSparse { get; private set; } // Processed list of nodes to split
        public int SplitQueueSparseCount { get; private set; } // Number of processed nodes
        public int MipmapQueueSparseCount { get; private set; } // Number of processed nodes
        public bool AbleToSplit { get; set; } // If there are nodes to split
        public bool AbleToMipmap { get; set; } // If there are nodes to mipmap

        private byte[] queue_NodeSplit;
        // Worker thread to preprocesses the split queue
        private Thread thread_SplitPreProcessor;
        // Does the preprocessor have work to do?
        private bool thread_SplitPreProcessor_HasWork;

        private byte[] queue_Mipmap;
        // Worker thread to preprocesses the split queue
        private Thread thread_MipmapPreProcessor;
        // Does the preprocessor have work to do?
        private bool thread_MipmapPreProcessor_HasWork;


        // Buffers
        public ComputeBuffer Buffer_SVO;
        public ComputeBuffer Buffer_Counters;
        public ComputeBuffer Buffer_Counters_Internal;
        public ComputeBuffer Buffer_Queue_Split;
        public ComputeBuffer Buffer_Queue_Mipmap;

        // Readback queue
        public Queue<AsyncGPUReadbackRequest> gPU_Requests_Buffer_Counters = new Queue<AsyncGPUReadbackRequest>();

        // Commandbuffer
        private CommandBuffer CB_Nigiri_SVO;

        // static consts
        public static readonly int Buffer_Counters_Count = 9;

        // Attached camera
        private Camera attachedCamera;

        // Constructor
        public void Create(Camera _camera, uint maxDepth, int maxNodes, uint splitQueueMaxLength, uint mipmapQueueMaxLength)
        {
            // Zero ram counters
            VRAM_Usage = 0;
            RAM_Usage = 0;

            // Set properties
            MaxDepth = maxDepth;

            // Rounds split queue length to nearest mul of 8 
            //  to match dispatch thread group size
            SplitQueueMaxLength =
                    Math.Max(Convert.ToUInt32(Math.Round(
                         (Convert.ToInt32(splitQueueMaxLength) / (double)8),
                         MidpointRounding.AwayFromZero
                     ) * 8), 8);

            // Rounds mipmap queue length to nearest mul of 8 
            //  to match dispatch thread group size
            MipmapQueueMaxLength =
                    Math.Max(Convert.ToUInt32(Math.Round(
                         (Convert.ToInt32(mipmapQueueMaxLength) / (double)8),
                         MidpointRounding.AwayFromZero
                     ) * 8), 8);

            // Output buffer to contain final SVO
            Buffer_SVO = new ComputeBuffer(maxNodes, sizeof(uint) * 4, ComputeBufferType.Default);
            Buffer_SVO_ByteLength = maxNodes * sizeof(uint) * 4;
            Buffer_SVO_Count = maxNodes;
            VRAM_Usage += Convert.ToInt64(maxNodes) * sizeof(uint) * 4 * 8;

            // Synchronisation counter buffer
            Buffer_Counters = new ComputeBuffer(Buffer_Counters_Count, sizeof(uint), ComputeBufferType.Default);
            VRAM_Usage += Convert.ToInt64(Buffer_Counters_Count) * sizeof(uint) * 8;

            // Internal position counter buffer
            Buffer_Counters_Internal = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Default);
            VRAM_Usage += 1 * sizeof(uint) * 8;

            // Temporary PTR storage buffer - Split queue
            Buffer_Queue_Split = new ComputeBuffer(Convert.ToInt32(SplitQueueMaxLength), sizeof(uint), ComputeBufferType.Default);
            VRAM_Usage += Convert.ToInt64(SplitQueueMaxLength) * sizeof(uint) * 8;

            // Temporary PTR storage buffer - Mipmap queue
            Buffer_Queue_Mipmap = new ComputeBuffer(Convert.ToInt32(MipmapQueueMaxLength), sizeof(uint), ComputeBufferType.Default);
            VRAM_Usage += Convert.ToInt64(MipmapQueueMaxLength) * sizeof(uint) * 8;

            // CPU readback storage for the split queue
            queue_NodeSplit = new byte[SplitQueueMaxLength * 4];
            RAM_Usage += queue_NodeSplit.LongLength * 8;

            // CPU readback storage for the mipmap queue
            queue_Mipmap = new byte[MipmapQueueMaxLength * 4];
            RAM_Usage += queue_Mipmap.LongLength * 8;

            // Processed split queue for feeding to compute
            SplitQueueSparse = new byte[SplitQueueMaxLength * 4];
            RAM_Usage += SplitQueueSparse.LongLength * 8;

            // Processed mipmap queue for feeding to compute
            MipmapQueueSparse = new byte[MipmapQueueMaxLength * 4];
            RAM_Usage += MipmapQueueSparse.LongLength * 8;

            // Set counter buffer
            SetCounterBuffer();

            // Set internal position counter buffer 
            // [0] SVO buffer write position
            uint[] Counters_Internal = new uint[1];
            Counters_Internal[0] = 1;
            Buffer_Counters_Internal.SetData(Counters_Internal);

            // Send root node to GPU
            SVONode rootNode = new SVONode(0, 0);
            rootNode.PackStruct(0, 0, maxDepth, false);
            List<SVONode> nodeList = new List<SVONode>(1)
            {
                rootNode
            };
            Buffer_SVO.SetData(nodeList, 0, 0, 1);

            if (_camera != null)
            {
                // Setup commandbuffer
                CB_Nigiri_SVO = new CommandBuffer
                {
                    name = "Nigiri Asynchronous SVO"
                };
                CB_Nigiri_SVO.RequestAsyncReadback(Buffer_Queue_Split, HandleSplitQueueReadback);
                CB_Nigiri_SVO.RequestAsyncReadback(Buffer_Queue_Mipmap, HandleMipmapQueueReadback);

                attachedCamera = _camera;
                attachedCamera.AddCommandBuffer(CameraEvent.AfterEverything, CB_Nigiri_SVO);

                // Start worker threads
                thread_SplitPreProcessor = new Thread(ThreadedNodeSplitPreProcessor);
                thread_MipmapPreProcessor = new Thread(ThreadedNodeMipmapPreProcessor);
                thread_SplitPreProcessor.Start();
                thread_MipmapPreProcessor.Start();
            }
        }

        /// <summary>
        /// Handles completed async readback of split queue buffer
        /// </summary>
        /// <param name="obj"></param>
        private void HandleSplitQueueReadback(AsyncGPUReadbackRequest obj)
        {
            if (obj.hasError) Debug.Log("SVO split queue readback error");
            else if (obj.done)
            {
                obj.GetData<byte>().CopyTo(queue_NodeSplit);
            }

            // Tell the thread there's work to do
            thread_SplitPreProcessor_HasWork = true;
        }

        /// <summary>
        /// Handles completed async readback of mipmap queue buffer
        /// </summary>
        /// <param name="obj"></param>
        private void HandleMipmapQueueReadback(AsyncGPUReadbackRequest obj)
        {
            if (obj.hasError) Debug.Log("SVO mipmap queue readback error");
            else if (obj.done)
            {
                obj.GetData<byte>().CopyTo(queue_Mipmap);
            }

            // Tell the thread there's work to do
            thread_MipmapPreProcessor_HasWork = true;
        }

        /// <summary>
        /// Worker thread - Preprocesses the node split queue for later dispatch
        /// </summary>
        private void ThreadedNodeSplitPreProcessor()
        {
            // Permanent worker
            while (true)
            {
                // Only check for work every 4ms
                Thread.Sleep(1);

                try
                {
                    if (thread_SplitPreProcessor_HasWork)
                    {
                        // Process the node split queue to remove duplicates
                        SplitQueueSparse = DeDupeUintByteArray(queue_NodeSplit, out bool contentsFound, out int sparseCount);

                        // Allow nodes to be split if applicable
                        AbleToSplit = contentsFound;

                        // Store count to detemine GPU thread count later
                        SplitQueueSparseCount = sparseCount;

                        // We're done here
                        thread_SplitPreProcessor_HasWork = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("<Nigiri> Exception occured in the SVO split queue preprocessor thread!" + Environment.NewLine + ex);
                }
            }
        }

        /// <summary>
        /// Worker thread - Preprocesses the node mipmap queue for later dispatch
        /// </summary>
        private void ThreadedNodeMipmapPreProcessor()
        {
            // Permanent worker
            while (true)
            {
                // Only check for work every 4ms
                Thread.Sleep(1);

                try
                {
                    if (thread_MipmapPreProcessor_HasWork)
                    {
                        // Process the node split queue to remove duplicates
                        MipmapQueueSparse = DeDupeUintByteArray(queue_Mipmap, out bool contentsFound, out int sparseCount);

                        // Allow nodes to be split if applicable
                        AbleToMipmap = contentsFound;

                        // Store count to detemine GPU thread count later
                        MipmapQueueSparseCount = sparseCount;

                        // We're done here
                        thread_MipmapPreProcessor_HasWork = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("<Nigiri> Exception occured in the SVO mipmap queue preprocessor thread!" + Environment.NewLine + ex);
                }
            }
        }


        /// <summary>
        /// Set custom split queue
        /// </summary>
        /// <param name="_splitQueue"></param>
        public void SetSplitQueue(byte[] _splitQueue)
        {
            queue_NodeSplit = _splitQueue;

            // Tell the thread there's work to do
            thread_SplitPreProcessor_HasWork = true;
        }

        /// <summary>
        /// Set custom mipmap queue
        /// </summary>
        /// <param name="_splitQueue"></param>
        public void SetMipMapQueue(byte[] _mipmapQueue)
        {
            queue_Mipmap = _mipmapQueue;

            // Tell the thread there's work to do
            thread_MipmapPreProcessor_HasWork = true;
        }

        /// <summary> 
        /// Removes duplicates from a byte array of 32bit uint values 
        /// </summary> 
        /// <param name="targetArray"></param> 
        /// <param name="contentsFound"></param> 
        /// <returns></returns> 
        private byte[] DeDupeUintByteArray(byte[] targetArray, out bool contentsFound, out int sparseCount)
        {
            // Copy split queue to hashset to remove dupes
            HashSet<UInt32> arraySet = new HashSet<uint>();
            for (int i = 0; i < (targetArray.Length / 4); i++)
            {
                byte[] queueByte = new byte[4];
                Buffer.BlockCopy(targetArray, (i * 4), queueByte, 0, 4);
                uint queueValue = BitConverter.ToUInt32(queueByte, 0);
                if (queueValue != 0)  arraySet.Add(queueValue);
            }

            // Keep track of how many sparse nodes in the queue
            sparseCount = arraySet.Count;

            if (sparseCount > 0)
            {
                // Copy hashset back to array
                int queueWriteBackIndex = 0;
                HashSet<uint>.Enumerator queueEnum = arraySet.GetEnumerator();
                while (queueEnum.MoveNext())
                {
                    byte[] queueByte = new byte[4];
                    queueByte = BitConverter.GetBytes(queueEnum.Current);
                    Buffer.BlockCopy(queueByte, 0, targetArray, queueWriteBackIndex * 4, 4);
                    queueWriteBackIndex++;
                }

                // Zeros rest of array
                int queueStartIndex = arraySet.Count * 4;
                Array.Clear(targetArray, queueStartIndex, (targetArray.Length - queueStartIndex));

                // There's work to do!
                contentsFound = true;
            }
            else
            {
                // Zeros entire array
                int queueStartIndex = arraySet.Count * 4;
                Array.Clear(targetArray, 0, targetArray.Length);

                // Nothing to do.
                contentsFound = false;
            }

            // We're done here
            return targetArray;
        }

        /// <summary>
        /// Sets counter buffer initial values and sends to GPU
        /// </summary>
        public void SetCounterBuffer()
        {
            // [0] Max depth
            // [1] Max split queue items
            // [2] Current split queue items
            // [3] Split queue position counter
            // [4] Mask buffer position
            // [5] Max mipmap queue items
            // [6] Current mipmap queue items
            // [7] Mipmap queue position counter

            // Set buffer variables
            uint[] Counters = new uint[Buffer_Counters_Count];
            Counters[0] = MaxDepth;
            Counters[1] = SplitQueueMaxLength;
            Counters[2] = 0;
            Counters[3] = 0;
            Counters[4] = 0;
            Counters[5] = 0;
            Counters[6] = 0;

            // Send buffer to GPU
            Buffer_Counters.SetData(Counters);
        }

        // Syncronous 'async' readback for Unit Testing.
        public void SyncGPUReadback(
            out Queue<AsyncGPUReadbackRequest> queue_Counters,
            out Queue<AsyncGPUReadbackRequest> queue_Counters_Internal,
            out Queue<AsyncGPUReadbackRequest> queue_SVO, 
            out Queue<AsyncGPUReadbackRequest> queue_SplitQueue)
            
        {
            queue_Counters = new Queue<AsyncGPUReadbackRequest>();
            queue_Counters_Internal = new Queue<AsyncGPUReadbackRequest>();
            queue_SVO = new Queue<AsyncGPUReadbackRequest>();
            queue_SplitQueue = new Queue<AsyncGPUReadbackRequest>();

            queue_Counters.Enqueue(AsyncGPUReadback.Request(Buffer_Counters));
            queue_Counters_Internal.Enqueue(AsyncGPUReadback.Request(Buffer_Counters_Internal));
            queue_SVO.Enqueue(AsyncGPUReadback.Request(Buffer_SVO));
            queue_SplitQueue.Enqueue(AsyncGPUReadback.Request(Buffer_Queue_Split));


            var req_Counters = queue_Counters.Peek();
            var req_Counters_Internal = queue_Counters_Internal.Peek();
            var req_SVO = queue_SVO.Peek();
            var req_SplitQueue = queue_SplitQueue.Peek();

            req_Counters.WaitForCompletion();
            req_Counters_Internal.WaitForCompletion();
            req_SVO.WaitForCompletion();
            req_SplitQueue.WaitForCompletion();
        }

        /// <summary>
        /// Releases all buffers
        /// </summary>
        public void ReleaseBuffers()
        {
            Nigiri.Helpers.ReleaseBufferRef(ref Buffer_Queue_Split);
            Nigiri.Helpers.ReleaseBufferRef(ref Buffer_Queue_Mipmap);
            Nigiri.Helpers.ReleaseBufferRef(ref Buffer_Counters);
            Nigiri.Helpers.ReleaseBufferRef(ref Buffer_Counters_Internal);
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
                    // Stop worker threads
                    if (thread_SplitPreProcessor != null) thread_SplitPreProcessor.Abort();
                    if (thread_MipmapPreProcessor != null) thread_MipmapPreProcessor.Abort();

                    // Attempt to dispose any existing buffers
                    ReleaseBuffers();

                    // Remove command buffer from camera
                    if (attachedCamera != null) attachedCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, CB_Nigiri_SVO);
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
