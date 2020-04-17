/// <summary>
/// NKLI     : Nigiri - EditMode, Test Unit - SVONode
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using NKLI.Nigiri.SVO;
using NUnit.Framework;

namespace NKLI.Tests.Nigiri.SVO
{
    public class Test_SVONode
    {
        // Test PackStruct bitfield occupancy
        [Test]
        public void PackStruct_BitfieldOccupancy()
        {
            uint control = 0b_1111_1111_0000_0000_0000_0000_0000_0000;
            uint sample = 0b_0000_0000_0000_0000_0000_0000_1111_1111;

            SVONode SVONode = new SVONode(0, sample, 0, 0, false);
            //Debug.Log($"<Unit Test> (PackStruct_BitfieldOccupancy) Control:  {Convert.ToString(control, toBase: 2)}");
            //Debug.Log($"<Unit Test> (PackStruct_BitfieldOccupancy) Sample:  {Convert.ToString(SVONode.packedBitfield, toBase: 2)}");

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test PackStruct octree depth
        [Test]
        public void PackStruct_OctreeDepth()
        {
            uint control = 0b_0000_0000_1111_0000_0000_0000_0000_0000;
            uint sample = 0b_0000_0000_0000_0000_0000_0000_0000_1111;

            SVONode SVONode = new SVONode(0, 0, 0, sample, false);

            Assert.AreEqual(SVONode.packedBitfield, control);
        }

        // Test PackStruct is leaf
        [Test]
        public void PackStruct_IsWaitingForMipmap()
        {
            uint control = 0b_0000_0000_0000_1000_0000_0000_0000_0000;

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
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _depth, out bool isLeaf);

            Assert.AreEqual(_bitFieldOccupancy, control);
        }

        // Test UnPackStruct octree depth
        [Test]
        public void UnPackStruct_OctreeDepth()
        {
            uint control = 0b_0000_0000_0000_0000_0000_0000_0000_1111;
            uint sample = 0b_0000_0000_1111_0000_0000_0000_0000_0000;

            SVONode SVONode = new SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _depth, out bool isLeaf);

            Assert.AreEqual(_depth, control);
        }

        // Test UnPackStruct is leaf
        [Test]
        public void UnPackStruct_IsWaitingForMipmap()
        {
            uint sample = 0b_0000_0000_0000_1000_0000_0000_0000_0000;

            SVONode SVONode = new SVONode(0, sample);
            SVONode.UnPackStruct(out uint _bitFieldOccupancy, out uint _depth, out bool isLeaf);

            Assert.AreEqual(isLeaf, true);
        }
    }
}
