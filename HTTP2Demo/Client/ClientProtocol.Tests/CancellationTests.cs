using SharedProtocol.Compression;
using SharedProtocol.Framing;
using SharedProtocol.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ClientProtocol.Tests
{
    public class CancellationTests
    {
        [Fact]
        public void CancelRequestAfterSendingHeaders_ResetSent()
        {
            MemoryStream rawStream = new MemoryStream();
            WriteQueue writeQueue = new WriteQueue(rawStream);
            HeaderWriter headerWriter = new HeaderWriter(writeQueue);
            Task pumpTask = writeQueue.PumpToStreamAsync();
            CancellationTokenSource cts = new CancellationTokenSource();
            Http2ClientStream clientStream = new Http2ClientStream(1, Priority.Pri3, writeQueue, headerWriter, cts.Token);
            clientStream.StartRequest(GenerateHeaders(), 0, false);
            Task responseTask = clientStream.GetResponseAsync();
            writeQueue.FlushAsync(Priority.Pri7, CancellationToken.None).Wait();

            cts.Cancel();
            Assert.True(responseTask.IsCanceled);
            writeQueue.FlushAsync(Priority.Pri7, CancellationToken.None).Wait();

            rawStream.Seek(0, SeekOrigin.Begin);
            FrameReader reader = new FrameReader(rawStream, true, CancellationToken.None);

            SynStreamFrame synFrame = (SynStreamFrame)reader.ReadFrameAsync().Result;
            Assert.True(synFrame.IsFin);

            RstStreamFrame rstFrame = (RstStreamFrame)reader.ReadFrameAsync().Result;
            Assert.Equal(ResetStatusCode.Cancel, rstFrame.StatusCode);
        }

        [Fact]
        public void CancelRequestAfterReceivingHeaders_ResetSent()
        {
            MemoryStream rawStream = new MemoryStream();
            WriteQueue writeQueue = new WriteQueue(rawStream);
            HeaderWriter headerWriter = new HeaderWriter(writeQueue);
            Task pumpTask = writeQueue.PumpToStreamAsync();
            CancellationTokenSource cts = new CancellationTokenSource();
            Http2ClientStream clientStream = new Http2ClientStream(1, Priority.Pri3, writeQueue, headerWriter, cts.Token);
            clientStream.StartRequest(GenerateHeaders(), 0, false);
            Task<IList<KeyValuePair<string, string>>> responseTask = clientStream.GetResponseAsync();
            writeQueue.FlushAsync(Priority.Pri7, CancellationToken.None).Wait();

            clientStream.SetReply(GenerateHeaders(), false);

            var response = responseTask.Result;

            cts.Cancel();
            writeQueue.FlushAsync(Priority.Pri7, CancellationToken.None).Wait();

            rawStream.Seek(0, SeekOrigin.Begin);
            FrameReader reader = new FrameReader(rawStream, true, CancellationToken.None);

            SynStreamFrame synFrame = (SynStreamFrame)reader.ReadFrameAsync().Result;
            Assert.True(synFrame.IsFin);

            RstStreamFrame rstFrame = (RstStreamFrame)reader.ReadFrameAsync().Result;
            Assert.Equal(ResetStatusCode.Cancel, rstFrame.StatusCode);
        }

        [Fact]
        public void CancelRequestAfterReceivingFin_ResetSent()
        {
            MemoryStream rawStream = new MemoryStream();
            WriteQueue writeQueue = new WriteQueue(rawStream);
            HeaderWriter headerWriter = new HeaderWriter(writeQueue);
            Task pumpTask = writeQueue.PumpToStreamAsync();
            CancellationTokenSource cts = new CancellationTokenSource();
            Http2ClientStream clientStream = new Http2ClientStream(1, Priority.Pri3, writeQueue, headerWriter, cts.Token);
            clientStream.StartRequest(GenerateHeaders(), 0, false);
            Task<IList<KeyValuePair<string, string>>> responseTask = clientStream.GetResponseAsync();
            writeQueue.FlushAsync(Priority.Pri7, CancellationToken.None).Wait();

            clientStream.SetReply(GenerateHeaders(), true);

            var response = responseTask.Result;

            cts.Cancel();
            writeQueue.FlushAsync(Priority.Pri7, CancellationToken.None).Wait();

            rawStream.Seek(0, SeekOrigin.Begin);
            FrameReader reader = new FrameReader(rawStream, true, CancellationToken.None);

            SynStreamFrame synFrame = (SynStreamFrame)reader.ReadFrameAsync().Result;
            Assert.True(synFrame.IsFin);

            // TODO: Should we send a reset after sending and receiving a fin?
            // Because the write queue gets purged, we're not positive our fin got sent.
            RstStreamFrame rstFrame = (RstStreamFrame)reader.ReadFrameAsync().Result;
            Assert.Equal(ResetStatusCode.Cancel, rstFrame.StatusCode);
        }

        private IList<KeyValuePair<string, string>> GenerateHeaders()
        {
            return new List<KeyValuePair<string, string>>()
            {
            };
        }
    }
}
