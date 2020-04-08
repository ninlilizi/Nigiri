/// <summary>
/// NKLI     : Nigiri - SVO
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

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
        public int SplitQueueMaxLength { get; private set; } // Max length of the spit queue
        public int MipmapQueueMaxLength { get; private set; } // Max length of the mipmap queue
        public int SplitQueueSparseCount { get; private set; } // Number of processed nodes
        public int MipmapQueueSparseCount { get; private set; } // Number of processed nodes
        public bool AbleToSplit { get; set; } // If there are nodes to split
        public bool AbleToMipmap { get; set; } // If there are nodes to mipmap
        public double Runtime_Thread_Split { get; private set; } // Execution time of node split worker thread
        public double Runtime_Thread_Mipmap { get; private set; } // Execution time of node mipmap worker thread

        // Node split buffer copied from GPU
        private byte[] queue_Split;
        // Sparsified split buffer for copying back to GPU
        public byte[] queue_Split_Sparse;
        // Worker thread to preprocesses the split queue
        private Thread thread_SplitPreProcessor;
        //true makes the thread start as "running", false makes it wait on _event.Set()
        private ManualResetEvent thread_SplitPreProcessor_Scaling_Event = new ManualResetEvent(true);
        // Does the preprocessor have work to do?
        private ManualResetEvent thread_SplitPreProcessor_HasWork_Event = new ManualResetEvent(false);
        // Is the processor waiting for output data to be processed?
        public ManualResetEvent thread_SplitPreProcessor_AwaitingAction_Event = new ManualResetEvent(true);

        // Mipmap buffer copied from GPU
        private byte[] queue_Mipmap;
        // Sparsified mipmap buffer for copying back to GPU
        public List<uint> queue_Mipmap_Sparse;
        // Job storage
        private byte[] queue_Mipmap_Workingset;
        // Worker thread to preprocesses the split queue
        private Thread thread_MipmapPreProcessor;
        //true makes the thread start as "running", false makes it wait on _event.Set()
        private ManualResetEvent thread_MipmapPreProcessor_Scaling_Event = new ManualResetEvent(true);
        // Does the preprocessor have work to do?
        private ManualResetEvent thread_MipmapPreProcessor_HasWork_Event = new ManualResetEvent(false);
        // Is the processor waiting for output data to be processed?
        public ManualResetEvent thread_MipmapPreProcessor_AwaitingAction_Event = new ManualResetEvent(true);


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
        private static readonly int Buffer_Counters_Count = 8;
        private static readonly int initial_SplitQueueMaxLength = 250000;

        // Attached camera
        private Camera attachedCamera;

        // Main thread dispatcher
        Tools.MainThreadDispatcher threadDispatch;
        GameObject threadDispatchObject;

        // Performance stopwatches
        private Stopwatch stopwatch_Thread_Split;
        private Stopwatch stopwatch_Thread_Mipmap;

        // Constructor
        public void Create(Camera _camera, uint maxDepth, int maxNodes, uint mipmapQueueMaxLength)
        {
            // Zero ram counters
            VRAM_Usage = 0;
            RAM_Usage = 0;

            // Set properties
            MaxDepth = maxDepth;

            // Rounds split queue length to nearest mul of 8 
            //  to match dispatch thread group size
            SplitQueueMaxLength =
                    Math.Max(Convert.ToInt32(Math.Round(
                         (initial_SplitQueueMaxLength / (double)8),
                         MidpointRounding.AwayFromZero
                     ) * 8), 8);

            // Rounds mipmap queue length to nearest mul of 8 
            //  to match dispatch thread group size
            MipmapQueueMaxLength =
                    Math.Max(Convert.ToInt32(Math.Round(
                         (mipmapQueueMaxLength / (double)8),
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
            queue_Split = new byte[SplitQueueMaxLength * 4];
            RAM_Usage += queue_Split.LongLength * 8;

            // Processed split queue for feeding to compute
            queue_Split_Sparse = new byte[SplitQueueMaxLength * 4];
            RAM_Usage += queue_Split_Sparse.LongLength * 8;

            // CPU readback storage for the mipmap queue
            queue_Mipmap = new byte[MipmapQueueMaxLength * 4];
            RAM_Usage += queue_Mipmap.LongLength * 8;

            // CPU readback storage for the mipmap queue
            queue_Mipmap_Workingset = new byte[MipmapQueueMaxLength * 4];
            RAM_Usage += queue_Mipmap_Workingset.LongLength * 8;

            // Processed mipmap queue for feeding to compute
            queue_Mipmap_Sparse = new List<uint>();

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
                BuildCommandBuffer();

                attachedCamera = _camera;
                attachedCamera.AddCommandBuffer(CameraEvent.AfterEverything, CB_Nigiri_SVO);

                // Start worker threads
                thread_SplitPreProcessor = new Thread(ThreadedSplitPreProcessor);
                thread_MipmapPreProcessor = new Thread(ThreadedMipmapPreProcessor);
                thread_SplitPreProcessor.Start();
                thread_MipmapPreProcessor.Start();
            }

            CreateThreadDispatcher();
        }

        private void BuildCommandBuffer()
        {
            // Instantiate if null
            if (CB_Nigiri_SVO == null)
            {
                CB_Nigiri_SVO = new CommandBuffer
                {
                    name = "Nigiri Asynchronous SVO"
                };
            }

            // Clear any existing contents
            CB_Nigiri_SVO.Clear();

            // Add commands
            CB_Nigiri_SVO.RequestAsyncReadback(Buffer_Queue_Split, HandleSplitQueueReadback);
            CB_Nigiri_SVO.RequestAsyncReadback(Buffer_Queue_Mipmap, HandleMipmapQueueReadback);
        }

        private void CreateThreadDispatcher()
        {
            // Destroy stale objects
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.transform.name == "SVO_THREADDISPATCH_fc41258a")
                {
                    DestroyImmediate(obj);
                }
            }
            threadDispatch = null;
            threadDispatchObject = null;

            // Create new object
            threadDispatchObject = new GameObject("SVO_THREADDISPATCH_fc41258a");
            threadDispatchObject.transform.parent = attachedCamera.transform;
            threadDispatchObject.hideFlags = HideFlags.HideAndDontSave;
            threadDispatch = threadDispatchObject.AddComponent<Tools.MainThreadDispatcher>();
        }


        /// <summary>
        /// Handles completed async readback of split queue buffer
        /// </summary>
        /// <param name="obj"></param>
        private void HandleSplitQueueReadback(AsyncGPUReadbackRequest obj)
        {
            // Check for successful readback
            if (obj.hasError) Debug.Log("SVO split queue readback error");
            else if (obj.done)
            {
                // Sanity check to avoid race-condition with performance auto-scaling
                if (obj.GetData<byte>().Length == queue_Split.Length)
                {
                    // Copy data to local array
                    obj.GetData<byte>().CopyTo(queue_Split);

                    // Tell the thread there's work to do
                    thread_SplitPreProcessor_HasWork_Event.Set();
                }
            }
        }

        /// <summary>
        /// Handles completed async readback of mipmap queue buffer
        /// </summary>
        /// <param name="obj"></param>
        private void HandleMipmapQueueReadback(AsyncGPUReadbackRequest obj)
        {
            // Check for successful readback
            if (obj.hasError) Debug.Log("SVO mipmap queue readback error");
            else if (obj.done)
            {
                // Sanity check to avoid race-condition with performance auto-scaling
                if (obj.GetData<byte>().Length == queue_Mipmap.Length)
                {
                    obj.GetData<byte>().CopyTo(queue_Mipmap);

                    // Tell the thread there's work to do
                    thread_MipmapPreProcessor_HasWork_Event.Set();
                }
            }
        }

        /// <summary>
        /// Worker thread - Preprocesses the node split queue for later dispatch
        /// </summary>
        private void ThreadedSplitPreProcessor()
        {
            // Instantiate execution runtime stopwatch
            stopwatch_Thread_Split = new Stopwatch();

            // Permanent worker
            while (true)
            {
                // Wait if thread locked by active performance scaling
                thread_SplitPreProcessor_AwaitingAction_Event.WaitOne(1000);

                // Wait till thread is unlocked for available work
                thread_SplitPreProcessor_HasWork_Event.WaitOne(Timeout.Infinite);

                // Wait if thread locked by active performance scaling
                thread_SplitPreProcessor_Scaling_Event.WaitOne(Timeout.Infinite);

                try
                {
                    // Start execution runtime stopwatch
                    stopwatch_Thread_Split.Start();

                    // Process the node split queue to remove duplicates
                    queue_Split_Sparse = DeDupeUintByteArray(queue_Split, false, out bool contentsFound, out int count, out int sparseCount);

                    // Allow nodes to be split if applicable
                    AbleToSplit = contentsFound;

                    // Store count to detemine GPU thread count later
                    SplitQueueSparseCount = sparseCount;

                    // Update thread execution time counter
                    stopwatch_Thread_Split.Stop();
                    Runtime_Thread_Split = stopwatch_Thread_Split.Elapsed.TotalMilliseconds;
                    stopwatch_Thread_Split.Reset();

                    bool resize = false;
                    // If thread execution exceeds 15ms
                    if (Runtime_Thread_Split > 14)
                    {
                        resize = true;
                    }
                    // If queue exceeds 50% of the total buffer but below CPU budget
                    else if (Runtime_Thread_Split < 4)
                    {
                        if (count >= (SplitQueueMaxLength * 0.5)) resize = true;
                    }

                    // Resize buffers
                    if (resize)
                    {
                        // Suspend thread till after buffers have been resizes
                        thread_SplitPreProcessor_Scaling_Event.Reset();

                        // Calculate new optimal queue length and Resize buffers on the main thread
                        threadDispatch.Enqueue(() => SplitQueueMaxLength = Math.Max((int)(Convert.ToDouble(count) / Runtime_Thread_Split) * 10, 128));
                        threadDispatch.Enqueue(() => ResizeComputeBuffer(ref Buffer_Queue_Split, sizeof(uint), SplitQueueMaxLength));
                        threadDispatch.Enqueue(() => queue_Split = ResizeByteArray(queue_Split, sizeof(uint) * SplitQueueMaxLength));
                        threadDispatch.Enqueue(() => queue_Split_Sparse = ResizeByteArray(queue_Split_Sparse, sizeof(uint) * SplitQueueMaxLength));
                        threadDispatch.Enqueue(() => BuildCommandBuffer());
                        threadDispatch.Enqueue(() => thread_SplitPreProcessor_Scaling_Event.Set());

                        // Avoid mismatched buffer sizes
                        AbleToSplit = false;
                    }

                    // Suspends thread
                    thread_SplitPreProcessor_HasWork_Event.Reset();

                    // Suspend thread till output data is handled
                    if (AbleToSplit) thread_SplitPreProcessor_AwaitingAction_Event.Reset();
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("System.Threading.ThreadAbortException"))
                        Debug.LogWarning("<Nigiri> Exception occured in the SVO split queue preprocessor thread!" + Environment.NewLine + ex);
                }
            }
        }

        /// <summary>
        /// Worker thread - Preprocesses the node mipmap queue for later dispatch
        /// </summary>
        private void ThreadedMipmapPreProcessor()
        {
            // Instantiate execution runtime stopwatch
            stopwatch_Thread_Mipmap = new Stopwatch();

            bool hasWork = false;
            int job_MaxPerFrame = 2500000;
            int job_CurrentIndex = 0;


            // Permanent worker
            while (true)
            {
                // Wait if thread locked by active performance scaling
                thread_MipmapPreProcessor_AwaitingAction_Event.WaitOne(1000);

                // Wait till thread is unlocked for available work
                thread_MipmapPreProcessor_HasWork_Event.WaitOne(Timeout.Infinite);

                // Wait if thread locked by active performance scaling
                thread_MipmapPreProcessor_Scaling_Event.WaitOne(Timeout.Infinite);

                try
                {
                    // Get some more work if we have none already
                    if (!hasWork)
                    {
                        hasWork = true;
                        job_CurrentIndex = 0;
                        Buffer.BlockCopy(queue_Mipmap, 0, queue_Mipmap_Workingset, 0, queue_Mipmap.Length);
                        //queue_Mipmap_Workingset = queue_Mipmap;
                    }

                    // Start execution runtime stopwatch
                    stopwatch_Thread_Mipmap.Start();

                    // Process the node mipmap queue to remove duplicates
                    queue_Mipmap_Sparse.Clear();
                    queue_Mipmap_Sparse = DeDupeUintByteArrayToList(
                        queue_Mipmap_Workingset,
                        job_CurrentIndex,
                        job_MaxPerFrame,
                        out bool contentsFound, 
                        out int count, 
                        out int sparseCount);

                    // Update thread execution time counter
                    stopwatch_Thread_Mipmap.Stop();
                    Runtime_Thread_Mipmap = stopwatch_Thread_Mipmap.Elapsed.TotalMilliseconds;
                    stopwatch_Thread_Mipmap.Reset();

                    // Allow nodes to be mipmap if applicable
                    AbleToMipmap = contentsFound;

                    if (contentsFound)
                    {
                        job_CurrentIndex += job_MaxPerFrame;
                        if (job_CurrentIndex * 4 > queue_Mipmap_Workingset.Length) contentsFound = false;

                        // Suspend thread till output data is handled
                        thread_MipmapPreProcessor_AwaitingAction_Event.Reset();
                    }

                    // Cheap hack to avoid an additional allocation per frame.
                    if (!contentsFound)
                    {
                        hasWork = false;

                        // Suspend the thread till we have more work
                        thread_MipmapPreProcessor_HasWork_Event.Reset();
                    }
                    else
                    {


                    }

                    // If we're in the middle of an ongoing job. Then it's a good time to assess CPU load
                    if ((Runtime_Thread_Mipmap > 6) || (Runtime_Thread_Mipmap < 4 && contentsFound))
                    {
                        job_MaxPerFrame = Math.Max((int)(Convert.ToDouble(job_MaxPerFrame) / Runtime_Thread_Mipmap) * 5, 128);
                    }

                    // Store count to detemine GPU thread count later
                    MipmapQueueSparseCount = sparseCount;


                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("System.Threading.ThreadAbortException"))
                        Debug.LogWarning("<Nigiri> Exception occured in the SVO mipmap queue preprocessor thread!" + Environment.NewLine + ex);
                }
            }
        }

        /// <summary>
        /// Instructs the worker thread that it may resume processing immediately
        /// </summary>
        public void ResumeNodeWorker()
        {
            thread_SplitPreProcessor_AwaitingAction_Event.Set();
        }

        /// <summary>
        /// Instructs the worker thread that it may resume processing immediately
        /// </summary>
        public void ResumeMipmapWorker()
        {
            thread_MipmapPreProcessor_AwaitingAction_Event.Set();
        }

        /// <summary>
        /// Resize compute buffer
        /// </summary>
        /// <param name="target"></param>
        /// <param name="stride"></param>
        /// <param name="newCount"></param>
        private void ResizeComputeBuffer(ref ComputeBuffer target, int stride, int newCount)
        {
            // Update VRAM usage
            VRAM_Usage -= Convert.ToInt64(target.count) * stride * 8;
            VRAM_Usage += Convert.ToInt64(newCount) * stride * 8;

            // Resize buffer
            target.Release();
            target = new ComputeBuffer(newCount, stride, ComputeBufferType.Default);
        }

        /// <summary>
        /// Resize array
        /// </summary>
        /// <param name="target"></param>
        /// <param name="newLength"></param>
        private byte[] ResizeByteArray(byte[] target, int newLength)
        {
            // Update RAM usage
            RAM_Usage -= target.LongLength * 8;
            RAM_Usage += Convert.ToInt64(newLength) * 8;

            // Resize Array
            return new byte[newLength];
        }

        /// <summary>
        /// Set custom split queue
        /// </summary>
        /// <param name="_splitQueue"></param>
        public void SetSplitQueue(byte[] _splitQueue)
        {
            queue_Split = _splitQueue;

            // Tell the thread there's work to do
            thread_SplitPreProcessor_HasWork_Event.Set();
        }

        /// <summary>
        /// Set custom mipmap queue
        /// </summary>
        /// <param name="_splitQueue"></param>
        public void SetMipMapQueue(byte[] _mipmapQueue)
        {
            queue_Mipmap = _mipmapQueue;

            // Tell the thread there's work to do
            thread_MipmapPreProcessor_HasWork_Event.Set();
        }

        /// <summary>
        /// Deduplicates section of UInt32 byte array into List
        /// </summary>
        /// <param name="targetArray"></param>
        /// <param name="index_start"></param>
        /// <param name="length"></param>
        /// <param name="contentsFound"></param>
        /// <param name="count"></param>
        /// <param name="sparseCount"></param>
        /// <returns></returns>
        private List<uint> DeDupeUintByteArrayToList(byte[] targetArray, int index_start, int length, out bool contentsFound, out int count, out int sparseCount)
        {
            // Get appended count
            byte[] countByte = new byte[4];
            Buffer.BlockCopy(targetArray, 0, countByte, 0, 4);

            // Clamp count to max buffer size
            count = (int)Math.Min(BitConverter.ToUInt32(countByte, 0), targetArray.Length / 4);

            // Ordered preserved list
            List<uint> orderedSet = new List<uint>(count);
            orderedSet.Add(0);

            // We only do this if there's actually anything to do
            if (count > 0)
            {
                int remaining = count - index_start;
                remaining = Math.Min(count - index_start, length);

                // Copy split queue to hashset to remove dupes
                HashSet<uint> arraySet = new HashSet<uint>();
                for (int i = index_start + 1; i < index_start + Math.Min(count - index_start, length); i++)
                {
                    byte[] queueByte = new byte[4];
                    Buffer.BlockCopy(targetArray, (i * 4), queueByte, 0, 4);
                    uint queueValue = BitConverter.ToUInt32(queueByte, 0);
                    if (queueValue != 0) if (arraySet.Add(queueValue)) orderedSet.Add(queueValue);
                }

                // Keep track of how many sparse nodes in the queue
                sparseCount = arraySet.Count;

                // There's work to do!
                contentsFound = true;
            }
            else
            {
                sparseCount = 0;
                contentsFound = false;
            }

            return orderedSet;
        }

        /// <summary> 
        /// Removes duplicates from a byte array of 32bit uint values 
        /// </summary> 
        /// <param name="targetArray"></param> 
        /// <param name="contentsFound"></param> 
        /// <returns></returns> 
        private byte[] DeDupeUintByteArray(byte[] targetArray, bool retainOrder, out bool contentsFound, out int count, out int sparseCount)
        {
            // Get appended count
            byte[] countByte = new byte[4];
            Buffer.BlockCopy(targetArray, 0, countByte, 0, 4);
            // Clamp count to max buffer size
            count = (int)Math.Min(BitConverter.ToUInt32(countByte, 0), (uint)targetArray.Length / 4);

            // We only do this if there's actually anything to do
            if (count > 0)
            {
                // Copy split queue to hashset to remove dupes
                List<uint> orderedSet = new List<uint>();
                HashSet<uint> arraySet = new HashSet<uint>();
                for (int i = 1; i < count; i++)
                {
                    byte[] queueByte = new byte[4];
                    Buffer.BlockCopy(targetArray, (i * 4), queueByte, 0, 4);
                    uint queueValue = BitConverter.ToUInt32(queueByte, 0);
                    if (queueValue != 0)
                    {
                        if (arraySet.Add(queueValue)) orderedSet.Add(queueValue);
                    }
                }

                // Keep track of how many sparse nodes in the queue
                sparseCount = arraySet.Count;

                if (sparseCount > 0)
                {
                    if (retainOrder)
                    {
                        // Copy list back to array
                        int queueWriteBackIndex = 1;
                        foreach (uint offset in orderedSet)
                        {
                            byte[] queueByte = new byte[4];
                            queueByte = BitConverter.GetBytes(offset);
                            Buffer.BlockCopy(queueByte, 0, targetArray, queueWriteBackIndex * 4, 4);
                            queueWriteBackIndex++;
                        }
                    }
                    else
                    {
                        // Copy hashset back to array
                        int queueWriteBackIndex = 1;
                        HashSet<uint>.Enumerator queueEnum = arraySet.GetEnumerator();
                        while (queueEnum.MoveNext())
                        {
                            byte[] queueByte = new byte[4];
                            queueByte = BitConverter.GetBytes(queueEnum.Current);
                            Buffer.BlockCopy(queueByte, 0, targetArray, queueWriteBackIndex * 4, 4);
                            queueWriteBackIndex++;
                        }
                    }
                    // There's work to do!
                    contentsFound = true;
                }
                else
                {
                    // Nothing to do.
                    contentsFound = false;
                }
            }
            else
            {
                contentsFound = false;
                sparseCount = 0;
            }

            // Zero append counter
            Array.Clear(targetArray, 0, 4);

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
            // [2] Split queue position counter
            // [3] Max mipmap queue items
            // [4] Mipmap queue position counter
            // [5] Mask buffer position
            // [6] --- UNUSED
            // [7] --- UNUSED

            // Set buffer variables
            uint[] Counters = new uint[Buffer_Counters_Count];
            Counters[0] = MaxDepth;
            Counters[1] = (uint) SplitQueueMaxLength;
            Counters[2] = 0;
            Counters[3] = (uint) MipmapQueueMaxLength;
            Counters[4] = 0;
            Counters[5] = 0;
            Counters[6] = 0;
            Counters[7] = 0;

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
                    // Destroy thread dispatcher
                    Nigiri.Helpers.DestroyGameObject(ref threadDispatchObject);
                    threadDispatch = null;

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
