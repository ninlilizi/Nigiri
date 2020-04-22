struct subtree_Struct
{
    
    float3 t0;
    float3 t1;
    uint referenceOffset;
};

struct result_Struct
{
    uint isLeaf;
    
    uint isOccupied[9];
    
    float3 t0[9];
    
    float3 t1[9];
    
    uint referenceOffset[9];
};

// #define STACK_DECLARE
// #define STACK_SETUP uint StackPosition = 0; subtree_Struct stackBuffer[256];
// #define STACK_PUSH(a) stackBuffer[StackPosition++] = a;
// #define STACK_POP stackBuffer[--StackPosition]
// #define STACK_HAS_DATA (StackPosition >0)

result_Struct Create_result_Struct()
{
    result_Struct output;
    
    output.isLeaf = 0;
    
    output.isOccupied[0] = 0;
    output.isOccupied[1] = 0;
    output.isOccupied[2] = 0;
    output.isOccupied[3] = 0;
    output.isOccupied[4] = 0;
    output.isOccupied[5] = 0;
    output.isOccupied[6] = 0;
    output.isOccupied[7] = 0;
    output.isOccupied[8] = 0;
    
    output.t0[0] = (0).xxx;
    output.t0[1] = (0).xxx;
    output.t0[2] = (0).xxx;
    output.t0[3] = (0).xxx;
    output.t0[4] = (0).xxx;
    output.t0[5] = (0).xxx;
    output.t0[6] = (0).xxx;
    output.t0[7] = (0).xxx;
    output.t0[8] = (0).xxx;
    
    output.t1[0] = (0).xxx;
    output.t1[1] = (0).xxx;
    output.t1[2] = (0).xxx;
    output.t1[3] = (0).xxx;
    output.t1[4] = (0).xxx;
    output.t1[5] = (0).xxx;
    output.t1[6] = (0).xxx;
    output.t1[7] = (0).xxx;
    output.t1[8] = (0).xxx;
    
    
    output.referenceOffset[0] = 0;
    output.referenceOffset[1] = 0;
    output.referenceOffset[2] = 0;
    
    output.referenceOffset[3] = 0;
    output.referenceOffset[4] = 0;
    output.referenceOffset[5] = 0;
    
    output.referenceOffset[6] = 0;
    output.referenceOffset[7] = 0;
    output.referenceOffset[8] = 0;

    
    return output;
}

inline void AddChildToResult(float tx0, float ty0, float tz0, float txm, float tym, float tzm, uint offset, uint nodeOffset, SVONode node, uint childCount, inout result_Struct result)
{
    // Only append the node if occupancy bit is set
    if (node.GetOccupancyBit(nodeOffset));
    {
        result.t0[childCount] = float3(tx0, ty0, tz0);
        result.t1[childCount] = float3(txm, tym, tzm);
        result.referenceOffset[childCount] = offset + nodeOffset;
        result.isOccupied[childCount] = 1;
    }
}


uint first_node(double tx0, double ty0, double tz0, double txm, double tym, double tzm){
    int answer = 0;	// 00000000
    double maximum = max(max(tx0,ty0),tz0);

    if(maximum == tx0){ // PLANE YZ
        if(tym < tx0) answer|=2;	// set bit at position 1
        if(tzm < tx0) answer|=1;	// set bit at position 0 			
        return answer; 		
    } 

    if(maximum == ty0){ // PLANE XZ
        if(txm < ty0) answer|=4;	// set bit at position 2
        if(tzm < ty0) answer|=1;	// set bit at position 0			
        return answer; 		
    } 
    
    if(maximum == tz0){ // PLANE XY
        if(txm < tz0) answer|=4;	// set bit at position 2
        if(tym < tz0) answer|=2;	// set bit at position 1
        return answer;	
    }

    return answer;
}

uint new_node(double txm, int x, double tym, int y, double tzm, int z){
    if(txm < tym){
        if(txm < tzm){return x;}  // YZ plane
    }
    else{
        if(tym < tzm){return y;} // XZ plane
    }
    return z; // XY plane;
}



