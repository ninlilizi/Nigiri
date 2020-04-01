/// <summary>
/// NKLI     : Nigiri - SVONode CGINC 
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

///

/// <summary>
/// Packed octree node data format
/// </summary>
struct SVONode
{
	// Linked reference offset or coordinate
	// (isRoot)  Morton/Buffer, target coordinate/Offset, 32b
	// (!isRoot) This buffer offset, 32b
    uint referenceOffset;

	// Packed payload
    uint packedBitfield;

    // Colour value (128b)
    float value_R;
    float value_G;
    float value_B;
    float value_A;

    // When used in shader, pad to fit 128 bit cache alignment
    uint pad0;
    uint pad1;
    // In cases of 64 bit payloads, this is unnecesary

	// Pack 32 bits
	// BO = Bitfield occupancy, 8b
	// RL = Sparse runlength, 4b
	// OD = Octree TTL depth of this node, 4b
	// IR = Is this a root node, 1b
	// Structure [00] [01] [02] [03] [04] [05] [06] [07] [08] [09] [10] [11] [12] [13] [14] [15]
	//            BO   BO   BO   BO   BO   BO   BO   BO   RL   RL   RL   RL   OD   OD   OD   OD
	//           [16] [17] [18] [19] [20] [21] [22] [23] [24] [25] [26] [27] [28] [29] [30] [31]
	//            IR   --   --   --   --   --   --   --   --   --   --   --   --   --   --   --
    void PackStruct(uint bitFieldOccupancy, uint runLength, uint ttl, uint isLeaf)
    {
        packedBitfield = (bitFieldOccupancy << 24) | (runLength << 20) | (ttl << 16) | (isLeaf << 15);
    }

	// Unpack 32 bits
    void UnPackStruct(out uint _bifFieldOccupancy, out uint _runLength, out uint _ttl, out uint isLeaf)
    {
		//ulong padding = (packedBitfield & 0x7FFF);
        isLeaf = (packedBitfield >> 15) & 1;
        _ttl = (uint) (packedBitfield >> 16) & 0xF;
        _runLength = (uint) (packedBitfield >> 20) & 0xF;
        _bifFieldOccupancy = (uint) (packedBitfield >> 24) & 0xFF;
    }
    
    inline void Intialize()
    {
        referenceOffset = 0;
        packedBitfield = 0;
        value_A = 0;
        value_R = 0;
        value_G = 0;
        value_B = 0;
    
        pad0 = 0;
        pad1 = 0;
    }
};