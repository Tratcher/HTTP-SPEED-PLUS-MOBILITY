using SharedProtocol.Framing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedProtocol
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
        public Task WriteFrameAsync(Frame frame, CancellationToken cancel)
        {
            QueueEntry entry = new QueueEntry(frame.Buffer, cancel);
            _messageQueue.Enqueue(entry);
            _dataAvailable.Set();
            return entry.Task;
        }

        // Completes when any frames ahead of it have been processed
        // TODO: Have this only flush messages from one specific HTTP2Stream
        public Task FlushAsync(/*int streamId, */ CancellationToken cancel)
        {
            if (_messageQueue.Count == 0)
            {
                return Task.FromResult<object>(null);
            }

            QueueEntry entry = new QueueEntry(null, cancel);
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
                    if (entry.CancellationToken.IsCancellationRequested)
                    {
                        entry.Cancel();
                        continue;
                    }

                    try
                    {
                        if (entry.Buffer != null)
                        {
                            await _stream.WriteAsync(entry.Buffer, 0, entry.Buffer.Length, entry.CancellationToken);
                        }

                        entry.Complete();
                    }
                    catch (Exception ex)
                    {
                        entry.Fail(ex);
                    }
                }
                
                // TODO: What kind of recurring signal can we use here that won't block the thread?
                // _dataAvailable.WaitOne();
                // _dataAvailable.Reset();
                await Task.Delay(1000);
            }
        }

        private class QueueEntry
        {
            private TaskCompletionSource<object> _tcs;
            private byte[] _buffer; // null for FlushAsync
            private CancellationToken _cancel;

            public QueueEntry(byte[] buffer, CancellationToken cancel)
            {
                _buffer = buffer;
                _tcs = new TaskCompletionSource<object>();
                _cancel = cancel;
            }

            public byte[] Buffer { get { return _buffer; } }

            public Task Task { get { return _tcs.Task; } }

            public CancellationToken CancellationToken { get { return _cancel; } }

            internal void Complete()
            {
                _tcs.TrySetResult(null);
            }

            internal void Cancel()
            {
                _tcs.TrySetCanceled();
            }

            internal void Fail(Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }
    }
}