result_Struct proc_subtree(float tx0, float ty0, float tz0, float tx1, float ty1, float tz1, SVONode node, uint a)
{
    result_Struct result = Create_result_Struct();
    
    float txm, tym, tzm;
    int currNode;

    if (tx1 <= 0 || ty1 <= 0 || tz1 <= 0)
    return result;
    if (!node.GetTTL())
    {
        //cout << "Reached leaf node " << node - > debug_ID << endl;
        
        //result.t0[0] = float3(tx0, ty0, tz0);
        //result.t1[0] = float3(txm, tym, tzm);
        //result.referenceOffset[0] = node.referenceOffset;
        result.isLeaf = 1;
        return result;
    }
    else if (!node.referenceOffset)
    {
        //cout << "Reached node " << node - > debug_ID << endl;
        
        /*if (node.colour_A)
        {
            //result.t0[0] = float3(tx0, ty0, tz0);
            //result.t1[0] = float3(txm, tym, tzm);
            //result.referenceOffset[0] = node.referenceOffset;
            result.isLeaf = 1;
            return result;
        }*/
        

        
        
        //result.isLeaf = 1;
        return result;
    }

    txm = 0.5 * (tx0 + tx1);
    tym = 0.5 * (ty0 + ty1);
    tzm = 0.5 * (tz0 + tz1);
    
    currNode = first_node(tx0, ty0, tz0, txm, tym, tzm);
    for (uint childCount = 0; childCount < 9; childCount++)
    {
        //subtree_Struct subtree;
        
        switch (currNode)
        {
            case 0:
            //proc_subtree(tx0, ty0, tz0, txm, tym, tzm, node - > children[a]);
            AddChildToResult(tx0, ty0, tz0, txm, tym, tzm, node.referenceOffset, a, node, childCount, result);
            currNode = new_node(txm, 4, tym, 2, tzm, 1);
            break;
            case 1:
            //proc_subtree(tx0, ty0, tzm, txm, tym, tz1, node - > children[1 ^ a]);
            AddChildToResult(tx0, ty0, tzm, txm, tym, tz1, node.referenceOffset, (1 ^ a), node, childCount, result);
            currNode = new_node(txm, 5, tym, 3, tz1, 8);
            break;
            case 2:
            //proc_subtree(tx0, tym, tz0, txm, ty1, tzm, node - > children[2 ^ a]);
            AddChildToResult(tx0, tym, tz0, txm, ty1, tzm, node.referenceOffset, (2 ^ a), node, childCount, result);
            currNode = new_node(txm, 6, ty1, 8, tzm, 3);
            break;
            case 3:
            //proc_subtree(tx0, tym, tzm, txm, ty1, tz1, node - > children[3 ^ a]);
            AddChildToResult(tx0, tym, tzm, txm, ty1, tz1, node.referenceOffset, (3 ^ a), node, childCount, result);
            currNode = new_node(txm, 7, ty1, 8, tz1, 8);
            break;
            case 4:
            //proc_subtree(txm, ty0, tz0, tx1, tym, tzm, node - > children[4 ^ a]);
            AddChildToResult(txm, ty0, tz0, tx1, tym, tzm, node.referenceOffset, (4 ^ a), node, childCount, result);
            currNode = new_node(tx1, 8, tym, 6, tzm, 5);
            break;
            case 5:
            //proc_subtree(txm, ty0, tzm, tx1, tym, tz1, node - > children[5 ^ a]);
            AddChildToResult(txm, ty0, tzm, tx1, tym, tz1, node.referenceOffset, (5 ^ a), node, childCount, result);
            currNode = new_node(tx1, 8, tym, 7, tz1, 8);
            break;
            case 6:
            //proc_subtree(txm, tym, tz0, tx1, ty1, tzm, node - > children[6 ^ a]);
            AddChildToResult(txm, tym, tz0, tx1, ty1, tzm, node.referenceOffset, (6 ^ a), node, childCount, result);
            currNode = new_node(tx1, 8, ty1, 8, tzm, 7);
            break;
            case 7:
            //proc_subtree(txm, tym, tzm, tx1, ty1, tz1, node - > children[7 ^ a]);
            AddChildToResult(txm, tym, tzm, tx1, ty1, tz1, node.referenceOffset, (7 ^ a), node, childCount, result);
            currNode = 8;
            break;
            case 8:
            return result;
            break;
        }
    }
    //result.childCount = childCount + 1;
    
    return result;
}



