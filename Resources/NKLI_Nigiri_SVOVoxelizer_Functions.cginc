/// <summary>
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
/// NKLI     : Nigiri - SVO Voxelization CGINC 
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

// Include
#include "NKLI_Nigiri_SVONode.cginc"


#define LUMA_THRESHOLD_FACTOR 0.01f // Higher = higher accuracy with higher flickering
#define LUMA_DEPTH_FACTOR 100.0f 	// Higher = lesser variation with depth
#define LUMA_FACTOR 1.9632107f

struct SplitRequest
{
    uint offset;
    uint TTL;
};


/// <summary>
/// Returns position within a grid, given specific resolution and area size.
/// </summary>
inline uint3 GetGridPosition(float4 worldPosition, uint resolution, uint giAreaSize)
{      
    float3 encodedPosition = worldPosition.xyz / giAreaSize;
    encodedPosition += float3(1.0f, 1.0f, 1.0f);
    encodedPosition /= 2.0f;
    return (uint3) (encodedPosition * resolution);
}

/// <summary>
/// Returns mixed colour for insertion
/// </summary>
inline float4 GetNewMixedColour(uint2 orderedCoord, Texture2D<float4> lightingTexture, Texture2D<float4> lightingTexture2, float emissiveIntensity, float shadowStrength, float occlusionGain)
{  
    return float4((
    max(lightingTexture[orderedCoord].rgb * emissiveIntensity, lightingTexture2[orderedCoord].rgb * (1 - shadowStrength).xxx)),
    lightingTexture2[orderedCoord].a * occlusionGain);
}

/// <summary>
/// Returns the node offset calculated from spatial relation
/// </summary>
inline uint GetSVOBitOffset(float3 tX, float3 tM)
{
    uint nextIndex = 0;
    if (tX.x > tM.x)
        nextIndex = nextIndex | (1);
    if (tX.y > tM.y)
        nextIndex = nextIndex | (1 << 1);
    if (tX.z > tM.z)
        nextIndex = nextIndex | (1 << 2);
    
    return nextIndex;
}

// Function to get the luma value of the input color
inline float GetLuma(float3 inputColor)
{
    return ((inputColor.y * LUMA_FACTOR) + inputColor.x);
}

/// <summary>
/// Writes colour value to node
/// </summary>
inline SVONode SetNodeColour(SVONode node, float4 colour, float depth)
{
    // Calculate depth based luma threshold (decreases with increasing depth)
    float lumaThreshold = LUMA_THRESHOLD_FACTOR * (1.0f / max(depth * LUMA_DEPTH_FACTOR, 0.1f));
    
    // Find the current pixel's luma
    float pixelLuma = GetLuma(colour.rgb);
    
    // Calculate difference between voxel and pixel luma
    float currentVoxelLuma = GetLuma(node.UnPackColour().rgb);
    float lumaDiff = saturate(currentVoxelLuma - pixelLuma);
    
    // Only inject if currently voxel is either 1. unoccupied or 2. of a lesser depth and passes luma test
    if ((node.colour_A == 0.0f) || ((depth < node.colour_A) && (lumaDiff < lumaThreshold)))
    {
        // Set values
        node.PackColour(colour);       
    }
    
    // return node
    return node;
}

/// <summary>
/// Appends to the queue of nodes to be split
/// </summary>
inline void AppendSVOSplitQueue(RWStructuredBuffer<uint> queueBuffer, RWStructuredBuffer<uint> counterBuffer, uint offset)
{   
    // Only if within bounds
    if (counterBuffer[2] < counterBuffer[1])
    {
        //  Get write index
        uint index_SplitQueue;
        InterlockedAdd(counterBuffer[2], 1, index_SplitQueue);
         
        // Append to split queue it within bounds
        //  offset is +1 because zero signifies null value
        if (index_SplitQueue < counterBuffer[1])
            queueBuffer[index_SplitQueue] = offset;
    }
}

/// <summary>
/// Appends upto 8 nodes to the split queue without duplicates
/// </summary>
void DeDupeAppendSplitQueue(uint thread, SplitRequest splits[64], RWStructuredBuffer<uint> _SVO_SplitQueue, RWStructuredBuffer<uint> _SVO_Counters)
{
    //uint seenOffset[4];
    //uint seenCount = 0;
    uint highestTTL = 0;
        
    // Search thread group for highest offset
    int row;
    for (row = 0; row < 64; row++)
    {
        if (splits[row].offset != 0)
        {
            if (splits[row].TTL >= highestTTL)
            {
                if (splits[row].TTL > highestTTL)
                    highestTTL = splits[row].TTL;
                    
                // Append to the node split queue
                AppendSVOSplitQueue(_SVO_SplitQueue, _SVO_Counters, splits[row].offset);
                
                /*uint seen = 0;
                int rowSeen;
                for (rowSeen = 0; rowSeen < 4; rowSeen++)
                {
                    if (splits[row].offset == seenOffset[rowSeen])
                    {
                        if (row != 0)
                            seen = 1;
                    }
                }
                if (seen == 0)
                {

                    // Append to the node split queue
                    AppendSVOSplitQueue(_SVO_SplitQueue, _SVO_Counters, splits[row].offset);
                
                    if (splits[row].TTL > highestTTL)
                    {
                        highestTTL = splits[row].TTL;
                        seenCount = 0;
                    }
                    
                    // Append to list of seen offsets
                    seenOffset[seenCount] = splits[row].offset;
                    seenCount++;
                    
                    if (seenCount > 3)
                        seenCount = 0;
                
                }*/
            }
        }
    }
}

