/// <summary>
/// NKLI     : Nigiri - SVONode
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;

namespace NKLI.Nigiri.SVO
{
    /// <summary>
    /// Packed octree node data format
    /// Length (32B / 256b)
    /// </summary>
    public struct SVONode
    {
        // Linked reference offset or coordinate (32b)
        public uint referenceOffset;

        // Packed payload (32b)
        public uint packedBitfield;

        // Colour value (128b)
        public uint value_R;
        public uint value_G;
        public uint value_B;
        public uint value_A;

        // When used in shader, pad to fit 128 bit cache alignment
        #pragma warning disable CS0414
        readonly uint pad0;
        readonly uint pad1;
        #pragma warning restore CS0414
        // In cases of 64 bit payloads, this is unnecesary

        // Constructor - Packed
        public SVONode(uint _referenceOffset, uint _packedBitfield)
        {
            packedBitfield = _packedBitfield;
            referenceOffset = _referenceOffset;

            // Colour
            value_R = 0;
            value_G = 0;
            value_B = 0;
            value_A = 0;

            // Padding
            pad0 = 0;
            pad1 = 0;
        }

        // Constructor - UnPacked
        public SVONode(uint _referenceOffset, uint bitFieldOccupancy, uint runLength, uint depth, bool isLeaf)
        {
            packedBitfield = 0;
            referenceOffset = _referenceOffset;

            // Colour
            value_R = 0;
            value_G = 0;
            value_B = 0;
            value_A = 0;

            // Padding
            pad0 = 0;
            pad1 = 0;

            // Pack values
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
        public void PackStruct(uint bitfieldOccupancy, uint runLength, uint depth, bool isLeaf)
        {
            packedBitfield = (bitfieldOccupancy << 24) | (runLength << 20) | (depth << 16) | (Convert.ToUInt32(isLeaf) << 15);
        }

        // Unpack 32 bits
        public void UnPackStruct(out uint _bitfieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf)
        {
            //ulong padding = (packedBitfield & 0x7FFF);
            isLeaf = Convert.ToBoolean((packedBitfield >> 15) & 1);
            _depth = (uint)(packedBitfield >> 16) & 0xF;
            _runLength = (uint)(packedBitfield >> 20) & 0xF;
            _bitfieldOccupancy = (uint)(packedBitfield >> 24) & 0xFF;
        }
    }
}