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
    public class SVOBuilder
    {
        // Read-only properties
        public int ThreadCount { get; private set; }
        public int VoxelCount { get; private set; }
        public int NodeCount { get; private set; }
        public int TreeDepth { get; private set; }
        public int[] Boundaries { get; private set; }

        // Read-only buffer properties
        public ComputeBuffer Buffer_SVO { get; private set; }
        public ComputeBuffer Buffer_Counters { get; private set; }

        // Compute
        ComputeShader shader_SVOBuilder;

        // Constructor
        public SVOBuilder(ComputeBuffer buffer_Morton, int occupiedVoxels, int gridWidth)
        {
            // Load shader
            shader_SVOBuilder = Resources.Load("NKLI_Nigiri_SVOBuilder") as ComputeShader;

            // Calculate depth
            TreeDepth = SVOHelper.GetDepth(gridWidth);

            // Calculate threadcount and depth index boundaries
            ThreadCount = SVOHelper.GetThreadCount(occupiedVoxels, gridWidth, TreeDepth, out int[] boundaries);
            NodeCount = SVOHelper.GetNodeCount(occupiedVoxels, gridWidth, TreeDepth);
            Boundaries = boundaries;
            int dispatchCount = ThreadCount;
            int maxVoxels = gridWidth * gridWidth * gridWidth;

            // Assign instance variables
            VoxelCount = occupiedVoxels;
            NodeCount = SVOHelper.GetNodeCount(occupiedVoxels, gridWidth, TreeDepth);


            // Temporary PTR storage buffer
            ComputeBuffer buffer_PTR = new ComputeBuffer(maxVoxels, sizeof(uint), ComputeBufferType.Default);

            // Synchronisation counter buffer
            Buffer_Counters = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.Default);

            // Zero counters, we don't know why. Just that we must
            Buffer_Counters.SetData(new int[4]);
                 
            // Output buffer to contain final SVO
            Buffer_SVO = new ComputeBuffer(NodeCount, 8, ComputeBufferType.Default);

            // Assign to compute
            shader_SVOBuilder.SetBuffer(0, "buffer_Counters", Buffer_Counters);
            shader_SVOBuilder.SetBuffer(0, "buffer_Morton", buffer_Morton);
            shader_SVOBuilder.SetBuffer(0, "buffer_PTR", buffer_PTR);
            shader_SVOBuilder.SetBuffer(0, "buffer_SVO", Buffer_SVO);
            shader_SVOBuilder.SetInts("boundaries", boundaries); // Likely unneded, but here for now
            shader_SVOBuilder.SetInt("threadCount", ThreadCount);
            shader_SVOBuilder.SetInt("voxelCount", VoxelCount);
            shader_SVOBuilder.SetInt("treeDepth", TreeDepth);

            // Dispatch compute
            shader_SVOBuilder.Dispatch(0, dispatchCount, 1, 1);

            // Cleanup
            buffer_PTR.Dispose();
        }

        // Syncronous 'async' readback for Unit Testing.
        public void SyncGPUReadback(out AsyncGPUReadbackRequest req_Counters, out AsyncGPUReadbackRequest req_SVO)
        {
            req_Counters = AsyncGPUReadback.Request(Buffer_Counters);
            req_SVO = AsyncGPUReadback.Request(Buffer_SVO);

            req_Counters.WaitForCompletion();
            req_SVO.WaitForCompletion();
        }

        // Cleanup
        public void Dispose()
        {
            // We try to explicity dispose these objects as not doing can result
            //  in leaks or uneven performance further down the pipeline.
            Buffer_Counters.Dispose();
            Buffer_SVO.Dispose();
        }
    }
    #endregion
    //

    /// <summary>
    /// Packed octree node data format
    /// </summary>
    #region Octree Node struct
    public struct SVONode
    {
        // Linked reference offset or coordinate
        // (isLeaf)  Morton/Buffer, target coordinate/Offset, 32b
        // (!isLeaf) This buffer offset, 32b
        public uint referenceOffset;

        // Packed payload
        public uint packedBitfield;

        // When used in shader, pad to fit 128 bit cache alignment
        //readonly uint pad0;
        //readonly uint pad1;
        // In cases of 64 bit payloads, this is unnecesary

        // Constructor - Packed
        public SVONode(uint _referenceOffset, uint _packedBitfield)
        {
            packedBitfield = _packedBitfield;
            referenceOffset = _referenceOffset;
        }

        // Constructor - UnPacked
        public SVONode(uint _referenceOffset, uint bitFieldOccupancy, uint runLength, uint depth, bool isLeaf)
        {
            packedBitfield = 0;
            referenceOffset = _referenceOffset;
            PackStruct(bitFieldOccupancy, runLength, depth, isLeaf);
        }

        // Pack 32 bits
        // BO = Bitfield occupancy, 8b
        // RL = Sparse runlength, 4b
        // OD = Octree depth of this node, 4b
        // IR = Is this a root node, 1b
        // Structure [00] [01] [02] [03] [04] [05] [06] [07] [08] [09] [10] [11] [12] [13] [14] [15]
        //            BO   BO   BO   BO   BO   BO   BO   BO   RL   RL   RL   RL   OD   OD   OD   OD
        //           [16] [17] [18] [19] [20] [21] [22] [23] [24] [25] [26] [27] [28] [29] [30] [31]
        //            IR   --   --   --   --   --   --   --   --   --   --   --   --   --   --   --
        public void PackStruct(uint bitFieldOccupancy, uint runLength, uint depth, bool isLeaf)
        {
            packedBitfield = (bitFieldOccupancy << 24) | (runLength << 20) | (depth << 16) | (Convert.ToUInt32(isLeaf) << 15);
        }

        // Unpack 32 bits
        public void UnPackStruct(out uint _bifFieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf)
        {
            //ulong padding = (packedBitfield & 0x7FFF);
            isLeaf = Convert.ToBoolean((packedBitfield >> 15) & 1);
            _depth = (uint)(packedBitfield >> 16) & 0xF;
            _runLength = (uint)(packedBitfield >> 20) & 0xF;
            _bifFieldOccupancy = (uint)(packedBitfield >> 24) & 0xFF;
        }
    }
    #endregion
    //

    /// <summary>
    /// Helper functions
    /// </summary>
    public class SVOHelper
    {
        // Calculates occupancy bitmap from int[8] array
        public static uint GetOccupancyBitmap(uint[] values)
        {
            return
                (Math.Min(values[0], 1) << 7) |
                (Math.Min(values[1], 1) << 6) |
                (Math.Min(values[2], 1) << 5) |
                (Math.Min(values[3], 1) << 4) |
                (Math.Min(values[4], 1) << 3) |
                (Math.Min(values[5], 1) << 2) |
                (Math.Min(values[6], 1) << 1) |
                (Math.Min(values[7], 1) & 1);

        }

        // Finds current depth from boundary array
        public static uint GetDepthFromBoundaries(uint index, int[] boundaries)
        {
            // TODO - Make this efficient (LUT, etc)
            return 0;
        }

        // Calculate thread count
        public static int GetThreadCount(int _nodeCount, int gridWidth, int treeDepth, out int[] boundaries)
        {
            // Get depth of tree
            boundaries = new int[treeDepth];

            // Start at max depth -1
            int cycles = 1;
            // Starting value is thick buffer size for leaf nodes
            int threadCount = (gridWidth * gridWidth * gridWidth) / 8;

            // Root depth only gathered from so less threads needed
            int nodeCount = (int)(Math.Ceiling(_nodeCount / 8.0d) * 8);

            // Divide by 8 to start at max depth -1
            nodeCount /= 8;

            // First boundary is thick buffer size
            boundaries[0] = threadCount;

            // Do the work
            while (treeDepth > cycles)
            {
                // Divide by 8 to get the thread count
                nodeCount /= 8;

                // Tabulate the sum
                threadCount += nodeCount;
                
                // Add depth boundary index to array
                boundaries[cycles] = threadCount;
                
                // Increment counter
                cycles++;          
            }
            return threadCount;
        }

        // Calculate thread count
        public static int GetNodeCount(int _nodeCount, int gridWidth, int treeDepth)
        {
            // Assign local
            int cycles = 0;
            int finalNodeCount = 0;

            // Root depth only gathered from so less threads needed
            int nodeCount = (int)(Math.Ceiling(_nodeCount / 8.0d) * 8);

            // Do the work
            while (treeDepth > cycles)
            {
                // Divide by 8 to get the thread count
                nodeCount /= 8;

                // Tabulate the sum
                finalNodeCount += nodeCount;

                // Increment counter
                cycles++;
            }
            return finalNodeCount;
        }

        // Calculate depth of tree
        public static int GetDepth(int gridWidth)
        {
            int depth = 0;
            while (gridWidth > 1)
            {
                depth++;
                gridWidth /= 2;
            }
            return depth;
        }
    }
}
