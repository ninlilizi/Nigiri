using System;
using System.Collections;
using System.Collections.Generic;
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
        public void GetOccupancyBitmap_Position_0()
        {
            uint[] testValues = new uint[8];

            testValues[0] = 5;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_1000_0000);

            testValues[0] = 0;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0000);
        }

        [Test]
        // Test occupancy bitmap calculation - position 1
        public void GetOccupancyBitmap_Position_1()
        {
            uint[] testValues = new uint[8];

            testValues[1] = 5;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0100_0000);

            testValues[1] = 0;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0000);
        }

        [Test]
        // Test occupancy bitmap calculation - position 2
        public void GetOccupancyBitmap_Position_2()
        {
            uint[] testValues = new uint[8];

            testValues[2] = 5;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0010_0000);

            testValues[2] = 0;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0000);
        }

        [Test]
        // Test occupancy bitmap calculation - position 3
        public void GetOccupancyBitmap_Position_3()
        {
            uint[] testValues = new uint[8];

            testValues[3] = 5;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0001_0000);

            testValues[3] = 0;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0000);
        }

        [Test]
        // Test occupancy bitmap calculation - position 4
        public void GetOccupancyBitmap_Position_4()
        {
            uint[] testValues = new uint[8];

            testValues[4] = 5;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_1000);

            testValues[4] = 0;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0000);
        }

        [Test]
        // Test occupancy bitmap calculation - position 5
        public void GetOccupancyBitmap_Position_5()
        {
            uint[] testValues = new uint[8];

            testValues[5] = 5;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0100);

            testValues[5] = 0;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0000);
        }

        [Test]
        // Test occupancy bitmap calculation - position 6
        public void GetOccupancyBitmap_Position_6()
        {
            uint[] testValues = new uint[8];

            testValues[6] = 5;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0010);

            testValues[6] = 0;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0000);
        }

        [Test]
        // Test occupancy bitmap calculation - position 7
        public void GetOccupancyBitmap_Position_7()
        {
            uint[] testValues = new uint[8];

            testValues[7] = 5;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0001);

            testValues[7] = 0;
            Assert.AreEqual(NKLI.Nigiri.SVO.SVOHelper.getOccupancyBitmap(testValues), (uint)0b_0000_0000);
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

            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, sample, 0, 0, false);
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

            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, 0, sample, 0, false);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test PackStruct octree depth
        [Test]
        public void PackStruct_OctreeDepth()
        {
            uint control = 0b_0000_0000_0000_1111_0000_0000_0000_0000;
            uint sample = 0b_0000_0000_0000_0000_0000_0000_0000_1111;

            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, 0, 0, sample, false);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test PackStruct is leaf
        [Test]
        public void PackStruct_IsLeaf()
        {
            uint control = 0b_0000_0000_0000_0000_1000_0000_0000_0000;

            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, 0, 0, 0, true);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test UnPackStruct bitfield occupancy
        [Test]
        public void UnPackStruct_BitfieldOccupancy()
        {
            uint control = 0b_0000_0000_0000_0000_0000_0000_1111_1111;
            uint sample = 0b_1111_1111_0000_0000_0000_0000_0000_0000;

            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf);

            Assert.AreEqual(_bitFieldOccupancy, control);
        }

        // Test UnPackStruct run length
        [Test]
        public void UnPackStruct_RunLength()
        {
            uint control = 0b_0000_0000_0000_0000_0000_0000_0000_1111;
            uint sample = 0b_0000_0000_1111_0000_0000_0000_0000_0000;

            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf);

            Assert.AreEqual(_runLength, control);
        }

        // Test UnPackStruct octree depth
        [Test]
        public void UnPackStruct_OctreeDepth()
        {
            uint control = 0b_0000_0000_0000_0000_0000_0000_0000_1111;
            uint sample = 0b_0000_0000_0000_1111_0000_0000_0000_0000;

            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf);

            Assert.AreEqual(_depth, control);
        }

        // Test UnPackStruct is leaf
        [Test]
        public void UnPackStruct_IsLeaf()
        {
            uint sample = 0b_0000_0000_0000_0000_1000_0000_0000_0000;

            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _runLength, out uint _depth, out bool isLeaf);

            Assert.AreEqual(isLeaf, true);
        }
    }
    #endregion
}
