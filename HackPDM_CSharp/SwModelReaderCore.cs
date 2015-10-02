// https://github.com/schwitters/swmodelreader
// https://github.com/schwitters/swmodelreader/blob/master/SwModelReader.Net/SwModelReaderCore/SwModelReader.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Ionic.Zlib;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SwModelReaderCore
{
    public enum SwFileReaderResult
    {
        Ok,
        Fail
    }

    public class SwStorageChunkInfo
    {
        public byte[] Chunk { get; set; }
        public uint UncompressedSize { get; set; }
        public uint CompressedSize { get; set; }
        public uint ChunkOffset { get; set; }
        public int StartCompressedBlock { get; set; }
        public string ChunkName { get; set; }
        public int HeaderLength { get; set; }

        public int GetLength()
        {
            return HeaderLength + (int)CompressedSize;
        }
    }
    public class SwStorage
    {
        private readonly List<SwStorageChunkInfo> m_chunks = new List<SwStorageChunkInfo>();
 
        private readonly uint m_header;
        public uint Header { get { return this.m_header; } }

        private readonly uint m_Key;
        public uint Key { get {return m_Key;}}

        public SwStorage(uint header, uint key)
        {
            this.m_header = header;
            this.m_Key = key;
        }

        public void AddChunk(SwStorageChunkInfo chunk)
        {
            this.m_chunks.Add(chunk);
        }
        
        public IReadOnlyList<SwStorageChunkInfo> GetChunks()
        {
            return m_chunks;
        }

    }
    public class SwModelReader : IDisposable
    {

        private readonly Stream m_stream;
        private SwStorage m_storage;
        public SwModelReader(Stream stream)
        {
            this.m_stream = stream;
            m_storage = Process(m_stream);
        }

        private SwStorage Process(Stream stream)
        {
            byte[] blob = StreamHelper.ReadToEnd(m_stream);
            int index = 0;
            // yet unknown the first bytes
            uint header = GetUInt(blob, index);
            index += 4;

            index += 3; // skip 3 bytes
            // there seems to be an key for scrambling strings
            uint key = blob[index];
            index += 1;
            SwStorage storage = new SwStorage(header, key);
            for (; index < blob.Length; index++)
            {
                // TODO: find a way to recognize if the table of contents starts
                SwStorageChunkInfo chunk = ReadChunk(storage, blob, index);
                if (chunk == null)
                {
                    //readContentTable(blob, index);
                    break;
                }
                storage.AddChunk(chunk);
                int nextOffset = index + (int)chunk.GetLength();
                index = nextOffset - 1; // for !
            }

            return storage;
        }



        private static SwStorageChunkInfo ReadChunk(SwStorage storage, byte[] blob, int startIndex)
        {
            // within a block before offset 0x12 the bytes are yet unknown
            int index = startIndex + 0x12;

            uint compressedSize = GetUInt(blob, index);
            index += 4;

            uint uncompressedSize = GetUInt(blob, index);
            index += 4;

            int nameSize = (int)GetUInt(blob, index);
            index += 4;
            int namestart = index;
            if (namestart + nameSize > blob.Length)
            {
                // happens if we try to read the content table
                return null;
            }
            // the stream names are scrambled ;-)
            byte[] unrolName = new byte[nameSize];
            for (; index < namestart + nameSize; index++)
            {
                byte unroledByte = Rol(blob[index], (int)storage.Key);
                unrolName[index - namestart] = unroledByte;
            }
            string chunkName = Encoding.UTF8.GetString(unrolName);
            if (string.IsNullOrEmpty(chunkName))
            {
                chunkName = "un_" + Guid.NewGuid().ToString();
            }

            int compressedDataStart = namestart + nameSize;
           
            SwStorageChunkInfo chunkInfo = new SwStorageChunkInfo();
            chunkInfo.ChunkOffset = (uint)startIndex;
            chunkInfo.CompressedSize = compressedSize;
            chunkInfo.StartCompressedBlock = compressedDataStart;
            chunkInfo.ChunkName = chunkName;
            chunkInfo.HeaderLength = compressedDataStart - startIndex;

            if (uncompressedSize > 0)
            {
                byte[] uncompressedData = new byte[uncompressedSize];
                ZlibCodec inflator = new ZlibCodec();
                inflator.InitializeInflate(false);
                inflator.InputBuffer = blob;
                inflator.AvailableBytesIn = (int)compressedSize;
                inflator.AvailableBytesOut = (int)uncompressedSize;
                inflator.NextIn = compressedDataStart;
                inflator.OutputBuffer = uncompressedData;
                inflator.NextOut = 0;
                inflator.Inflate(FlushType.Full);
                inflator.EndInflate();
                chunkInfo.Chunk = uncompressedData;
            }
            else
            {
                chunkInfo.Chunk = new byte[0];
            }
            
            return chunkInfo;
        }
        
        public static int GetInt(byte[] data, int offset)
        {
            int i = offset;
            int b0 = data[i++] & 0xFF;
            int b1 = data[i++] & 0xFF;
            int b2 = data[i++] & 0xFF;
            int b3 = data[i++] & 0xFF;
            return (b3 << 24) + (b2 << 16) + (b1 << 8) + (b0 << 0);
        }

        public static uint GetUInt(byte[] data, int offset)
        {
            int retNum = GetInt(data, offset);
            return (uint)(retNum & 0x00FFFFFFFFL);
        }

        public static byte[] Inflate(byte[] data, int outputSize)
        {
            byte[] output = new Byte[outputSize];
            using (MemoryStream ms = new MemoryStream())
            {
                ZlibCodec compressor = new ZlibCodec();
                compressor.InitializeInflate(false);

                compressor.InputBuffer = data;
                compressor.AvailableBytesIn = data.Length;
                compressor.NextIn = 0;
                compressor.OutputBuffer = output;

                foreach (var f in new FlushType[] { FlushType.None, FlushType.Finish })
                {
                    int bytesToWrite = 0;
                    do
                    {
                        compressor.AvailableBytesOut = outputSize;
                        compressor.NextOut = 0;
                        compressor.Inflate(f);

                        bytesToWrite = outputSize - compressor.AvailableBytesOut;
                        if (bytesToWrite > 0)
                            ms.Write(output, 0, bytesToWrite);
                    }
                    while ((f == FlushType.None && (compressor.AvailableBytesIn != 0 || compressor.AvailableBytesOut == 0)) ||
                        (f == FlushType.Finish && bytesToWrite != 0));
                }
                compressor.EndInflate();
                return ms.ToArray();
            }
        }
        

        public SwFileReaderResult GetStream(string streamName,out byte[] streamData)
        {
            streamData = m_storage.GetChunks().Where(chunk => streamName.Equals(chunk.ChunkName)).Select(chunk => chunk.Chunk).First();
            return SwFileReaderResult.Ok;
        }

        public SwFileReaderResult GetAvailableStreamNames(out string[] names)
        {

            names = m_storage.GetChunks().Select(chunk => chunk.ChunkName).ToArray();
            return SwFileReaderResult.Ok;
        }

        public static byte Rol(byte bits, int shift)
        {
            // Unlike the RotateLeft( uint, int ) and RotateLeft( ulong, int ) 
            // overloads, we need to mask out the required bits of count 
            // manually, as the shift operaters will promote a byte to uint, 
            // and will not mask out the correct number of count bits.
            shift &= 0x07;
            return (byte)((bits << shift) | (bits >> (8 - shift)));
        }

        public class StreamHelper
        {
            public static byte[] ReadToEnd(System.IO.Stream stream)
            {

                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;

            }


        }


        public void Dispose()
        {
            this.m_stream.Dispose();
        }

        public static SwModelReader Open(string path)
        {
            return new SwModelReader(new FileStream(path, FileMode.Open, FileAccess.Read));
        }
    }
}