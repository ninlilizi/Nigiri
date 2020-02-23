﻿using System;
using UnityEngine;

namespace NKLI.Nigiri.SVO
{
    /// <summary>
    /// Builds static sparse voxel octree from Morton ordered buffer
    /// </summary>
    #region Spase voxel builder
    class SVOBuilder
    {
        // Read-only properties
        public int VoxelCount { get; private set; }
        public int TreeDepth { get; private set; }

        ComputeShader shader_SVOBuilder;

        // Octree Node struct
        struct SVONode
        {
            // Linked reference offset or coordinate
            // (isRoot)  Morton coordinate, 32b
            // (!isRoot) BufferOffset, 32b
            public uint referenceOffset;

            // Packed payload
            public uint packedBitfield;

            // When used in shader, pad to fit 128 byte cache alignment
            //readonly uint pad0;
            //readonly uint pad1;
            // In cases of 64 byte payloads, this is unnecesary

            // Constructor - Packed
            public SVONode(uint _referenceOffset, uint _packedBitfield)
            {
                packedBitfield = _packedBitfield;
                referenceOffset = _referenceOffset;
            }

            // Constructor - UnPacked
            public SVONode(uint _referenceOffset, uint bitFieldOccupancy, uint runLength, uint depth, bool isRoot)
            {
                packedBitfield = 0;
                referenceOffset = _referenceOffset;
                PackStruct(bitFieldOccupancy, runLength, depth, isRoot);
            }

            // Pack 64 bits
            // BO = Bitfield occupancy, 8b
            // RL = Sparse runlength, 4b
            // OD = Octree depth of this node, 4b
            // IR = Is this a root node, 1b
            // Structure [00] [01] [02] [03] [04] [05] [06] [07] [08] [09] [10] [11] [12] [13] [14] [15]
            //            MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC
            //           [16] [17] [18] [19] [20] [21] [22] [23] [24] [25] [26] [27] [28] [29] [30] [31]
            //            MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC   MC
            // Structure [32] [33] [34] [35] [36] [37] [38] [39] [40] [41] [42] [43] [44] [45] [46] [47]
            //            BO   BO   BO   BO   BO   BO   BO   BO   RL   RL   RL   RL   OD   OD   OD   OD
            //           [48] [49] [50] [51] [52] [53] [54] [55] [56] [57] [58] [59] [60] [61] [62] [63]
            //            IR   --   --   --   --   --   --   --   --   --   --   --   --   --   --   --
            public void PackStruct(uint bitFieldOccupancy, uint runLength, uint depth, bool isRoot)
            {
                packedBitfield = (bitFieldOccupancy << 32) | (runLength << 24) | (depth << 20) | (Convert.ToUInt32(isRoot) << 16);
            }

            // Unpack 64 bits
            public void UnPackStruct(out uint _bifFieldOccupancy, out uint _runLength, out uint _depth, out bool isRoot)
            {
                //ulong padding = (packedBitfield & 0x7FFF);
                isRoot = Convert.ToBoolean((packedBitfield >> 15) & 1);
                _depth = (uint)(packedBitfield >> 16) & 0xF;
                _runLength = (uint)(packedBitfield >> 20) & 0xF;
                _bifFieldOccupancy = (uint)(packedBitfield >> 24) & 0xFF;
            }
        }
        //

        // Constructor
        public SVOBuilder(ComputeBuffer mortonBuffer, int _voxelCount, int gridWidth, out ComputeBuffer buffer_SVO)
        {
            // Assign instance variable
            VoxelCount = _voxelCount;

            // Load shader
            shader_SVOBuilder = Resources.Load("NKLI_Nigiri_SVOBuilder") as ComputeShader;

            // Calculate threadcount and depth index boundaries
            int threadCount = GetThreadCount(gridWidth, out int[] boundaries);
            int dispatchCount = (int)Math.Sqrt(threadCount) / 16;

            // Synchronisation counter buffer
            ComputeBuffer buffer_Counters = new ComputeBuffer(4, sizeof(UInt32), ComputeBufferType.Default);

            // Temporary PTR storage buffer
            ComputeBuffer buffer_PTR = new ComputeBuffer(VoxelCount, sizeof(UInt32), ComputeBufferType.Default);

            // Output buffer to contain final SVO
            buffer_SVO = new ComputeBuffer(threadCount, 128, ComputeBufferType.Raw);

            // Assign to compute
            shader_SVOBuilder.SetBuffer(0, "buffer_Counters", buffer_Counters);
            shader_SVOBuilder.SetBuffer(0, "buffer_PTR", buffer_PTR);
            shader_SVOBuilder.SetBuffer(0, "buffer_SVO", buffer_SVO);
            shader_SVOBuilder.SetInts("boundaries", boundaries); // Likely unneded, but here for now
            shader_SVOBuilder.SetInt("threadCount", threadCount);
            shader_SVOBuilder.SetInt("voxelCount", VoxelCount);

            // Dispatch compute
            shader_SVOBuilder.Dispatch(0, dispatchCount, dispatchCount, 1);

        }

        // Calculate thread count
        public int GetThreadCount(int gridWidth, out int[] boundaries)
        {
            // Local variable assignment
            int cycles = 0;
            int threadCount = 0;

            // Get depth of tree
            TreeDepth = GetDepth(gridWidth);           
            boundaries = new int[TreeDepth];  // Likely unneded, but here for now

            // Do the work
            int depth = TreeDepth;
            while (depth > 0)
            {
                threadCount += VoxelCount;
                boundaries[cycles] = threadCount;  // Likely unneded, but here for now
                VoxelCount = Math.Max(VoxelCount / 8, 1);
                cycles++;
                depth--;
            }
            return threadCount;
        }

        // Calculate depth of tree
        public int GetDepth(int gridWidth)
        {
            int depth = 0;
            while (gridWidth > 1)
            {
                depth++;
                gridWidth /= 8;
            }
            return depth;
        }
    }
    #endregion
    //

}
