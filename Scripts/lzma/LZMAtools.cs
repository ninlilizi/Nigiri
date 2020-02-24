using System.Collections;
using System.IO;
using System;

namespace NKLI.Nigiri.Tools
{
    public static class LZMAtools
    {

        public enum LZMADictionarySize
        {
            Dict256KiB = 262144, // 2^18 = 262144 = 256 KiB
            Dict512KiB = 524288, // 2^19
            Dict1MiB = 1048576, // 2^20
            Dict2MiB = 2097152, // 2^21
            Dict4MiB = 4194304  // 2^22
        };

        #region Compress
        // First ones without dictionary size
        public static void CompressFileToLZMAFile(string inFile, string outFile)
        {
            FileStream input = new FileStream(inFile, FileMode.Open);
            FileStream output = new FileStream(outFile, FileMode.Create);

            Compress(input, output);
        }

        public static void CompressByteArrayToLZMAFile(byte[] inByteArray, string outFile)
        {
            Stream input = new MemoryStream(inByteArray);
            FileStream output = new FileStream(outFile, FileMode.Create);

            Compress(input, output);
        }

        public static byte[] CompressByteArrayToLZMAByteArray(byte[] inByteArray)
        {
            Stream input = new MemoryStream(inByteArray);
            MemoryStream output = new MemoryStream();

            Compress(input, output);
            return output.ToArray();
        }

        // Then the ones with dictionary size
        public static void CompressFileToLZMAFile(string inFile, string outFile, LZMADictionarySize dictSize)
        {
            FileStream input = new FileStream(inFile, FileMode.Open);
            FileStream output = new FileStream(outFile, FileMode.Create);

            Compress(input, output, dictSize);
        }

        public static void CompressByteArrayToLZMAFile(byte[] inByteArray, string outFile, LZMADictionarySize dictSize)
        {
            Stream input = new MemoryStream(inByteArray);
            FileStream output = new FileStream(outFile, FileMode.Create);

            Compress(input, output, dictSize);
        }

        public static byte[] CompressByteArrayToLZMAByteArray(byte[] inByteArray, LZMADictionarySize dictSize)
        {
            Stream input = new MemoryStream(inByteArray);
            MemoryStream output = new MemoryStream();

            Compress(input, output, dictSize);
            return output.ToArray();
        }
        #endregion

        #region Decompress
        public static void DecompressLZMAFileToFile(string inFile, string outFile)
        {
            FileStream input = new FileStream(inFile, FileMode.Open);
            FileStream output = new FileStream(outFile, FileMode.Create);

            Decompress(input, output);
        }

        public static void DecompressLZMAByteArrayToFile(byte[] inByteArray, string outFile)
        {
            Stream input = new MemoryStream(inByteArray);
            FileStream output = new FileStream(outFile, FileMode.Create);

            Decompress(input, output);
        }

        public static byte[] DecompressLZMAByteArrayToByteArray(byte[] inByteArray)
        {
            Stream input = new MemoryStream(inByteArray);
            MemoryStream output = new MemoryStream();

            Decompress(input, output);
            return output.ToArray();
        }
        #endregion

        #region Private functions
        private static void Compress(Stream inputStream, Stream outputStream)
        {
            SevenZip.Compression.LZMA.Encoder coder = new SevenZip.Compression.LZMA.Encoder();

            // Write encoder properties to output stream
            coder.WriteCoderProperties(outputStream);

            // Write size of input stream to output stream.
            outputStream.Write(BitConverter.GetBytes(inputStream.Length), 0, 8);

            // Encode
            coder.Code(inputStream, outputStream, inputStream.Length, -1, null);
            outputStream.Flush();
            outputStream.Close();
        }

        private static void Compress(Stream inputStream, Stream outputStream, LZMADictionarySize dictSize)
        {
            SevenZip.Compression.LZMA.Encoder coder = new SevenZip.Compression.LZMA.Encoder();
            Int32 dictSize32 = (Int32)dictSize;
            coder.SetCoderProperties(new SevenZip.CoderPropID[] { SevenZip.CoderPropID.DictionarySize }, new object[] { dictSize32 });
            // Write encoder properties to output stream
            coder.WriteCoderProperties(outputStream);

            // Write size of input stream to output stream.
            outputStream.Write(BitConverter.GetBytes(inputStream.Length), 0, 8);

            // Encode
            coder.Code(inputStream, outputStream, inputStream.Length, -1, null);
            outputStream.Flush();
            outputStream.Close();
        }

        private static void Decompress(Stream inputStream, Stream outputStream)
        {
            SevenZip.Compression.LZMA.Decoder coder = new SevenZip.Compression.LZMA.Decoder();

            // Read decoder properties
            byte[] properties = new byte[5]; // 5 comes from kPropSize (LzmaEncoder.cs)
            inputStream.Read(properties, 0, 5);

            // Read the size of the output stream.
            byte[] fileLengthBytes = new byte[8];
            inputStream.Read(fileLengthBytes, 0, 8);
            long fileLength = BitConverter.ToInt64(fileLengthBytes, 0);

            // Decode
            coder.SetDecoderProperties(properties);
            coder.Code(inputStream, outputStream, inputStream.Length, fileLength, null);
            outputStream.Flush();
            outputStream.Close();
        }
        #endregion
    }
}
