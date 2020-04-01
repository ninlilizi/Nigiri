/// <summary>
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
        public int Buffer_SVO_ByteLength { get; private set; } // Byte length of buffer
        public int Buffer_SVO_Count { get; private set; } // Max possible nodes
        public uint MaxDepth { get; private set; } // Default starting depth TTL of the tree
        public uint SplitQueueMaxLength { get; private set; } // Max length of the spit queue
        public byte[] SplitQueueSparse { get; private set; } // Processed list of nodes to split
        public int SplitQueueSparseCount { get; private set; } // Number of processed nodes
        public bool AbleToSplit { get; set; } // If there are nodes to split

        // GPU readback storage of the split queue
        private byte[] splitQueue;
        //private int sparseNodesTosplit;


        // Buffers
        public ComputeBuffer Buffer_SVO;
        public ComputeBuffer Buffer_Counters;
        public ComputeBuffer Buffer_Counters_Internal;
        public ComputeBuffer Buffer_SplitQueue;

        // Readback queue
        public Queue<AsyncGPUReadbackRequest> gPU_Requests_Buffer_Counters = new Queue<AsyncGPUReadbackRequest>();

        // Commandbuffer
        private CommandBuffer CB_Nigiri_SVO;

        // static consts
        //public static readonly uint maxDepth = 8;
        //public static readonly int maxNodes = 128;
        //public static readonly uint split_MaxQueueLength = 10;
        public static readonly int Buffer_Counters_Count = 9;

        // Compute
        ComputeShader shader_SVOBuilder;

        // Attached camera
        private Camera attachedCamera;

        // Constructor
        public void Create(Camera _camera, uint maxDepth, int maxNodes, uint splitQueueMaxLength)
        {
            // Set properties
            MaxDepth = maxDepth;

            // Rounds split queue length to nearest mul of 8 
            //  to match dispatch thread group size
            int factor = 8;
            SplitQueueMaxLength =
                    Math.Max(Convert.ToUInt32(Math.Round(
                         (Convert.ToInt32(splitQueueMaxLength) / (double)factor),
                         MidpointRounding.AwayFromZero
                     ) * factor), 8);


            // Output buffer to contain final SVO
            Buffer_SVO = new ComputeBuffer(maxNodes, sizeof(uint) * 8, ComputeBufferType.Default);
            Buffer_SVO_ByteLength = maxNodes * sizeof(uint) * 8;
            Buffer_SVO_Count = maxNodes;

            // Synchronisation counter buffer
            Buffer_Counters = new ComputeBuffer(Buffer_Counters_Count, sizeof(uint), ComputeBufferType.Default);

            // Internal position counter buffer
            Buffer_Counters_Internal = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Default);

            // Temporary PTR storage buffer
            Buffer_SplitQueue = new ComputeBuffer(Convert.ToInt32(SplitQueueMaxLength), sizeof(uint), ComputeBufferType.Default);

            // CPU readback storage for the split queue
            splitQueue = new byte[SplitQueueMaxLength * 4];

            // Processed split queue for feeding to compute
            SplitQueueSparse = new byte[SplitQueueMaxLength * 4];

            // Target array for async counters readback
            //Counters = new uint[Buffer_Counters_Count];

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

            // Setup commandbuffer
            CB_Nigiri_SVO = new CommandBuffer
            {
                name = "Nigiri Asynchronous SVO"
            };
            //CB_Nigiri_SVO.RequestAsyncReadback(Buffer_Counters, HandleCountersReadback);
            CB_Nigiri_SVO.RequestAsyncReadback(Buffer_SplitQueue, HandleSplitQueueReadback);
            
            attachedCamera = _camera;
            attachedCamera.AddCommandBuffer(CameraEvent.AfterEverything, CB_Nigiri_SVO);

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
                obj.GetData<byte>().CopyTo(splitQueue);
            }

            Thread workerThread = new Thread(ThreadedNodeSplit);
            workerThread.Start();
        }

        private void ThreadedNodeSplit()
        {
            SplitQueueSparse = DeDupeUintByteArray(splitQueue, out bool contentsFound);
            AbleToSplit = contentsFound;
        }

        private byte[] DeDupeUintByteArray(byte[] targetArray, out bool contentsFound)
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
            SplitQueueSparseCount = arraySet.Count;

            if (SplitQueueSparseCount > 0)
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

        /*/// <summary>
        /// Handles completed async readback of counter buffer
        /// </summary>
        /// <param name="obj"></param>
        private void HandleCountersReadback(AsyncGPUReadbackRequest obj)
        {
            if (obj.hasError) Debug.Log("SVO GPU Counter readback error");
            else if (obj.done)
            {
                obj.GetData<uint>().CopyTo(Counters);
            }
        }*/

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

            // Set buffer variables
            uint[] Counters = new uint[Buffer_Counters_Count];
            Counters[0] = MaxDepth;
            Counters[1] = SplitQueueMaxLength;
            Counters[2] = 0;
            Counters[3] = 0;
            Counters[4] = 0;

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
            queue_SplitQueue.Enqueue(AsyncGPUReadback.Request(Buffer_SplitQueue));


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
            Nigiri.Helpers.ReleaseBufferRef(ref Buffer_SplitQueue);
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
                    // Attempt to dispose any existing buffers
                    ReleaseBuffers();

                    // Remove command buffer from camera
                    attachedCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, CB_Nigiri_SVO);
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
