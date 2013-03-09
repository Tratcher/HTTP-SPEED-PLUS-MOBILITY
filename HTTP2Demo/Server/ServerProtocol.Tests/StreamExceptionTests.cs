using Owin.Types;
using SharedProtocol.Compression;
using SharedProtocol.Framing;
using SharedProtocol.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ServerProtocol.Tests
{
    public class StreamExceptionTests
    {
        [Fact]
        public void StreamException_BeforeSendingHeaders_500Response()
        {
            MemoryStream rawStream = new MemoryStream();
            WriteQueue writeQueue = new WriteQueue(rawStream);
            Task pumpTask = writeQueue.PumpToStreamAsync();
            Http2ServerStream stream = new Http2ServerStream(1, new TransportInformation(), CreateEnvironment(), writeQueue, CancellationToken.None);
            Task task = stream.Run(env => { throw new NotImplementedException(); });
            Assert.True(task.IsCompleted);
            Assert.False(task.IsFaulted);
            writeQueue.FlushAsync(Priority.Pri7, CancellationToken.None).Wait();

            rawStream.Seek(0, SeekOrigin.Begin);
            FrameReader reader = new FrameReader(rawStream, true, CancellationToken.None);
            Frame frame = reader.ReadFrameAsync().Result;
            Assert.True(frame.IsControl);
            ControlFrame controlFrame = (ControlFrame)frame;
            Assert.Equal(ControlFrameType.SynReply, controlFrame.FrameType);
            SynReplyFrame reply = (SynReplyFrame)controlFrame;
            CompressionProcessor compresser = new CompressionProcessor();
            var headers = FrameHelpers.DeserializeHeaderBlock(compresser.Decompress(reply.CompressedHeaders));
            Assert.Equal(2, headers.Count);
            Assert.Equal(":status", headers[0].Key);
            Assert.Equal("500 Internal Server Error", headers[0].Value);
            Assert.True(reply.IsFin);
        }

        [Fact]
        public void StreamException_AfterSendingHeaders_Reset()
        {
            MemoryStream rawStream = new MemoryStream();
            WriteQueue writeQueue = new WriteQueue(rawStream);
            Task pumpTask = writeQueue.PumpToStreamAsync();
            Http2ServerStream stream = new Http2ServerStream(1, new TransportInformation(), CreateEnvironment(), writeQueue, CancellationToken.None);
            Task task = stream.Run(env =>
            {
                new OwinResponse(env).Write("Hello World");
                throw new NotImplementedException();
            });
            Assert.True(task.IsCompleted);
            Assert.False(task.IsFaulted);
            writeQueue.FlushAsync(Priority.Pri7, CancellationToken.None).Wait();

            rawStream.Seek(0, SeekOrigin.Begin);
            FrameReader reader = new FrameReader(rawStream, true, CancellationToken.None);

            SynReplyFrame reply = (SynReplyFrame)reader.ReadFrameAsync().Result;
            var headers = FrameHelpers.DeserializeHeaderBlock(new CompressionProcessor().Decompress(reply.CompressedHeaders));
            Assert.Equal(2, headers.Count);
            Assert.Equal(":status", headers[0].Key);
            Assert.Equal("200 OK", headers[0].Value);
            Assert.False(reply.IsFin);

            DataFrame data = (DataFrame)reader.ReadFrameAsync().Result;
            Assert.Equal("Hello World", FrameHelpers.GetAsciiAt(data.Data));

            RstStreamFrame reset = (RstStreamFrame)reader.ReadFrameAsync().Result;
            Assert.Equal(ResetStatusCode.InternalError, reset.StatusCode);
        }

        private IDictionary<string, object> CreateEnvironment()
        {
            Dictionary<string, object> environment = new Dictionary<string, object>();
            environment["owin.Version"] = "1.0";
            return environment;
        }
    }
}
