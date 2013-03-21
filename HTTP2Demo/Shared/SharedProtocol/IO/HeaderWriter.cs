using SharedProtocol.Compression;
using SharedProtocol.Framing;
using SharedProtocol.IO;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SharedProtocol.IO
{
    public class HeaderWriter : IDisposable
    {
        private readonly object _compressionLock;
        private readonly WriteQueue _writeQueue;
        private readonly CompressionProcessor _compressor;

        public HeaderWriter(WriteQueue writeQueue)
        {
            _writeQueue = writeQueue;
            _compressionLock = new object();
            _compressor = new CompressionProcessor();
        }

        // TODO: In the next draft there will only be one or two overloads for this.
        public void WriteSynStream(IList<KeyValuePair<string, string>> headers, int streamId, Priority priority, bool isFin, CancellationToken cancel)
        {
            // Lock because the compression context is shared across the whole connection.
            // We need to make sure requests are compressed and enqueued atomically.
            // TODO: What about cancellation and queue purging? That would break decompression.
            // TODO: Prioritization re-ordering will also break decompression. Scrap the priority queue.
            lock (_compressionLock)
            {
                byte[] headerBytes = FrameHelpers.SerializeHeaderBlock(headers);
                headerBytes = _compressor.Compress(headerBytes);
                SynStreamFrame frame = new SynStreamFrame(streamId, headerBytes);
                frame.IsFin = isFin;
                frame.Priority = priority;

                _writeQueue.WriteFrameAsync(frame, priority, cancel);
            }
        }

        // TODO: In the next draft there will only be one or two overloads for this.
        public void WriteSynReply(IList<KeyValuePair<string, string>> headers, int streamId, Priority priority, bool isFin, CancellationToken cancel)
        {
            // Lock because the compression context is shared across the whole connection.
            // We need to make sure requests are compressed and enqueued atomically.
            // TODO: What about cancellation and queue purging? That would break decompression.
            // TODO: Prioritization re-ordering will also break decompression. Scrap the priority queue.
            lock (_compressionLock)
            {
                byte[] headerBytes = FrameHelpers.SerializeHeaderBlock(headers);
                headerBytes = _compressor.Compress(headerBytes);
                SynReplyFrame frame = new SynReplyFrame(streamId, headerBytes);
                frame.IsFin = isFin;

                _writeQueue.WriteFrameAsync(frame, priority, cancel);
            }
        }

        // TODO: In the next draft there will only be one or two overloads for this.
        public void WriteHeaders(IList<KeyValuePair<string, string>> headers, int streamId, Priority priority, bool isFin, CancellationToken cancel)
        {
            // Lock because the compression context is shared across the whole connection.
            // We need to make sure requests are compressed and enqueued atomically.
            // TODO: What about cancellation and queue purging? That would break decompression.
            // TODO: Prioritization re-ordering will also break decompression. Scrap the priority queue.
            lock (_compressionLock)
            {
                byte[] headerBytes = FrameHelpers.SerializeHeaderBlock(headers);
                headerBytes = _compressor.Compress(headerBytes);
                HeadersFrame frame = new HeadersFrame(streamId, headerBytes);
                frame.IsFin = isFin;

                _writeQueue.WriteFrameAsync(frame, priority, cancel);
            }
        }

        public void Dispose()
        {
            _compressor.Dispose();
        }
    }
}
