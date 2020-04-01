/// <summary>
/// NKLI     : Nigiri - Test Unit SVO
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
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Tests.Nigiri.SVO
{
    #region Test_SVOBuilder
    public class Test_SVOVoxelizer
    {
        [Test]
        public void Voxelizer()
        {
            // Load test buffer from disk
            // Test buffer is a Morton ordered 256^3 grid of stored RGBA (values * 256)
            /*string fileNameWithoutExtension = Path.GetFileNameWithoutExtension("Test_Unit-MortonBuffer");
            TextAsset textAsset = Resources.Load(fileNameWithoutExtension) as TextAsset;
            Debug.Log("<Unit Test> Loaded Buffer, size:" + textAsset.bytes.Length);*/

            int width = 2160;
            int height = 1200;

            // Load textures
            Texture2D Tex2D_gBuffer0 = Resources.Load("Test_Unit-RenderTexture-gBuffer0", typeof(Texture2D)) as Texture2D;
            Texture2D Tex2D_gBuffer1 = Resources.Load("Test_Unit-RenderTexture-gBuffer1", typeof(Texture2D)) as Texture2D;
            Texture2D Tex2D_gBuffer2 = Resources.Load("Test_Unit-RenderTexture-gBuffer2", typeof(Texture2D)) as Texture2D;

            Texture2D Tex2D_Position = Resources.Load("Test_Unit-RenderTexture-PositionTexture", typeof(Texture2D)) as Texture2D;
            Texture2D Tex2D_Depth = Resources.Load("Test_Unit-RenderTexture-DepthTexture", typeof(Texture2D)) as Texture2D;
            Texture2D Tex2D_Source = Resources.Load("Test_Unit-RenderTexture-Source", typeof(Texture2D)) as Texture2D;
            Debug.Log("<Unit Test> Loaded textures");

            RenderTexture RT_gBuffer0 = new RenderTexture(width, height, 24);
            RenderTexture RT_gBuffer1 = new RenderTexture(width, height, 24);
            RenderTexture RT_gBuffer2 = new RenderTexture(width, height, 24);

            RenderTexture RT_Position = new RenderTexture(width, height, 24);
            RenderTexture RT_Depth = new RenderTexture(width, height, 24);
            RenderTexture RT_Source = new RenderTexture(width, height, 24);

            Graphics.Blit(Tex2D_gBuffer0, RT_gBuffer0);
            Graphics.Blit(Tex2D_gBuffer1, RT_gBuffer1);
            Graphics.Blit(Tex2D_gBuffer2, RT_gBuffer2);

            Graphics.Blit(Tex2D_Position, RT_Position);
            Graphics.Blit(Tex2D_Depth, RT_Depth);
            Graphics.Blit(Tex2D_Source, RT_Source);
            Debug.Log("<Unit Test> Textures copied to GPU" + Environment.NewLine);

            int sampleCount = Tex2D_Source.width * Tex2D_Source.height;

            // Load mask buffer
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension("Test_Unit-MaskBuffer");
            TextAsset textAsset = Resources.Load(fileNameWithoutExtension) as TextAsset;

            // Decompress mask buffer
            byte[] test_MaskBuffer = NKLI.Nigiri.Tools.LZMAtools.DecompressLZMAByteArrayToByteArray(textAsset.bytes);
            Debug.Log("<Unit Test> Decompressed Buffer, size: " + test_MaskBuffer.Length + " Bytes" + Environment.NewLine);

            // Test decompressed buffer is expected size
            Assert.AreEqual(10368000, test_MaskBuffer.Length);

            // Write buffer to GPU
            ComputeBuffer buffer_Mask = new ComputeBuffer(sampleCount, sizeof(uint), ComputeBufferType.Default);
            buffer_Mask.SetData(test_MaskBuffer);
            Debug.Log("<Unit Test> Buffer copied to GPU" + Environment.NewLine);

            // Instantiate SVO Tree
            NKLI.Nigiri.SVO.Tree SVO = ScriptableObject.CreateInstance<NKLI.Nigiri.SVO.Tree>();
            SVO.Create(8, 4096, 66);

            // Split queue length should be rounded to nerest mul of 8
            Assert.AreEqual(64, SVO.SplitQueueMaxLength);

            if (SVO.Buffer_SVO == null) Debug.LogError("<Unity Test> SVO_Buffer == Null");
            else Debug.Log("<Unit Test> Instantiated SVO tree");

            // Instantiate voxelizer
            Voxelizer voxelizer = new Voxelizer(SVO, 1, 0.9f, 1, 100, 8);
            Debug.Log("<Unit Test> Instantiated voxelizer" + Environment.NewLine);

            Debug.Log("<Unit Test> Max SVO Nodes: " + SVO.Buffer_SVO_Count);
            Debug.Log("<Unit Test> Testing 4096 iterations..." + Environment.NewLine);

            // We iterate multiple times to fill buffer
            byte[] test_Buffer_SVO = new byte[1];
            for (int z = 0; z <= 1024; z = z + 1)
            {
                // Keep track of interation index
                //Debug.Log("-----------------------------------");
                //Debug.Log("Iterating encode and split. Count: " + z + Environment.NewLine);


                // Voxelize scene
                voxelizer.VoxelizeScene(sampleCount, RT_Position, RT_Source, RT_gBuffer0, buffer_Mask);
                //Debug.Log("<Unit Test> Voxelized scene");

                //Debug.Log("<Unit Test> Built SVO, gridWidth:" + gridWidth + ", ThreadCount:" + svo.ThreadCount + ", NodeCount:" + svo.NodeCount + ", VoxelCount:" + svo.VoxelCount + ", TreeDepth:" + svo.TreeDepth);

                // Intiate syncronous 'async' readback
                SVO.SyncGPUReadback(
                    out Queue<AsyncGPUReadbackRequest> queue_Counters,
                    out Queue<AsyncGPUReadbackRequest> queue_Counters_Internal,
                    out Queue<AsyncGPUReadbackRequest> queue_SVO,
                    out Queue<AsyncGPUReadbackRequest> queue_SplitQueue);

                // Readback counters to CPU
                var req_Counters = queue_Counters.Peek();
                uint[] test_Buffer_Counters = new uint[NKLI.Nigiri.SVO.Tree.Buffer_Counters_Count];
                if (req_Counters.hasError) Debug.Log("GPU readback error detected.");
                else
                {
                    req_Counters.GetData<uint>().CopyTo(test_Buffer_Counters);
                    queue_Counters.Dequeue();
                }


                // Readback internal counters to CPU
                var req_Counters_Internal = queue_Counters_Internal.Peek();
                uint[] test_Buffer_Counters_Internal = new uint[1];
                if (req_Counters_Internal.hasError) Debug.Log("GPU readback error detected.");
                else
                {
                    req_Counters_Internal.GetData<uint>().CopyTo(test_Buffer_Counters_Internal);
                    queue_Counters_Internal.Dequeue();
                }

                // Readback octree to CPU
                //uint sizeOctree = svo.NodeCount * 8;
                var req_SVO = queue_SVO.Peek();
                test_Buffer_SVO = new byte[SVO.Buffer_SVO_ByteLength];
                if (req_SVO.hasError) Debug.Log("GPU readback error detected.");
                else
                {
                    req_SVO.GetData<byte>().CopyTo(test_Buffer_SVO);
                    queue_SVO.Dequeue();
                }

                // Readback splitqueue to CPU
                var req_SplitQueue = queue_SplitQueue.Peek();
                byte[] test_Buffer_SplitQueue = new byte[SVO.SplitQueueMaxLength * 4];
                if (req_SplitQueue.hasError) Debug.Log("GPU readback error detected.");
                else
                {
                    req_SplitQueue.GetData<byte>().CopyTo(test_Buffer_SplitQueue);
                    queue_SplitQueue.Dequeue();
                }

                // Manually flush pipeline to be sure we have everything
                //Debug.Log("<Unit Test> Buffers copied to CPU" + Environment.NewLine);
                GL.Flush();

                // Log counter values
                //Debug.Log("<Unit Test> (Counters) Max Depth: " + test_Buffer_Counters[0]);
                //Debug.Log("<Unit Test> (Counters) Max split queue items: " + test_Buffer_Counters[1]);
                //Debug.Log("<Unit Test> (Counters) Cur split queue items: " + test_Buffer_Counters[2]);
                //Debug.Log("<Unit Test> (Counters) SVO write position: " + test_Buffer_Counters_Internal[0] + Environment.NewLine);


                // Outputs contents of split queue buffer
                string queueString = "";
                for (int i = 0; i < (test_Buffer_SplitQueue.Length / 4); i++)
                {
                    byte[] queueByte = new byte[4];
                    Buffer.BlockCopy(test_Buffer_SplitQueue, (i * 4), queueByte, 0, 4);
                    queueString += "[" + i + ":" + BitConverter.ToUInt32(queueByte, 0) + "]";
                }
                //Debug.Log("Split queue content:" + Environment.NewLine + queueString + Environment.NewLine);

                //
                /// It's important to remove duplicates from the queue!
                //

                // Copies split queue buffer to hashset to remove dupes
                HashSet<UInt32> queueSet = new HashSet<uint>();
                for (int i = 0; i < (test_Buffer_SplitQueue.Length / 4); i++)
                {
                    byte[] queueByte = new byte[4];
                    Buffer.BlockCopy(test_Buffer_SplitQueue, (i * 4), queueByte, 0, 4);
                    uint queueValue = BitConverter.ToUInt32(queueByte, 0);
                    if (queueValue != 0) queueSet.Add(queueValue);
                }
                //Debug.Log("Uniques in split queue: " + Environment.NewLine + queueSet.Count + Environment.NewLine);

                // Copies hashset back to buffer
                int queueWriteBackIndex = 0;
                HashSet<uint>.Enumerator queueEnum = queueSet.GetEnumerator();
                while (queueEnum.MoveNext())
                {
                    byte[] queueByte = new byte[4];
                    queueByte = BitConverter.GetBytes(queueEnum.Current);
                    Buffer.BlockCopy(queueByte, 0, test_Buffer_SplitQueue, queueWriteBackIndex * 4, 4);
                    queueWriteBackIndex++;
                }

                // Zeros rest of buffer
                int queueStartIndex = queueSet.Count * 4;
                Array.Clear(test_Buffer_SplitQueue, queueStartIndex, (test_Buffer_SplitQueue.Length - queueStartIndex));


                // Outputs remaining contents of split queue buffer
                queueString = "";
                for (int i = 0; i < (test_Buffer_SplitQueue.Length / 4); i++)
                {
                    byte[] queueByte = new byte[4];
                    Buffer.BlockCopy(test_Buffer_SplitQueue, (i * 4), queueByte, 0, 4);
                    queueString += "[" + i + ":" + BitConverter.ToUInt32(queueByte, 0) + "]";
                }
                //Debug.Log("Ajusted Split queue content:" + Environment.NewLine + queueString + Environment.NewLine);

                // Send adjusted buffer back to GPU
                SVO.Buffer_SplitQueue.SetData(test_Buffer_SplitQueue);


                // Split nodes
                voxelizer.SplitNodes(queueSet.Count);
            }

            // Attempt to verify number of output nodes
            int detectedCount = 0;
            for (int i = 0; i < (test_Buffer_SVO.Length / 32); i++)
            {
                bool isDetected = false;
                for (int y = 0; y < 32; y++)
                {
                    if (test_Buffer_SVO[(i * 32) + y] != 0) isDetected = true;
                }
                if (isDetected) detectedCount++;

            }
            Debug.Log("<Unit Test> Detected SVO nodes:" + detectedCount + Environment.NewLine);

            // Out nodes in human readable format
            string filenameReadable = Application.dataPath + "/Test_Unit-SVO-HumanReadable.txt";
            Debug.Log("Writing to:" + filenameReadable);
            using (System.IO.StreamWriter fileOutput = new System.IO.StreamWriter(filenameReadable))
            {

                //int verifiedCount = 0;
                for (int i = 0; i < (test_Buffer_SVO.Length / 32); i++)
                {
                    byte[] nodeBytes = new byte[32];
                    byte[] nodeBytesReferenceOffset = new byte[4];
                    byte[] nodeBytesPackedBitfield = new byte[4];

                    byte[] nodeBytesR = new byte[4];
                    byte[] nodeBytesG = new byte[4];
                    byte[] nodeBytesB = new byte[4];
                    byte[] nodeBytesA = new byte[4];

                    Buffer.BlockCopy(test_Buffer_SVO, (i * 32), nodeBytes, 0, 32);
                    Buffer.BlockCopy(nodeBytes, 0, nodeBytesReferenceOffset, 0, 4);
                    Buffer.BlockCopy(nodeBytes, 4, nodeBytesPackedBitfield, 0, 4);

                    Buffer.BlockCopy(nodeBytes, 8, nodeBytesR, 0, 4);
                    Buffer.BlockCopy(nodeBytes, 12, nodeBytesG, 0, 4);
                    Buffer.BlockCopy(nodeBytes, 16, nodeBytesB, 0, 4);
                    Buffer.BlockCopy(nodeBytes, 20, nodeBytesA, 0, 4);

                    SVONode node = new SVONode(BitConverter.ToUInt32(nodeBytesReferenceOffset, 0), BitConverter.ToUInt32(nodeBytesPackedBitfield, 0));
                    node.UnPackStruct(out uint _bitfieldOccupance, out uint _runlength, out uint _ttl, out bool isLeaf);

                    node.value_A = BitConverter.ToUInt32(nodeBytesA, 0);
                    node.value_R = BitConverter.ToUInt32(nodeBytesR, 0);
                    node.value_G = BitConverter.ToUInt32(nodeBytesG, 0);
                    node.value_B = BitConverter.ToUInt32(nodeBytesB, 0);

                    string line = "[" + i + "] [Ref:" + node.referenceOffset + "] [BO:" + Convert.ToString(_bitfieldOccupance, toBase: 2) + "]" +
                        " [RL:" + _runlength + "] [TTL:" + _ttl + "] [isLeaf:" + isLeaf + "]" +
                        " [R:" + node.value_R + "] [G:" + node.value_G + "] [B:" + node.value_B + "] [A:" + node.value_A + "]";

                    fileOutput.WriteLine(line);

                    //Debug.Log(node.referenceOffset);
                }
            }
            //Debug.Log("<Unit Test> Detected SVO nodes:" + detectedCount + Environment.NewLine);


            // Test that write counter matches detected node
            //Assert.AreEqual(test_Buffer_Counters[2], detectedCount);

            // Dump file to disk
            /*string file = Application.dataPath + "/Test_Unit-SVO.bytes";
            Debug.Log("Writing to:" + file);
            FileStream fs = System.IO.File.Create(file);
            fs.Write(test_Buffer_SVO, 0, Convert.ToInt32(sizeOctree));
            fs.Close()*/

            // Cleanup

            // Dispose voxelizer
            voxelizer.Dispose();

            // Destroy SVO
            NKLI.Nigiri.Helpers.DestroyScriptableObject(ref SVO);

            // Release buffer
            NKLI.Nigiri.Helpers.ReleaseBufferRef(ref buffer_Mask);

            // Dispose textures
            NKLI.Nigiri.Helpers.DisposeTextureRef(ref RT_gBuffer0, true);
            NKLI.Nigiri.Helpers.DisposeTextureRef(ref RT_gBuffer1, true);
            NKLI.Nigiri.Helpers.DisposeTextureRef(ref RT_gBuffer2, true);

            NKLI.Nigiri.Helpers.DisposeTextureRef(ref RT_Position, true);
            NKLI.Nigiri.Helpers.DisposeTextureRef(ref RT_Depth, true);
            NKLI.Nigiri.Helpers.DisposeTextureRef(ref RT_Source, true);
        }
    }
    #endregion
}
