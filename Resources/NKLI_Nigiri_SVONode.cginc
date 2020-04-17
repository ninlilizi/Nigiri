/// <summary>
/// NKLI     : Nigiri - SVONode CGINC 
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

///

/// <summary>
/// Packed octree node data format
/// Length (16B / 128b)
/// </summary>
struct SVONode
{
	// Linked reference offset or coordinate
	// (isRoot)  Morton/Buffer, target coordinate/Offset, 32b
	// (!isRoot) This buffer offset, 32b
    uint referenceOffset;

	// Packed payload
    uint packedBitfield;

    // Colour value (64b)
    uint packedColour;
    float colour_A;

    // When used in shader, pad to fit 128 bit cache alignment
    //uint pad0;
    //uint pad1;
    // In cases of 64 bit payloads, this is unnecesary

	// Pack 32 bits
	// BO = Bitfield occupancy, 8b
	// RL = Sparse runlength, 4b
	// OD = Octree TTL depth of this node, 4b
	// IR = Is this a root node, 1b
	// Structure [00] [01] [02] [03] [04] [05] [06] [07] [08] [09] [10] [11] [12] [13] [14] [15]
	//            BO   BO   BO   BO   BO   BO   BO   BO   OD   OD   OD   OD   IR   --   --   --
	//           [16] [17] [18] [19] [20] [21] [22] [23] [24] [25] [26] [27] [28] [29] [30] [31]
	//            --   --   --   --   --   --   --   --   --   --   --   --   --   --   --   --
    void PackStruct(uint bitFieldOccupancy, uint ttl, uint isWaitingForMipmap)
    {
        packedBitfield = (bitFieldOccupancy << 24) | (ttl << 20) | (isWaitingForMipmap << 19);
    }

	// Unpack 32 bits
    void UnPackStruct(out uint _bifFieldOccupancy, out uint _ttl, out uint isWaitingForMipmap)
    {
		//ulong padding = (packedBitfield & 0x7FFF);
        isWaitingForMipmap = (packedBitfield >> 19) & 1;
        _ttl = (uint) (packedBitfield >> 20) & 0xF;
        _bifFieldOccupancy = (uint) (packedBitfield >> 24) & 0xFF;
    }
    
    inline void Intialize()
    {
        referenceOffset = 0;
        packedBitfield = 0;
        packedColour = 0;
        colour_A = 0;
    
        //pad0 = 0;
        //pad1 = 0;
    }
       
  
    #define MAX_BRIGHTNESS 12
    
    /// <summary>
    /// Encodes HDR half4 into uint
    /// Credits to: https://github.com/keijiro/PackedRGBMShader
    /// </summary>
    inline uint EncodeColour(half3 rgb)
    {
        half y = max(max(rgb.r, rgb.g), rgb.b);
        y = clamp(ceil(y * 255 / MAX_BRIGHTNESS), 1, 255);
        rgb *= 255 * 255 / (y * MAX_BRIGHTNESS);
        uint4 i = half4(rgb, y);
        return i.x | (i.y << 8) | (i.z << 16) | (i.w << 24);
    }

    /// <summary>
    /// Decodes HDR half4 from uint
    /// Credits to: https://github.com/keijiro/PackedRGBMShader
    /// </summary>
    inline half3 DecodeColour(uint data)
    {
        half r = (data) & 0xff;
        half g = (data >> 8) & 0xff;
        half b = (data >> 16) & 0xff;
        half a = (data >> 24) & 0xff;
        return half3(r, g, b) * a * MAX_BRIGHTNESS / (255 * 255);
    }
    
    /// <summary>
    /// Packs HDR RGBA into uint2
    /// </summary>
    inline void PackColour(half4 colour)
    {
        // Structure [00] [01] [02] [03] [04] [05] [06] [07] [08] [09] [10] [11] [12] [13] [14] [15]
	    //            R    R    R    R    R    R    R    R    G    G    G    G    G    G    G    G 
	    //           [16] [17] [18] [19] [20] [21] [22] [23] [24] [25] [26] [27] [28] [29] [30] [31]
	    //            B    B    B    B    B    B    B    B    A    A    A    A    A    A    A    A 
        
        packedColour = EncodeColour(colour.rgb);
        colour_A = colour.a;
    }
    
    /// <summary>
    /// Returns unpacked HDR RGBA values
    /// </summary>
    inline half4 UnPackColour()
    {
        return half4(DecodeColour(packedColour), colour_A);
    }
        
    /// <summary>
    /// Returns weather node needs mipmapping
    /// </summary>
    inline uint GetIsWaitingForMipmap()
    {

        return (packedBitfield >> 19) & 1;
    }
    
    /// <summary>
    /// Sets weather node needs mipmapping
    /// </summary>
    inline void SetIsWaitingForMipmap(uint value)
    {
        packedBitfield &= ~(1 << 19);
        packedBitfield |= value << 19;
    }
    
    /// <summary>
    /// Returns TTL
    /// </summary>
    inline uint GetTTL()
    {
        return (packedBitfield >> 20) & 0xF;
    }
    
    /// <summary>
    /// Returns single occupancy bit
    /// </summary>
    inline uint GetOccupancyBit(uint index)
    {
        return ((packedBitfield >> 24 + index) & 1);
    }
};