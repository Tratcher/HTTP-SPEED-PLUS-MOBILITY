using System;
using System.Threading.Tasks;
using Xunit;

namespace SharedProtocol.Tests
{
    public class QueueStreamTests
    {
        [Fact]
        public async Task CreateAndDispose_ReadsReturn0WritesThrow()
        {
            QueueStream stream = new QueueStream();
            stream.Dispose();
            byte[] buffer = new byte[1];
            Assert.Equal(0, stream.Read(buffer, 0, 1));
            Assert.Equal(0, await stream.ReadAsync(buffer, 0, 1));
            Assert.Throws<ObjectDisposedException>(() => stream.Write(buffer, 0, 1));
            // TODO: A problem with XUnit prevents this exception from coming back to the Assert:
            // Assert.Throws<ObjectDisposedException>(async () => await stream.WriteAsync(buffer, 0, 1));
        }

        [Fact]
        public async Task CreateWriteAndDispose_ReadsReturn0WritesThrow()
        {
            QueueStream stream = new QueueStream();
            byte[] buffer = new byte[1];
            stream.Write(buffer, 0, 1);
            stream.Dispose();
            Assert.Equal(0, stream.Read(buffer, 0, 1));
            Assert.Equal(0, await stream.ReadAsync(buffer, 0, 1));
            Assert.Throws<ObjectDisposedException>(() => stream.Write(buffer, 0, 1));
            // TODO: A problem with XUnit prevents this exception from coming back to the Assert:
            // Assert.Throws<ObjectDisposedException>(async () => await stream.WriteAsync(buffer, 0, 1));
        }

        [Fact]
        public async Task WriteAndRead()
        {
            QueueStream stream = new QueueStream();
            byte[] writeBuffer = new byte[] { 0x65 };
            stream.Write(writeBuffer, 0, writeBuffer.Length);
            byte[] readBuffer = new byte[10];
            Assert.Equal(1, stream.Read(readBuffer, 0, readBuffer.Length));
            Assert.Equal(writeBuffer[0], readBuffer[0]);
            readBuffer = new byte[10];

            await stream.WriteAsync(writeBuffer, 0, writeBuffer.Length);
            Assert.Equal(1, await stream.ReadAsync(readBuffer, 0, readBuffer.Length));
            Assert.Equal(writeBuffer[0], readBuffer[0]);
        }

        [Fact]
        public async Task MultipleWrites_SingleRead()
        {
            QueueStream stream = new QueueStream();
            byte[] writeBuffer = new byte[] { 0x65 };
            stream.Write(writeBuffer, 0, writeBuffer.Length);
            stream.Write(writeBuffer, 0, writeBuffer.Length);
            byte[] readBuffer = new byte[10];
            Assert.Equal(2, stream.Read(readBuffer, 0, readBuffer.Length));
            Assert.Equal(writeBuffer[0], readBuffer[0]);
            Assert.Equal(writeBuffer[0], readBuffer[1]);
            readBuffer = new byte[10];

            await stream.WriteAsync(writeBuffer, 0, writeBuffer.Length);
            await stream.WriteAsync(writeBuffer, 0, writeBuffer.Length);
            Assert.Equal(2, await stream.ReadAsync(readBuffer, 0, readBuffer.Length));
            Assert.Equal(writeBuffer[0], readBuffer[0]);
            Assert.Equal(writeBuffer[0], readBuffer[1]);
        }

        [Fact]
        public async Task SingleWrite_MultipleReads()
        {
            QueueStream stream = new QueueStream();
            byte[] writeBuffer = new byte[] { 0x65, 0x66 };
            stream.Write(writeBuffer, 0, writeBuffer.Length);
            byte[] readBuffer = new byte[1];
            Assert.Equal(1, stream.Read(readBuffer, 0, readBuffer.Length));
            Assert.Equal(writeBuffer[0], readBuffer[0]);
            readBuffer = new byte[1];
            Assert.Equal(1, stream.Read(readBuffer, 0, readBuffer.Length));
            Assert.Equal(writeBuffer[1], readBuffer[0]);
            readBuffer = new byte[1];

            await stream.WriteAsync(writeBuffer, 0, writeBuffer.Length);
            Assert.Equal(1, await stream.ReadAsync(readBuffer, 0, readBuffer.Length));
            Assert.Equal(writeBuffer[0], readBuffer[0]);
            readBuffer = new byte[1];
            Assert.Equal(1, await stream.ReadAsync(readBuffer, 0, readBuffer.Length));
            Assert.Equal(writeBuffer[1], readBuffer[0]);
        }
    }
}
