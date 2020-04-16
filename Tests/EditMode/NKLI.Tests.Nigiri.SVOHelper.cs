/// <summary>
/// NKLI     : Nigiri - EditMode, Test Unit - SVO Helper
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using NKLI.Nigiri.SVO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NKLI.Tests.Nigiri.SVO
{
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
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_1000_0000);

            testValues[0] = 0;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 1
            testValues = new uint[8];

            testValues[1] = 5;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0100_0000);

            testValues[1] = 0;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 2
            testValues = new uint[8];

            testValues[2] = 5;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0010_0000);

            testValues[2] = 0;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 3
            testValues = new uint[8];

            testValues[3] = 5;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0001_0000);

            testValues[3] = 0;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 4
            testValues = new uint[8];

            testValues[4] = 5;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_1000);

            testValues[4] = 0;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 5
            testValues = new uint[8];

            testValues[5] = 5;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0100);

            testValues[5] = 0;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 6
            testValues = new uint[8];

            testValues[6] = 5;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0010);

            testValues[6] = 0;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);

            // Position 7
            testValues = new uint[8];

            testValues[7] = 5;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0001);

            testValues[7] = 0;
            Assert.AreEqual(Helpers.GetOccupancyBitmap(testValues), (uint)0b_0000_0000);
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
}
