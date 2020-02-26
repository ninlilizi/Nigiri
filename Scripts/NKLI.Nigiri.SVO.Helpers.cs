/// <summary>
/// NKLI     : Nigiri - SVO Helpers
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;

namespace NKLI.Nigiri.SVO
{
    /// <summary>
    /// Helper functions
    /// </summary>
    public class Helpers
    {
        // Calculates occupancy bitmap from int[8] array
        public static uint GetOccupancyBitmap(uint[] values)
        {
            return
                (Math.Min(values[0], 1) << 7) |
                (Math.Min(values[1], 1) << 6) |
                (Math.Min(values[2], 1) << 5) |
                (Math.Min(values[3], 1) << 4) |
                (Math.Min(values[4], 1) << 3) |
                (Math.Min(values[5], 1) << 2) |
                (Math.Min(values[6], 1) << 1) |
                (Math.Min(values[7], 1) & 1);

        }

        // Finds current depth from boundary array
        public static uint GetDepthFromBoundaries(uint index, uint treeDepth, uint[] boundaries)
        {
            // TODO - Make this efficient (LUT, etc)
            for (uint j = Tree.boundariesOffsetU; j < (treeDepth + Tree.boundariesOffsetU); j++)
            {
                if (index < boundaries[j])
                    return j - Tree.boundariesOffsetU;

            }
            // Return of 999 designates error
            return 999;
        }

        // Calculate thread count
        public static uint GetThreadCount(uint gridWidth, uint treeDepth, out uint[] counter_Boundaries)
        {
            // Get depth of tree
            counter_Boundaries = new uint[Tree.boundariesOffset + treeDepth];

            // Start at max depth -1
            int cycles = Tree.boundariesOffset + 1;
            // max cycles to run
            int maxCycles = Convert.ToInt32(Tree.boundariesOffset + treeDepth);
            // Starting value is thick buffer size for leaf nodes
            uint threadCount = (gridWidth * gridWidth * gridWidth) / 8;

            // Starting value
            uint nodeCount = threadCount;

            // First boundary is thick buffer size
            counter_Boundaries[Tree.boundariesOffset] = threadCount;

            // Do the work
            while (maxCycles > cycles)
            {
                // Divide by 8 to get the thread count
                nodeCount = Math.Max(nodeCount / 8, 8);

                // Tabulate the sum
                threadCount += nodeCount;

                // Add depth boundary index to array
                counter_Boundaries[cycles] = threadCount;

                // Increment counter
                cycles++;
            }
            return threadCount;
        }

        // Calculate thread count
        public static uint GetNodeCount(uint _nodeCount, uint gridWidth, uint treeDepth)
        {
            // Assign local
            uint cycles = 0;
            uint finalNodeCount = 0;

            // Root depth only gathered from so less threads needed
            uint nodeCount = (uint)(Math.Ceiling(_nodeCount / 8.0d) * 8);

            // Do the work
            while (treeDepth > cycles)
            {
                // Divide by 8 to get the thread count
                nodeCount = Math.Max(nodeCount / 8, 1);

                // Tabulate the sum
                finalNodeCount += nodeCount;

                // Increment counter
                cycles++;
            }
            return finalNodeCount;
        }

        // Calculate depth of tree
        public static uint GetDepth(uint gridWidth)
        {
            uint depth = 0;
            while (gridWidth > 1)
            {
                depth++;
                gridWidth /= 2;
            }
            return depth;
        }
    }
}
