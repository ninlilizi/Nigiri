using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using NKLI.Nigiri.SVO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Nigiri.SVO
{
    #region Test_SVOBuilder
    public class Test_SVOBuilder
    {
        [Test]
        public void SVOBuilder()
        {
            // Load test buffer from disk
            // Test buffer is a Morton ordered 256^3 grid of stored RGBA (values * 256)
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension("Test_Unit-MortonBuffer");
            TextAsset textAsset = Resources.Load(fileNameWithoutExtension) as TextAsset;
            Debug.Log("<Unit Test> Loaded Buffer, size:" + textAsset.bytes.Length);

            // Decompress test buffer
            byte[] test_MortonBuffer = NKLI.Nigiri.Tools.LZMAtools.DecompressLZMAByteArrayToByteArray(textAsset.bytes);
            Debug.Log("<Unit Test> Decompressed Buffer, size:" + test_MortonBuffer.Length + Environment.NewLine);

            // Test decompressed buffer is expected size
            Assert.AreEqual(268435456, test_MortonBuffer.Length);

            // Write buffer to GPU
            ComputeBuffer buffer_Morton = new ComputeBuffer(256 * 256 * 256, sizeof(float) * 4, ComputeBufferType.Default);
            buffer_Morton.SetData(test_MortonBuffer);
            Debug.Log("<Unit Test> Buffer copied to GPU");

            uint gridWidth = 256;
            uint occupiedVoxels = 20003;

            // Attempt to instantiate SVO
            SVOBuilder svo = new SVOBuilder(buffer_Morton, occupiedVoxels, gridWidth);
            Debug.Log("<Unit Test> Built SVO, gridWidth:" + gridWidth + ", ThreadCount:" + svo.ThreadCount + ", NodeCount:" + svo.NodeCount + ", VoxelCount:" + svo.VoxelCount + ", TreeDepth:" + svo.TreeDepth);

            // Intiate syncronous 'async' readback
            svo.SyncGPUReadback(out UnityEngine.Rendering.AsyncGPUReadbackRequest req_Counters, out UnityEngine.Rendering.AsyncGPUReadbackRequest req_SVO);
            
            // Readback counters to CPU
            uint[] test_Buffer_Counters = new uint[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + svo.TreeDepth];
            if (req_Counters.hasError) Debug.Log("GPU readback error detected.");
            else
            {
                var buffer = req_Counters.GetData<uint>();
                buffer.CopyTo(test_Buffer_Counters);

            }

            // Readback octree to CPU
            uint sizeOctree = svo.NodeCount * 8;
            byte[] test_Buffer_SVO = new byte[sizeOctree];
            if (req_SVO.hasError) Debug.Log("GPU readback error detected.");
            else
            {
                var buffer = req_SVO.GetData<byte>();
                buffer.CopyTo(test_Buffer_SVO);

            }

            // Manually flush pipeline to be sure we have everything
            Debug.Log("<Unit Test> Buffers copied to CPU" + Environment.NewLine);
            GL.Flush();

            for (uint i = 0; i < (test_Buffer_Counters.Length - NKLI.Nigiri.SVO.SVOBuilder.boundariesOffsetU); i++)
            {
                Debug.Log("<Unit Test> Boundary " + (i) + ":" + test_Buffer_Counters[i + NKLI.Nigiri.SVO.SVOBuilder.boundariesOffsetU]);
            }

            // Test that returned max depth matches pre-calculated
            Assert.AreEqual((svo.TreeDepth - 1), test_Buffer_Counters[8]);

            // Log counter values
            Debug.Log(Environment.NewLine + "<Unit Test> SVO Read counter:" + test_Buffer_Counters[1]);
            Debug.Log("<Unit Test> SVO Write counter:" + test_Buffer_Counters[2]);
            Debug.Log("<Unit Test> PTR Read counter:" + test_Buffer_Counters[4]);
            Debug.Log("<Unit Test> PTR Write counter:" + test_Buffer_Counters[5]);
            Debug.Log("<Unit Test> Depth counter:" + test_Buffer_Counters[8] + Environment.NewLine);

            // Attempt to verify number of output nodes
            int detectedCount = 0;
            for (int i = 0; i < (test_Buffer_SVO.Length / 8); i++)
            {
                if (((test_Buffer_SVO[(i * 8)]) != 0) ||
                    ((test_Buffer_SVO[(i * 8) + 1]) != 0) ||
                    ((test_Buffer_SVO[(i * 8) + 2]) != 0) ||
                    ((test_Buffer_SVO[(i * 8) + 3]) != 0) ||
                    ((test_Buffer_SVO[(i * 8) + 4]) != 0) ||
                    ((test_Buffer_SVO[(i * 8) + 5]) != 0) ||
                    ((test_Buffer_SVO[(i * 8) + 6]) != 0) ||
                    ((test_Buffer_SVO[(i * 8) + 7]) != 0))
                {
                    detectedCount++;
                }
            }
            Debug.Log("<Unit Test> Detected SVO nodes:" + detectedCount + Environment.NewLine);

            // Test that write counter matches detected node
            Assert.AreEqual(test_Buffer_Counters[2], detectedCount);

            // Dump file to disk
            string file = Application.dataPath + "/Tests_SVO.bytes";
            Debug.Log("Writing to:" + file);
            FileStream fs = System.IO.File.Create(file);
            fs.Write(test_Buffer_SVO, 0, Convert.ToInt32(sizeOctree));
            fs.Close();

            // Cleanup
            svo.Dispose();
            buffer_Morton.Dispose();
        }
    }
    #endregion

    #region Test_SVOHelper
    public class Test_SVOHelper
    {
        // Functions for compressing and verifying Morton test buffer file
        /*[Test]
        public void CompressTestBuffer()
        {
            string file = Application.dataPath + "/Test_Unit-MortonBuffer.dat";
            byte[] array = File.ReadAllBytes(file);

            Debug.Log("<Unit Test> Uncompressed size:" + array.Length);

            byte[] compressed = NKLI.Nigiri.Tools.LZMAtools.CompressByteArrayToLZMAByteArray(array);

            Debug.Log("<Unit Test>   Compressed size:" + compressed.Length);

            FileStream fs = System.IO.File.Create(file + ".lzma");
            fs.Write(compressed, 0, compressed.Length);
            fs.Close();
        }

        [Test]
        public void VerifyTestBuffer()
        {
            string controlFile = Application.dataPath + "/Test_Unit-MortonBuffer.dat";
            byte[] controlArray = File.ReadAllBytes(controlFile);

            Debug.Log("<Unit Test> Control size:" + controlArray.Length);

            string sampleFile = Application.dataPath + "/Test_Unit-MortonBuffer.dat" + ".lzma";
            byte[] sampleArray = File.ReadAllBytes(sampleFile);

            Debug.Log("<Unit Test>   Compressed size:" + sampleArray.Length);
            byte[] decompressed = NKLI.Nigiri.Tools.LZMAtools.DecompressLZMAByteArrayToByteArray(sampleArray);

            Debug.Log("<Unit Test> Sample size:" + controlArray.Length);

            Assert.True(ByteArrayCompare(controlArray, decompressed));

            int occupiedCount = 0;
            for (int i = 0; i < (decompressed.Length / 4); i++)
            {
                if (((decompressed[(i * 4)]) != 0) ||
                        ((decompressed[(i * 4) + 1]) != 0) ||
                        ((decompressed[(i * 4) + 2]) != 0) ||
                        ((decompressed[(i * 4) + 3]) != 0))
                {
                    occupiedCount++;
                }
            }
            Debug.Log("<Unit Test> Occupied voxels detected:" + occupiedCount);
        }*/

        [Test]
        // Test occupancy bitmap calculation - position 0
        public void GetOccupancyBitmap()
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
        // Test calculation of current depth from boundary index
        public void GetDepthFromBoundaries()
        {
            // Calculate control data
            uint gridWidth = 256;
            uint voxelCount = gridWidth * gridWidth * gridWidth;
            uint treeDepth = SVOHelper.GetDepth(gridWidth);
            uint threadCount = SVOHelper.GetThreadCount(voxelCount, gridWidth, treeDepth, out uint[] boundaries);

            uint testInterval = Convert.ToUInt32(Math.Floor(Convert.ToDouble(threadCount / 50000)));
            Debug.Log("<Unit Test> Total threads:" + threadCount);
            Debug.Log("<Unit Test> Sample interval:" + testInterval);
            Debug.Log("<Unit Test> Testing 50,000 indices...");

            for (uint i = 0; i < 50000; i++)
            {
                // Calculate test index
                uint sampleIndex = i * testInterval;

                // Calculate control
                uint controlDepth = 99;
                if (sampleIndex >= 0 && sampleIndex <= boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset])
                {
                    controlDepth = 0;
                }
                else if (sampleIndex > boundaries[0] && sampleIndex <= boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + 1])
                {
                    controlDepth = 1;
                }
                else if (sampleIndex > boundaries[1] && sampleIndex <= boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + 2])
                {
                    controlDepth = 2;
                }
                else if (sampleIndex > boundaries[2] && sampleIndex <= boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + 3])
                {
                    controlDepth = 3;
                }
                else if (sampleIndex > boundaries[3] && sampleIndex <= boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + 4])
                {
                    controlDepth = 4;
                }
                else if (sampleIndex > boundaries[4] && sampleIndex <= boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + 5])
                {
                    controlDepth = 5;
                }
                else if (sampleIndex > boundaries[5] && sampleIndex <= boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + 6])
                {
                    controlDepth = 6;
                }
                else if ((sampleIndex > boundaries[6]) && sampleIndex <= boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + 7])
                {
                    controlDepth = 7;
                }
                else if (sampleIndex == threadCount)
                {
                    controlDepth = 8;
                }

                // Get sample
                uint sampleDepth = SVOHelper.GetDepthFromBoundaries(sampleIndex, treeDepth, boundaries);

                // Test result
                Assert.AreEqual(controlDepth, sampleDepth);
            }
        }

        [Test]
        // Test calculation of thread count and boundary offsets
        public void GetThreadCount()
        {
            // Calculate control data
            uint gridWidth = 256;
            uint voxelCount = Convert.ToUInt32(gridWidth * gridWidth * gridWidth);
            uint treeDepth = SVOHelper.GetDepth(gridWidth);
            uint threadCount = SVOHelper.GetThreadCount(voxelCount, gridWidth, treeDepth, out uint[] boundaries);

            Assert.AreEqual(treeDepth, 8);
            Debug.Log("<Unit Test> (GetThreadCount) gridWidth:" + gridWidth + ", voxelCount:" + voxelCount + ", threadCount:" + threadCount + ", treeDepth:" + treeDepth);

            // Control data
            uint[] control = new uint[8];
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
                Debug.Log("<Unit Test> (GetThreadCount) Control " + i + ":" + control[i] + ", Boundary " + i + ":" + boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + i]);
                Assert.AreEqual(control[i], boundaries[NKLI.Nigiri.SVO.SVOBuilder.boundariesOffset + i]);
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

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.  
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
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
