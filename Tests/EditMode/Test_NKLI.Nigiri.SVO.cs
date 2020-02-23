using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Nigiri.SVO
{
    public class Test_SVONode
    {
        // Test PackStruct bitfield occupancy
        [Test]
        public void PackStruct_BitfieldOccupancy()
        {
            uint control = 0b_1111_1111_0000_0000_0000_0000_0000_0000;
            uint sample  = 0b_0000_0000_0000_0000_0000_0000_1111_1111;

            //Nigiri.SVO.
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

            //Nigiri.SVO.
            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, 0, sample, 0, false);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test PackStruct octree depth
        [Test]
        public void PackStruct_OctreeDepth()
        {
            uint control = 0b_0000_0000_0000_1111_0000_0000_0000_0000;
            uint sample = 0b_0000_0000_0000_0000_0000_0000_0000_1111;

            //Nigiri.SVO.
            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, 0, 0, sample, false);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test PackStruct is leaf
        [Test]
        public void PackStruct_IsLeaf()
        {
            uint control = 0b_0000_0000_0000_0000_1000_0000_0000_0000;
            uint sample = 0b_0000_0000_0000_0000_0000_0000_0000_0001;

            //Nigiri.SVO.
            NKLI.Nigiri.SVO.SVONode SVONode = new NKLI.Nigiri.SVO.SVONode(0, 0, 0, 0, true);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }
    }
}
