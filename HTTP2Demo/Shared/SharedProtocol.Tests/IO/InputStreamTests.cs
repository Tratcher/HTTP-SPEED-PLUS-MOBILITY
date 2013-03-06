using Xunit;

namespace SharedProtocol.IO.Tests
{
    public class InputStreamTests
    {
        [Fact]
        public void WriteData_NoWindowUpdate()
        {
            int invokeCount = 0;
            InputStream input = new InputStream(100, delta => { invokeCount++; });
            input.Write(new byte[50], 0, 50);
            Assert.Equal(0, invokeCount);
        }

        [Fact]
        public void WriteAndReadData_WindowUpdate()
        {
            int invokeCount = 0;
            int aggregate = 0;
            InputStream input = new InputStream(100, delta => { invokeCount++; aggregate += delta; });

            input.Write(new byte[50], 0, 50);
            int read = input.Read(new byte[10], 0, 10);
            Assert.Equal(1, invokeCount);
            Assert.Equal(read, aggregate);
            Assert.Equal(10, aggregate);
        }

        [Fact]
        public void WriteAndMultipleReadData_WindowUpdatePerRead()
        {
            int invokeCount = 0;
            int aggregate = 0;
            InputStream input = new InputStream(100, delta => { invokeCount++; aggregate += delta; });
            input.Write(new byte[50], 0, 50);

            int read = input.Read(new byte[10], 0, 10);
            Assert.Equal(1, invokeCount);
            Assert.Equal(10, aggregate);
            Assert.Equal(10, read);

            read = input.Read(new byte[11], 0, 11);
            Assert.Equal(2, invokeCount);
            Assert.Equal(21, aggregate);
            Assert.Equal(11, read);

            read = input.Read(new byte[12], 0, 12);
            Assert.Equal(3, invokeCount);
            Assert.Equal(33, aggregate);
            Assert.Equal(12, read);
        }

        // TODO: We'll eventually want this to cause a stream reset.
        [Fact]
        public void WriteMoreThanCreditedData_NoException()
        {
            int invokeCount = 0;
            InputStream input = new InputStream(50, delta => { invokeCount++; });
            input.Write(new byte[100], 0, 100);
            Assert.Equal(0, invokeCount);

            int read = input.Read(new byte[1000], 0, 1000);
            Assert.Equal(100, read);
        }
    }
}
