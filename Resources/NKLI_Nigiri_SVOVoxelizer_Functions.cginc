/// <summary>
/// NKLI     : Nigiri - SVO Voxelization CGINC 
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

// Include
#include "NKLI_Nigiri_SVONode.cginc"

/// <summary>
/// Returns position within a grid, given specific resolution and area size.
/// </summary>
inline uint3 GetGridPosition(float4 worldPosition, uint resolution, uint giAreaSize)
{   
    float3 encodedPosition = worldPosition / giAreaSize;
    encodedPosition += float3(1.0f, 1.0f, 1.0f);
    encodedPosition /= 2.0f;
    uint3 voxelPosition = (uint3) (encodedPosition * resolution);
    return voxelPosition;
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
inline uint GetSVOBitOffset(uint3 index3D, uint resolution)
{
    // Lazy caculation of node offset
    // TODO - This can be condensed into single operaion
    uint nextIndex = 0;
    uint halfWidth = resolution / 2;
    if (index3D.x > halfWidth)
        nextIndex = nextIndex | (1);
    if (index3D.y > halfWidth)
        nextIndex = nextIndex | (1 << 1);
    if (index3D.z > halfWidth)
        nextIndex = nextIndex | (1 << 2);
    
    return nextIndex;
}

/// <summary>
/// Writes colour value to node
/// </summary>
inline SVONode SetNodeColour(SVONode node, float4 colour)
{
    // Set values
    node.value_A = lerp(colour.a, node.value_A, 0.999);
    node.value_R = lerp(colour.r, node.value_R, 0.999);
    node.value_G = lerp(colour.g, node.value_G, 0.999);
    node.value_B = lerp(colour.b, node.value_B, 0.999);
    
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
        // Lazy test against filling with runs of identical offsets
        if ((queueBuffer[counterBuffer[2]] + 1) != (offset + 1))
        {
            //  Get write index
            uint index_SplitQueue;
            InterlockedAdd(counterBuffer[2], 1, index_SplitQueue);
         
            // Append to split queue it within bounds
            //  offset is +1 because zero signifies null value
            if (index_SplitQueue < counterBuffer[1])
                queueBuffer[index_SplitQueue] = offset + 1;
        }
    }
}

/// <summary>
/// Traverses the SVO, either queueing nodes for splitting or writing out new colour
/// </summary>
void SplitInsertSVO(RWStructuredBuffer<SVONode> svoBuffer, RWStructuredBuffer<uint> queueBuffer, uniform RWStructuredBuffer<uint> counterBuffer, 
    float4 worldPosition, float4 colour, float giAreaSize)
{
    // Traverse tree
    uint maxDepth = counterBuffer[0];
    uint currentDepth = 0;
    uint done = 0;
    uint offset = 0;
    uint emergencyExit = 0;
    while (done == 0)
    {
        // Ejector seat
        emergencyExit++;
        if (emergencyExit > 8192)
            return;
        
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
            svoBuffer[offset] = SetNodeColour(node, colour);
                       
            // We're done here
            done = 1;
        }
        else
        {
            // At depth 0, we take default values
            //  currentDepth = 0
            //  resolution = 1
            
            if (node.referenceOffset == 0)
            {
                // Here we split the node
                AppendSVOSplitQueue(queueBuffer, counterBuffer, offset);
                
                // Just here for debugging purposes
                svoBuffer[offset] = SetNodeColour(node, colour);
                
                // We're done here
                done = 1;
            }
            else
            {               
                // Resolution is depth to the power of 4
                uint resolution = pow(currentDepth, 2);
                
                // Offet is reference + the node offset index
                offset = node.referenceOffset + GetSVOBitOffset(GetGridPosition(worldPosition, resolution, giAreaSize), resolution);
                //offset = node.referenceOffset + GetSVOBitOffset(worldPosition * resolution, resolution);
                
                // We setup to search next depth
                currentDepth++;
            }
        }
    }
}