using ServerProtocol.Framing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerProtocol
{
    // Queue up frames to send, including headers, body, flush, pings, etc.
    // TODO: Sort by priority?
    public class WriteQueue
    {
        private ConcurrentQueue<QueueEntry> _messageQueue;
        private Stream _stream;
        private ManualResetEvent _dataAvailable;

        public WriteQueue(Stream stream)
        {
            _messageQueue = new ConcurrentQueue<QueueEntry>();
            _stream = stream;
            _dataAvailable = new ManualResetEvent(false);
        }

        // Queue up a fully rendered frame to send
        public Task WriteFrameAsync(Frame frame)
        {
            QueueEntry entry = new QueueEntry(frame.Buffer);
            _messageQueue.Enqueue(entry);
            _dataAvailable.Set();
            return entry.Task;
        }

        // Completes when any frames ahead of it have been processed
        public Task FlushAsync(/*int streamId */)
        {
            if (_messageQueue.Count == 0)
            {
                return Task.FromResult<object>(null);
            }

            QueueEntry entry = new QueueEntry(null);
            _messageQueue.Enqueue(entry);
            _dataAvailable.Set();
            return entry.Task;
        }

        public async Task PumpToStreamAsync()
        {
            while (true)
            {
                QueueEntry entry;
                while (_messageQueue.TryDequeue(out entry))
                {
                    // TODO: Check if the write was canceled?

                    if (entry.Buffer != null)
                    {
                        await _stream.WriteAsync(entry.Buffer, 0, entry.Buffer.Length);
                    }

                    entry.Complete();
                }
                
                // TODO: What kind of recurring signal can we use here that won't block the thread?
                _dataAvailable.WaitOne();
                _dataAvailable.Reset();
            }
        }

        private class QueueEntry
        {
            private TaskCompletionSource<object> _tcs;
            private byte[] _buffer; // null for FlushAsync

            public QueueEntry(byte[] buffer)
            {
                _buffer = buffer;
                _tcs = new TaskCompletionSource<object>();
            }

            public byte[] Buffer { get { return _buffer; } }

            public Task Task { get { return _tcs.Task; } }

            internal void Complete()
            {
                _tcs.TrySetResult(null);
            }
        }
    }
}
