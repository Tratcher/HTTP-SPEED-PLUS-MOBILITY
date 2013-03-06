using SharedProtocol.Framing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SharedProtocol.IO.Tests
{
    public class OutputStreamTests
    {
        [Fact]
        public void WriteData_DataFrameArrives()
        {
            using (Stream transport = new QueueStream())
            using (WriteQueue queue = new WriteQueue(transport))
            {
                Task pumpTask = queue.PumpToStreamAsync();
                Stream output = new OutputStream(1, Framing.Priority.Pri1, queue);
                FrameReader reader = new FrameReader(transport, false, CancellationToken.None);

                int dataLength = 100;
                output.Write(new byte[dataLength], 0, dataLength);
                Frame frame = reader.ReadFrameAsync().Result;
                Assert.False(frame.IsControl);
                Assert.Equal(dataLength, frame.FrameLength);
            }
        }

        [Fact]
        public void WriteMultipleData_OneFramePerWrite()
        {
            using (Stream transport = new QueueStream())
            using (WriteQueue queue = new WriteQueue(transport))
            {
                Task pumpTask = queue.PumpToStreamAsync();
                Stream output = new OutputStream(1, Framing.Priority.Pri1, queue);
                FrameReader reader = new FrameReader(transport, false, CancellationToken.None);

                int dataLength = 100;
                output.Write(new byte[dataLength], 0, dataLength);
                output.Write(new byte[dataLength * 2], 0, dataLength * 2);
                output.Write(new byte[dataLength * 3], 0, dataLength * 3);

                Frame frame = reader.ReadFrameAsync().Result;
                Assert.False(frame.IsControl);
                Assert.Equal(dataLength, frame.FrameLength);

                frame = reader.ReadFrameAsync().Result;
                Assert.False(frame.IsControl);
                Assert.Equal(dataLength * 2, frame.FrameLength);

                frame = reader.ReadFrameAsync().Result;
                Assert.False(frame.IsControl);
                Assert.Equal(dataLength * 3, frame.FrameLength);
            }
        }

        [Fact]
        public void WriteMoreDataThanCredited_OnlyCreditedDataWritten()
        {
            using (Stream transport = new QueueStream())
            using (WriteQueue queue = new WriteQueue(transport))
            {
                Task pumpTask = queue.PumpToStreamAsync();
                OutputStream output = new OutputStream(1, Framing.Priority.Pri1, queue);
                FrameReader reader = new FrameReader(transport, false, CancellationToken.None);

                int dataLength = 0x1FFFF; // More than the 0x10000 default
                Task writeTask = output.WriteAsync(new byte[dataLength], 0, dataLength);
                Assert.False(writeTask.IsCompleted);

                Frame frame = reader.ReadFrameAsync().Result;
                Assert.False(frame.IsControl);
                Assert.Equal(0x10000, frame.FrameLength);

                Task<Frame> nextFrameTask = reader.ReadFrameAsync();
                Assert.False(nextFrameTask.IsCompleted);

                // Free up some space
                output.AddFlowControlCredit(10);

                frame = nextFrameTask.Result;
                Assert.False(frame.IsControl);
                Assert.Equal(10, frame.FrameLength);

                nextFrameTask = reader.ReadFrameAsync();
                Assert.False(nextFrameTask.IsCompleted);
            }
        }
    }
}
