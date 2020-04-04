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
        packedColour = 0;
        colour_A = 0;
    
        //pad0 = 0;
        //pad1 = 0;
    }
    
    /// <summary>
    /// Encodes 32bit HDR RGB into 8bit RGBA.
    /// Credits to: http://graphicrants.blogspot.com/2009/04/rgbm-color-encoding.html
    /// </summary>
    inline float4 RGBMEncode(float3 colour)
    {
        //colour = pow(colour, 0.454545); // Convert Linear to Gamma
        float4 rgbm;
        colour *= 1.0 / 6.0;
        rgbm.a = saturate(max(max(colour.r, colour.g), max(colour.b, 1e-6)));
        rgbm.a = ceil(rgbm.a * 255.0) / 255.0;
        rgbm.rgb = colour / rgbm.a;
        return rgbm;
    }

    /// <summary>
    /// Decodes 8bit RGBA into 32bit HDR RGB
    /// Credits to: http://graphicrants.blogspot.com/2009/04/rgbm-color-encoding.html
    /// </summary>
    inline float3 RGBMDecode(float4 rgbm)
    {
        return 6.0 * rgbm.rgb * rgbm.a; // Also converts Gamma to Linear
	    //return pow(6.0 * rgbm.rgb * rgbm.a, 2.2); // Also converts Gamma to Linear
    }
    
    /// <summary>
    /// Packs HDR RGBA into uint2
    /// </summary>
    inline void PackColour(float4 colour)
    {
        // Structure [00] [01] [02] [03] [04] [05] [06] [07] [08] [09] [10] [11] [12] [13] [14] [15]
	    //            R    R    R    R    R    R    R    R    G    G    G    G    G    G    G    G 
	    //           [16] [17] [18] [19] [20] [21] [22] [23] [24] [25] [26] [27] [28] [29] [30] [31]
	    //            B    B    B    B    B    B    B    B    A    A    A    A    A    A    A    A 
        
        uint4 encodedColour = RGBMEncode(colour.rgb) * 255;
        
        packedColour = (encodedColour.r << 24) | (encodedColour.g << 16) | (encodedColour.b << 8) | (encodedColour.a);
        colour_A = colour.a;
    }
    
    /// <summary>
    /// Returns unpacked HDR RGBA values
    /// </summary>
    inline float4 UnPackColour()
    {
        float4 encodedColour;
        encodedColour.a = (packedColour) & 0xFF;
        encodedColour.b = (packedColour >> 8) & 0xFF;
        encodedColour.g = (packedColour >> 16) & 0xFF;
        encodedColour.r = (packedColour >> 24) & 0xFF;
        
        return float4(RGBMDecode(encodedColour / 255), colour_A);

    }
};