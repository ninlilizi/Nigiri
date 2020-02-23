using System;
using System.Collections;
using System.Collections.Generic;
using NKLI.Nigiri.SVO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Nigiri.SVO
{
    #region Test_SVOHelper
    public class Test_SVOHelper
    {
        [Test]
        // Test occupancy bitmap calculation - position 0
        public void GetOccupancyBitmap_Position()
        {
            // Position 0
            uint[] testValues = new uint[8];

            testValues[0] = 5;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_1000_0000);

            testValues[0] = 0;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 1
            testValues = new uint[8];

            testValues[1] = 5;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0100_0000);

            testValues[1] = 0;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 2
            testValues = new uint[8];

            testValues[2] = 5;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0010_0000);

            testValues[2] = 0;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 3
            testValues = new uint[8];

            testValues[3] = 5;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0001_0000);

            testValues[3] = 0;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 4
            testValues = new uint[8];

            testValues[4] = 5;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_1000);

            testValues[4] = 0;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 5
            testValues = new uint[8];

            testValues[5] = 5;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0100);

            testValues[5] = 0;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 6
            testValues = new uint[8];

            testValues[6] = 5;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0010);

            testValues[6] = 0;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 7
            testValues = new uint[8];

            testValues[7] = 5;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0001);

            testValues[7] = 0;
            Assert.AreEqual(SVOHelper.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);
        }

        [Test]
        // Test depth calculation from boundary array
        public void GetThreadCount()
        {
            // Calculate control data
            int gridWidth = 256;
            int voxelCount = gridWidth * gridWidth * gridWidth;
            int treeDepth = SVOHelper.GetDepth(gridWidth);
            int threadCount = SVOHelper.GetThreadCount(gridWidth, treeDepth, out int[] boundaries);

            Assert.AreEqual(treeDepth, 8);
            Debug.Log("<Unit Test> (GetDepthFromBoundaries) gridWidth:" + gridWidth + ", voxelCount:" + voxelCount + ", threadCount:" + threadCount + ", treeDepth:" + treeDepth);

            int index = UnityEngine.Random.Range(0, threadCount);

            // Control data
            int[] control = new int[8];
            control[0] = voxelCount / 8;
            control[1] = control[0] + (voxelCount / 8 / 8);
            control[2] = control[1] + (voxelCount / 8 / 8 / 8);
            control[3] = control[2] + (voxelCount / 8 / 8 / 8 / 8);
            control[4] = control[3] + (voxelCount / 8 / 8 / 8 / 8 / 8);
            control[5] = control[4] + (voxelCount / 8 / 8 / 8 / 8 / 8 / 8);
            control[6] = control[5] + (voxelCount / 8 / 8 / 8 / 8 / 8 / 8 / 8);
            control[7] = control[6] + (voxelCount / 8 / 8 / 8 / 8 / 8 / 8 / 8 / 8);

            for (uint i = 0; i < treeDepth; i++)
            {
                // Test the boundary array
                Debug.Log("<Unit Test> (GetDepthFromBoundaries) Control " + i + ":" + control[i] + ", Boundary " + i + ":" + boundaries[i]);
                Assert.AreEqual(control[i], boundaries[i]);
            }
        }

        [Test]
        // Test Calculation of tree depth
        public void GetDepth()
        {
            Assert.AreEqual(SVOHelper.GetDepth(16), 4);
            Assert.AreEqual(SVOHelper.GetDepth(32), 5);
            Assert.AreEqual(SVOHelper.GetDepth(64), 6);
            Assert.AreEqual(SVOHelper.GetDepth(128), 7);
            Assert.AreEqual(SVOHelper.GetDepth(256), 8);
            Assert.AreEqual(SVOHelper.GetDepth(512), 9);
            Assert.AreEqual(SVOHelper.GetDepth(1024), 10);
            Assert.AreEqual(SVOHelper.GetDepth(2048), 11);
            Assert.AreEqual(SVOHelper.GetDepth(4096), 12);
            Assert.AreEqual(SVOHelper.GetDepth(8192), 13);
            Assert.AreEqual(SVOHelper.GetDepth(16384), 14);
            Assert.AreEqual(SVOHelper.GetDepth(32768), 15);
            Assert.AreEqual(SVOHelper.GetDepth(65536), 16);
        }
    }
    #endregion

    #region Test_SVONode
    public class Test_SVONode
    {
        // Test PackStruct bitfield occupancy
        [Test]
        public void PackStruct_BitfieldOccupancy()
        {
            uint control = 0b_1111_1111_0000_0000_0000_0000_0000_0000;
            uint sample  = 0b_0000_0000_0000_0000_0000_0000_1111_1111;

            SVONode SVONode = new SVONode(0, sample, 0, 0, false);
            //Debug.Log($"<Unit Test> (PackStruct_BitfieldOccupancy) Control:  {Convert.ToString(control, toBase: 2)}");
            //Debug.Log($"<Unit Test> (PackStruct_BitfieldOccupancy) Sample:  {Convert.ToString(SVONode.packedBitfield, toBase: 2)}");

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test PackStruct run length
        [Test]
        public void PackStruct_RunLength()
        {
            uint control = 0b_0000_0000_1111_0000_0000_0000_0000_0000;
            uint sample = 0b_0000_0000_0000_0000_0000_0000_0000_1111;

            SVONode SVONode = new SVONode(0, 0, sample, 0, false);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test PackStruct octree depth
        [Test]
        public void PackStruct_OctreeDepth()
        {
            uint control = 0b_0000_0000_0000_1111_0000_0000_0000_0000;
            uint sample = 0b_0000_0000_0000_0000_0000_0000_0000_1111;

            SVONode SVONode = new SVONode(0, 0, 0, sample, false);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test PackStruct is leaf
        [Test]
        public void PackStruct_IsLeaf()
        {
            uint control = 0b_0000_0000_0000_0000_1000_0000_0000_0000;

            SVONode SVONode = new SVONode(0, 0, 0, 0, true);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test UnPackStruct bitfield occupancy
        [Test]
        public void UnPackStruct_BitfieldOccupancy()
        {
            uint control = 0b_0000_0000_0000_0000_0000_0000_1111_1111;
            uint sample = 0b_1111_1111_0000_0000_0000_0000_0000_0000;

            SVONode SVONode = new SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf);

            Assert.AreEqual(_bitFieldOccupancy, control);
        }

        // Test UnPackStruct run length
        [Test]
        public void UnPackStruct_RunLength()
        {
            uint control = 0b_0000_0000_0000_0000_0000_0000_0000_1111;
            uint sample = 0b_0000_0000_1111_0000_0000_0000_0000_0000;

            SVONode SVONode = new SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf);

            Assert.AreEqual(_runLength, control);
        }

        // Test UnPackStruct octree depth
        [Test]
        public void UnPackStruct_OctreeDepth()
        {
            uint control = 0b_0000_0000_0000_0000_0000_0000_0000_1111;
            uint sample = 0b_0000_0000_0000_1111_0000_0000_0000_0000;

            SVONode SVONode = new SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf);

            Assert.AreEqual(_depth, control);
        }

        // Test UnPackStruct is leaf
        [Test]
        public void UnPackStruct_IsLeaf()
        {
            uint sample = 0b_0000_0000_0000_0000_1000_0000_0000_0000;

            SVONode SVONode = new SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf);

            Assert.AreEqual(isLeaf, true);
        }
    }
    #endregion
}
