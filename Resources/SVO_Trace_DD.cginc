
#include "Utils.cginc"


// Strength of the direct lighting
uniform float					_DirectStrength;

// Strength of the ambient lighting
uniform float					_AmbientLightingStrength;

// Strength of the indirect specular lighting
uniform float					_IndirectSpecularStrength;

// Strength of the indirect diffuse lighting
uniform float					_IndirectDiffuseStrength;

// Maximum number of iterations in indirect specular cone tracing pass
uniform float					_MaximumIterations;

// Step mulitplier for the cone tracing step
uniform float					_StepMultiplier;

// Step value for indirect specular cone tracing pass
uniform float					_ConeStep;

// Angle for the cone tracing step in indirect diffuse lighting
uniform float					_ConeAngle;

// Offset value for indirect specular cone tracing pass
uniform float					_ConeOffset;

// Step value used for blurring
uniform float					_BlurStep;

// Current timestamp for voxel information
uniform int						_CurrentTimestamp;


/// <summary>
/// Returns the node offset calculated from spatial relation
/// </summary>
inline uint GetSVOBitOffset(float3 tX, float3 tM)
{
    uint nextIndex = 0;
    if (tX.x > tM.x)
    nextIndex |= 4;
    if (tX.y > tM.y)
    nextIndex |= 2;
    if (tX.z > tM.z)
    nextIndex |= 1;

    return nextIndex;
}



inline float4 GetVoxelInfoSVO(float3 worldPosition, uint ttl)
{
    /// Calculate initial values
    // AABB Min/Max x,y,z
    float halfArea = _giAreaSize / 2;
    float3 t0 = float3(-halfArea, -halfArea, -halfArea);
    float3 t1 = float3(halfArea, halfArea, halfArea);


    if ((worldPosition.x < t0.x || worldPosition.x > t1.x) || (worldPosition.y < t0.y || worldPosition.y > t1.y) || (worldPosition.z < t0.z || worldPosition.z > t1.z))
    {
        // We're done here
        return (0).xxxx;
    }



    float4 tempColour = (0).xxxx;
    float4 tempColour2 = (0).xxxx;

    // Traverse tree
    uint offset = 0;
    uint colourCount = 0;
    while (true)
    {
        // Retrieve node
        SVONode node = _SVO[offset];

        // TODO - Add noise threshold slider to inspector
        //			Noise threashold controlled by
        //			adjusting the 0.5
        
        uint nodeTTL = node.GetTTL();
        if (node.colour_A){
            colourCount++;
            tempColour2 += node.UnPackColour();
        }



        // If no children then tag for split queue consideration
        if (!node.referenceOffset || (nodeTTL == ttl))
        {
            // If no child then return colour
            //return tempColour / colourCount;
            //colourCount++;
            tempColour += node.UnPackColour();
            // if(colourCount>0.9)
            //tempColour += tempColour2/colourCount;
            return tempColour2;///colourCount;
            //return node.UnPackColour();
            //return float4(node.UnPackNormal(),1);
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
                case 1:
                t0 = float3(t0.x, t0.y, tM.z);
                t1 = float3(tM.x, tM.y, t1.z);
                break;
                case 2:
                t0 = float3(t0.x, tM.y, t0.z);
                t1 = float3(tM.x, t1.y, tM.z);
                break;
                case 3:
                t0 = float3(t0.x, tM.y, tM.z);
                t1 = float3(tM.x, t1.y, t1.z);
                break;
                case 4:
                t0 = float3(tM.x, t0.y, t0.z);
                t1 = float3(t1.x, tM.y, tM.z);
                break;
                case 5:
                t0 = float3(tM.x, t0.y, tM.z);
                t1 = float3(t1.x, tM.y, t1.z);
                break;
                case 6:
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

            // SVONode node = _SVO[offset];
            // tempColour2 += node.UnPackColour();
            // colourCount++;
        }
    }
    if(colourCount>0.9)
    tempColour += tempColour2/colourCount;
    return tempColour/colourCount;
    //float4(0,0,0,0);
}

