/// <summary>
/// NKLI     : Nigiri - SVONode
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using UnityEngine;

namespace NKLI.Nigiri.SVO
{
    /// <summary>
    /// Packed octree node data format
    /// Length (16B / 128b)
    /// </summary>
    public struct SVONode
    {
        // Linked reference offset or coordinate (32b)
        public uint referenceOffset;

        // Packed payload (32b)
        public uint packedBitfield;

        // Colour value (64b)
        public uint packedColour;
        public float colour_A;

        // When used in shader, pad to fit 128 bit cache alignment
        //#pragma warning disable CS0414
        //readonly uint pad0;
        //readonly uint pad1;
        //#pragma warning restore CS0414
        // In cases of 64 bit payloads, this is unnecesary

        // Constructor - Packed
        public SVONode(uint _referenceOffset, uint _packedBitfield)
        {
            packedBitfield = _packedBitfield;
            referenceOffset = _referenceOffset;

            // Colour
            packedColour = 0;
            colour_A = 0;

            // Padding
            //pad0 = 0;
            //pad1 = 0;
        }

        // Constructor - UnPacked
        public SVONode(uint _referenceOffset, uint bitFieldOccupancy, uint runLength, uint depth, bool isLeaf)
        {
            packedBitfield = 0;
            referenceOffset = _referenceOffset;

            // Colour
            packedColour = 0;
            colour_A = 0;

            // Padding
            //pad0 = 0;
            //pad1 = 0;

            // Pack values
            PackStruct(bitFieldOccupancy, depth, isLeaf);
        }

        // Pack 32 bits
        // BO = Bitfield occupancy, 8b
        // RL = Sparse runlength, 4b
        // OD = Octree depth of this node, 4b
        // IR = Is this node waiting to be mipmapped, 1b
        // Structure [00] [01] [02] [03] [04] [05] [06] [07] [08] [09] [10] [11] [12] [13] [14] [15]
        //            BO   BO   BO   BO   BO   BO   BO   BO   OD   OD   OD   OD   IR   --   --   --
        //           [16] [17] [18] [19] [20] [21] [22] [23] [24] [25] [26] [27] [28] [29] [30] [31]
        //            --   --   --   --   --   --   --   --   --   --   --   --   --   --   --   --
        public void PackStruct(uint bitfieldOccupancy, uint ttl, bool isWaitingForMipmap)
        {
            packedBitfield = (bitfieldOccupancy << 24) | (ttl << 20) |  (Convert.ToUInt32(isWaitingForMipmap) << 19);
        }

        // Unpack 32 bits
        public void UnPackStruct(out uint _bitfieldOccupancy, out uint _ttl, out bool isWaitingForMipmap)
        {
            //ulong padding = (packedBitfield & 0x7FFF);
            isWaitingForMipmap = Convert.ToBoolean((packedBitfield >> 19) & 1);
            _ttl = (uint)(packedBitfield >> 20) & 0xF;
            _bitfieldOccupancy = (uint)(packedBitfield >> 24) & 0xFF;
        }

        const int MAX_BRIGHTNESS = 12;

        /// <summary>
        /// Encodes HDR half4 into uint
        /// Credits to: https://github.com/keijiro/PackedRGBMShader
        /// </summary>
        uint EncodeColour(Vector3 rgb)
        {
            float y = Mathf.Max(Mathf.Max(rgb.x, rgb.y), rgb.z);
            y = Mathf.Clamp(Mathf.Ceil(y * 255 / MAX_BRIGHTNESS), 1, 255);
            rgb *= 255 * 255 / (y * MAX_BRIGHTNESS);
            Vector4 i = new Vector4(rgb.x, rgb.y, rgb.z, y);
            return (uint)i.x | ((uint)i.y << 8) | ((uint)i.z << 16) | ((uint)i.w << 24);
        }

        /// <summary>
        /// Decodes HDR half4 from uint
        /// Credits to: https://github.com/keijiro/PackedRGBMShader
        /// </summary>
        Vector3 DecodeColour(uint data)
        {
            float r = (data) & 0xff;
            float g = (data >> 8) & 0xff;
            float b = (data >> 16) & 0xff;
            float a = (data >> 24) & 0xff;
            return new Vector3(r, g, b) * a * MAX_BRIGHTNESS / (255 * 255);
        }

        /// <summary>
        /// Packs HDR RGBA into uint2
        /// </summary>
        public void PackColour(Vector4 colour)
        {
            // Structure [00] [01] [02] [03] [04] [05] [06] [07] [08] [09] [10] [11] [12] [13] [14] [15]
            //            R    R    R    R    R    R    R    R    G    G    G    G    G    G    G    G 
            //           [16] [17] [18] [19] [20] [21] [22] [23] [24] [25] [26] [27] [28] [29] [30] [31]
            //            B    B    B    B    B    B    B    B    A    A    A    A    A    A    A    A 

            packedColour = EncodeColour(new Vector4(colour.x, colour.y, colour.z));
            colour_A = colour.w;
        }

        /// <summary>
        /// Returns unpacked HDR RGBA values
        /// </summary>
        public Vector4 UnPackColour()
        {
            Vector4 unpackedColour = DecodeColour(packedColour);
            return new Vector4(unpackedColour.x, unpackedColour.y, unpackedColour.z, colour_A);
        }
    }
}