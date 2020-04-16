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
        uint packedColour;
        float colour_A;

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
            PackStruct(bitFieldOccupancy, runLength, depth, isLeaf);
        }

        // Pack 32 bits
        // BO = Bitfield occupancy, 8b
        // RL = Sparse runlength, 4b
        // OD = Octree depth of this node, 4b
        // IR = Is this node waiting to be mipmapped, 1b
        // Structure [00] [01] [02] [03] [04] [05] [06] [07] [08] [09] [10] [11] [12] [13] [14] [15]
        //            BO   BO   BO   BO   BO   BO   BO   BO   RL   RL   RL   RL   OD   OD   OD   OD
        //           [16] [17] [18] [19] [20] [21] [22] [23] [24] [25] [26] [27] [28] [29] [30] [31]
        //            IR   --   --   --   --   --   --   --   --   --   --   --   --   --   --   --
        public void PackStruct(uint bitfieldOccupancy, uint runLength, uint depth, bool isWaitingForMipmap)
        {
            packedBitfield = (bitfieldOccupancy << 24) | (runLength << 20) | (depth << 16) | (Convert.ToUInt32(isWaitingForMipmap) << 15);
        }

        // Unpack 32 bits
        public void UnPackStruct(out uint _bitfieldOccupancy, out uint _runLength, out uint _depth, out bool isWaitingForMipmap)
        {
            //ulong padding = (packedBitfield & 0x7FFF);
            isWaitingForMipmap = Convert.ToBoolean((packedBitfield >> 15) & 1);
            _depth = (uint)(packedBitfield >> 16) & 0xF;
            _runLength = (uint)(packedBitfield >> 20) & 0xF;
            _bitfieldOccupancy = (uint)(packedBitfield >> 24) & 0xFF;
        }

        /// <summary>
        /// Encodes 32bit HDR RGB into 8bit RGBA.
        /// Credits to: http://graphicrants.blogspot.com/2009/04/rgbm-color-encoding.html
        /// </summary>
        Vector4 RGBMEncode(Vector3 colour)
        {
            //colour = pow(colour, 0.454545); // Convert Linear to Gamma
            Vector4 rgbm = new Vector4();
            colour *= (float)(1.0 / 6.0);
            rgbm.w = Mathf.Clamp(Mathf.Max(Mathf.Max(colour.x, colour.y), Mathf.Max(colour.z, (float)1e-6)), 0, 1);
            rgbm.w = Mathf.Ceil(rgbm.w * 255.0F) / 255.0F;
            rgbm.x = colour.x / rgbm.w;
            rgbm.y = colour.y / rgbm.w;
            rgbm.z = colour.z / rgbm.w;
            return rgbm;
        }

        /// <summary>
        /// Decodes 8bit RGBA into 32bit HDR RGB.
        /// Credits to: http://graphicrants.blogspot.com/2009/04/rgbm-color-encoding.html
        /// </summary>
        Vector3 RGBMDecode(Vector4 rgbm)
        {
            rgbm.x *= rgbm.w * 6.0f;
            rgbm.y *= rgbm.w * 6.0f;
            rgbm.z *= rgbm.w * 6.0f;
            return new Vector3(rgbm.x, rgbm.y, rgbm.z);
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

            colour.x *= 255;
            Vector4 encodedColour = RGBMEncode(colour);

            packedColour = ((uint)encodedColour.x << 24) | ((uint)encodedColour.y << 16) | ((uint)encodedColour.z << 8) | ((uint)encodedColour.w);
            colour_A = colour.w;
        }

        /// <summary>
        /// Returns unpacked HDR RGBA values
        /// </summary>
        public Vector4 UnPackColour()
        {
            Vector4 encodedColour;
            encodedColour.x = (packedColour) & 0xFF;
            encodedColour.y = (packedColour >> 8) & 0xFF;
            encodedColour.z = (packedColour >> 16) & 0xFF;
            encodedColour.w = (packedColour >> 24) & 0xFF;

            encodedColour.x /= 255;
            encodedColour.y /= 255;
            encodedColour.z /= 255;
            encodedColour.w /= 255;

            Vector3 decodedColour = RGBMDecode(encodedColour);

            return new Vector4(decodedColour.x, decodedColour.y, decodedColour.z, colour_A);
        }
    }
}