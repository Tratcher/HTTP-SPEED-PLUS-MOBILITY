﻿using Xunit;

namespace SharedProtocol.Compression.Tests
{
    public class CompressionTests
    {
        [Fact]
        public void CompressAndDecompressEmptyData()
        {
            CompressionProcessor compressor = new CompressionProcessor();

            byte[] data = new byte[10];

            byte[] compressedData = compressor.Compress(data);
            byte[] decompressedData = compressor.Decompress(compressedData);

            Assert.Equal(data, decompressedData);
        }

        [Fact]
        public void CompressAndDecompressSimpleData()
        {
            CompressionProcessor compressor = new CompressionProcessor();

            byte[] data = new byte[] { 0x65, 0x66, 0x67, 0x68 };

            byte[] compressedData = compressor.Compress(data);
            byte[] decompressedData = compressor.Decompress(compressedData);

            Assert.Equal(data, decompressedData);
        }

        [Fact]
        public void CompressAndDecompressSimpleDataX2()
        {
            CompressionProcessor compressor = new CompressionProcessor();

            byte[] data = new byte[] { 0x65, 0x66, 0x67, 0x68 };

            byte[] compressedData = compressor.Compress(data);
            byte[] decompressedData = compressor.Decompress(compressedData);
            Assert.Equal(data, decompressedData);

            compressedData = compressor.Compress(data);
            decompressedData = compressor.Decompress(compressedData);
            Assert.Equal(data, decompressedData);
        }
    }
}