/// <summary>
/// Traverses the SVO, either queueing nodes for splitting or writing out new colour
/// </summary>
SplitRequest SplitInsertSVO(RWStructuredBuffer<SVONode> svoBuffer, RWStructuredBuffer<uint> queueBuffer, uniform RWStructuredBuffer<uint> counterBuffer,
    float4 worldPosition, float4 colour, float depth, float giAreaSize)
{
    /// Calculate initial values
    // AABB Min/Max x,y,z
    float halfArea = giAreaSize / 2;
    float3 t0 = float3(-halfArea, -halfArea, -halfArea);
    float3 t1 = float3(halfArea, halfArea, halfArea);  
    
    SplitRequest split;
    
    // Traverse tree
    uint currentDepth = 0;
    uint offset = 0;
    uint emergencyExit = 0;
    while (true)
    {
        // Ejector seat
        emergencyExit++;
        if (emergencyExit > 8192)
        {
            split.offset = 0;
            split.TTL = 0;
            return split;
        }
                
        // Unpack node
        SVONode node = svoBuffer[offset];
        uint bitfieldOccupancy;
        uint runLength;
        uint ttl;
        uint isLeaf;
        node.UnPackStruct(bitfieldOccupancy, runLength, ttl, isLeaf);
                
        // At max depth we just write out the voxel and quit
        if (ttl == 0)
        {                     
            // Write back to buffer
            // TODO - This is not threadsafe and will result in a
            //          race condition characterized by flicking GI
            //          This will be replaced with an atomic rolling
            //          average to fix this problem in the future
            svoBuffer[offset] = SetNodeColour(node, colour, depth);
                       
            // We're done here
            split.offset = 0;
            split.TTL = 0;
            return split;
        }
        else
        {
            // If no children then tag for split queue consideration
            if (node.referenceOffset == 0)
            {       
                // Just here for debugging purposes
                float4 newColour = lerp(
                    node.UnPackColour(),
                    float4(colour.r, colour.g, colour.b, colour.a), 0.05f);
                
                float mono = (newColour.r + newColour.g + newColour.b) / 6.0f;
                
                svoBuffer[offset] = SetNodeColour(node, float4(mono, mono, mono, newColour.a), depth);
                
                split.offset = offset + 1;
                split.TTL = ttl;
                return split;
            }
            else
            {           
                // Middle of node coordiates
                float3 tM = float3(0.5 * (t0.x + t1.x), 0.5 * (t0.y + t1.y), 0.5 * (t0.z + t1.z));
                
                // Child node offset index
                uint childIndex = GetSVOBitOffset(worldPosition.xyz, tM);
                
                // Set extents of child node
                switch (childIndex)
                {
                    case 0:
                        t0 = float3(t0.x, t0.y, t0.z);
                        t1 = float3(tM.x, tM.y, tM.z);
                        break;
                    case 4:
                        t0 = float3(t0.x, t0.y, tM.z);
                        t1 = float3(tM.x, tM.y, t1.z);
                        break;
                    case 2:
                        t0 = float3(t0.x, tM.y, t0.z);
                        t1 = float3(tM.x, t1.y, tM.z);
                        break;
                    case 6:
                        t0 = float3(t0.x, tM.y, tM.z);
                        t1 = float3(tM.x, t1.y, t1.z);
                        break;
                    case 1:
                        t0 = float3(tM.x, t0.y, t0.z);
                        t1 = float3(t1.x, tM.y, tM.z);
                        break;
                    case 5:
                        t0 = float3(tM.x, t0.y, tM.z);
                        t1 = float3(t1.x, tM.y, t1.z);
                        break;
                    case 3:
                        t0 = float3(tM.x, tM.y, t0.z);
                        t1 = float3(t1.x, t1.y, tM.z);
                        break;
                    case 7:
                        t0 = float3(tM.x, tM.y, tM.z);
                        t1 = float3(t1.x, t1.y, t1.z);
                        break;
                }
                
                // Offet is reference + the node offset index
                offset = node.referenceOffset + childIndex;
                
                // We setup to search next depth
                currentDepth++;
            }
        }
    }
}