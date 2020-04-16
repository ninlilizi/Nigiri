/// <summary>
/// NKLI     : Nigiri - SVO, Play mode debugger
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>
/// 

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NKLI.Nigiri.SVO
{
    public class _NKLI_Nigiri_SVO_DebugTool : MonoBehaviour
    {
        public void Output_Human_Readable_SVO()
        {
            // Use the Assert class to test conditions

            // Ensure we have a tree to work with
            if (TestUnitHooks.Most_Recent_Tree == null)
            {
                Debug.LogError("<Nigiri> [UNIT TEST] No Trees have been instantiated this session!");
                return;
            }

            // Grab reference to tree
            NKLI.Nigiri.SVO.Tree tree = TestUnitHooks.Most_Recent_Tree;

            // Intiate syncronous 'async' readback
            tree.SyncGPUReadback(
                out Queue<AsyncGPUReadbackRequest> queue_Counters,
                out Queue<AsyncGPUReadbackRequest> queue_Counters_Internal,
                out Queue<AsyncGPUReadbackRequest> queue_SVO,
                out Queue<AsyncGPUReadbackRequest> queue_SplitQueue);

            // Dequeue counters as we don't need this
            var req_Counters = queue_Counters.Peek();
            if (req_Counters.hasError) Debug.Log("[Nigiri] <UNIT TEST> (Buffer_Counters) GPU readback error detected.");
            else queue_Counters.Dequeue();

            // Dequeue internal counters as we don't need this
            var req_Counters_Internal = queue_Counters_Internal.Peek();
            if (req_Counters_Internal.hasError) Debug.Log("[Nigiri] <UNIT TEST> (Buffer_Counters_Internal) GPU readback error detected.");
            else queue_Counters_Internal.Dequeue();


            // Dequeue splitqueue as we don't need this
            var req_SplitQueue = queue_SplitQueue.Peek();
            if (req_SplitQueue.hasError) Debug.Log("[Nigiri] <UNIT TEST> (Buffer_Queue_Split) GPU readback error detected.");
            else queue_SplitQueue.Dequeue();


            // Readback octree to CPU
            var req_SVO = queue_SVO.Peek();
            byte[] test_Buffer_SVO = new byte[tree.Buffer_SVO_ByteLength];
            if (req_SVO.hasError) Debug.Log("[Nigiri] <UNIT_TEST> (Buffer_SVO) GPU readback error detected.");
            else
            {
                req_SVO.GetData<byte>().CopyTo(test_Buffer_SVO);
                queue_SVO.Dequeue();
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
            Debug.Log("[Nigiri] <Unit Test> Detected SVO nodes:" + detectedCount + Environment.NewLine);

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
                    node.UnPackStruct(out uint _bitfieldOccupance, out uint _runlength, out uint _ttl, out bool isWaitingForMipmap);

                    node.PackColour(new Vector4(BitConverter.ToUInt32(nodeBytesR, 0), BitConverter.ToUInt32(nodeBytesG, 0), BitConverter.ToUInt32(nodeBytesB, 0), BitConverter.ToUInt32(nodeBytesA, 0)));
                    Vector4 unpackedColour = node.UnPackColour();

                    string line = "[" + i + "] [Ref:" + node.referenceOffset + "] [BO:" + Convert.ToString(_bitfieldOccupance, toBase: 2) + "]" +
                        " [RL:" + _runlength + "] [TTL:" + _ttl + "] [isWaitingForMipmap:" + isWaitingForMipmap + "]" +
                        " [R:" + unpackedColour.x + "] [G:" + unpackedColour.y + "] [B:" + unpackedColour.z + "] [A:" + unpackedColour.w + "]";

                    fileOutput.WriteLine(line);

                    //Debug.Log(node.referenceOffset);
                }
            }

        }
    }
}