bool SVOIntersection(Ray ray, out RayHit hit , out float4 color)
{
    bool ris = false;
    float3 p;
    
    /// Calculate initial values
    // AABB Min/Max x,y,z
    float halfArea = _giAreaSize / 2;
    grid_min = (-halfArea).xxx;
    grid_max = (halfArea).xxx;
    
    float octreeCenter[3] = { 0, 0, 0};
    
    //check if ray origin is inside the voxel grid
    if (point_inside_box(ray.origin, grid_min, grid_max))
    {
        p = ray.origin;
    }
    else //the origin is not in the grid, check if the ray intersects the grid
    {
        float tmin, tmax;
        float3 aabb[2] = { grid_min, grid_max };

        ray_box_intersection(ray, aabb, tmin, tmax);

        if (tmin > tmax)  //no scene intersection
        {
            return false;
        }
        else
        {
            //p = ray.origin + tmin * ray.direction;
        }
    }
    //ray.origin +=camera_position;
    
    uint a = 0;

    // fixes for rays with negative direction
    if (ray.direction[0] < 0)
    {
        ray.origin[0] = octreeCenter[0] * 2 - ray.origin[0];
        ray.direction[0] = -ray.direction[0];
        a |= 4; //bitwise OR (latest bits are XYZ)
    }
    if (ray.direction[1] < 0)
    {
        ray.origin[1] = octreeCenter[1] * 2 - ray.origin[1];
        ray.direction[1] = -ray.direction[1];
        a |= 2;
    }
    if (ray.direction[2] < 0)
    {
        ray.origin[2] = octreeCenter[2] * 2 - ray.origin[2];
        ray.direction[2] = -ray.direction[2];
        a |= 1;
    }
    
    
    float divx = 1 / ray.direction.x; // IEEE stability fix
    float divy = 1 / ray.direction.y;
    float divz = 1 / ray.direction.z;
    
    float tx0 = (grid_min.x - ray.origin[0]) * divx;
    float tx1 = (grid_max.x - ray.origin[0]) * divx;
    float ty0 = (grid_min.y - ray.origin[1]) * divy;
    float ty1 = (grid_max.y - ray.origin[1]) * divy;
    float tz0 = (grid_min.z - ray.origin[2]) * divz;
    float tz1 = (grid_max.z - ray.origin[2]) * divz;
    color = float4(0,0,0,0);
    
    //STACK_SETUP;
    //RINGBUFFER_SETUP;
    
    uint RingBuffer_MaxCount = 32;
    subtree_Struct RingBuffer[32];
    uint RingBuffer_ReadIndex = 0;
    uint RingBuffer_WriteIndex = 0;
    uint RingBuffer_Count = 0;
    
    
    subtree_Struct rootNode;
    rootNode.t0 = float3(tx0, ty0, tz0);
    rootNode.t1 = float3(tx1, ty1, tz1);
    rootNode.referenceOffset = 0;
    //STACK_PUSH(rootNode);
    //RINGBUFFER_WRITE(rootNode);
    
    RingBuffer[RingBuffer_WriteIndex] = rootNode;
    
    RingBuffer_WriteIndex++;
    RingBuffer_Count++;

    if (RingBuffer_WriteIndex == RingBuffer_MaxCount)
    RingBuffer_WriteIndex = 0;
    
    
    if (max(max(tx0, ty0), tz0) < min(min(tx1, ty1), tz1))
    {
        //result_Struct result = proc_subtree(tx0, ty0, tz0, tx1, ty1, tz1, _SVO[0], a);
        float min_t = 9999;
        uint intersected = 0;
        
        //while (STACK_HAS_DATA)
        while (RingBuffer_Count > 0)
        {
            //subtree_Struct subtree = STACK_POP;
            //subtree_Struct subtree = RINGBUFFER_READ;
            
            //Fetch next node from the buffer
            subtree_Struct subtree = RingBuffer[RingBuffer_ReadIndex];
            
            RingBuffer_ReadIndex++;
            RingBuffer_Count--;
            
            if (RingBuffer_ReadIndex == RingBuffer_MaxCount)
            RingBuffer_ReadIndex = 0;

            
            result_Struct result = proc_subtree(subtree.t0.x, subtree.t0.y, subtree.t0.z, subtree.t1.x, subtree.t1.y, subtree.t1.z, _SVO[subtree.referenceOffset], a);
            
            /*if (result.isLeaf)
            {
                float tmin, tmax;
                float3 aabb[2] = { subtree.t0, subtree.t1 };

                ray_box_intersection(ray, aabb, tmin, tmax);
                
                float t = distance(ray.origin, tmin);
                
                if (tmin < t)
                {
                    min_t = tmin;
                    //min_b = b;
                    //min_tris_index = tris_index;
                    
                    intersected = 1;
                }
            }*/
            
            if (result.isLeaf)
            {
                SVONode localNode = _SVO[subtree.referenceOffset];
                
                float tmin, tmax;
                float3 aabb[2] = { subtree.t0, subtree.t1 };

                ray_box_intersection(ray, aabb, tmin, tmax);
                float center = (tmax - tmin) * 0.5;
                
                float t = distance(ray.origin, center + tmin);                
                color = localNode.UnPackColour();

                return true;
            }

            
            for (uint ri = 0; ri < 9; ri += 1)
            {
                if (result.isOccupied[ri])
                {
                    subtree_Struct subtree;
                    subtree.t0 = result.t0[ri];
                    subtree.t1 = result.t1[ri];
                    subtree.referenceOffset = result.referenceOffset[ri];
                    //STACK_PUSH(subtree);
                    //RINGBUFFER_WRITE(subtree);
                    
                    if (RingBuffer_MaxCount - RingBuffer_Count)
                    {
                        RingBuffer[RingBuffer_WriteIndex] = subtree;
                        
                        RingBuffer_WriteIndex++;
                        RingBuffer_Count++;

                        if (RingBuffer_WriteIndex == RingBuffer_MaxCount)
                        RingBuffer_WriteIndex = 0;
                    }


                }
            }
        }
    }
    

    hit = CreateRayHit();
    
    // For now. Obv change later
    return false;
}
