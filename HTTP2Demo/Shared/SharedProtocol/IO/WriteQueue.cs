using SharedProtocol.Framing;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharedProtocol.IO
{
    // Queue up frames to send, including headers, body, flush, pings, etc.
    public sealed class WriteQueue : IDisposable
    {
        private PriorityQueue _messageQueue;
        private Stream _stream;
        private ManualResetEvent _dataAvailable;
        private bool _disposed;
        private TaskCompletionSource<object> _readWaitingForData;

        public WriteQueue(Stream stream)
        {
            _messageQueue = new PriorityQueue();
            _stream = stream;
            _dataAvailable = new ManualResetEvent(false);
            _readWaitingForData = new TaskCompletionSource<object>();
        }

        // Queue up a fully rendered frame to send
        public Task WriteFrameAsync(Frame frame, Priority priority)
        {
            PriorityQueueEntry entry = new PriorityQueueEntry(frame, priority);
            Enqueue(entry);
            SignalDataAvailable();
            return entry.Task;
        }

        // Completes when any frames ahead of it have been processed
        // TODO: Have this only flush messages from one specific HTTP2Stream
        public Task FlushAsync(/*int streamId, */ Priority priority)
        {
            if (!IsDataAvailable)
            {
                return Task.FromResult<object>(null);
            }

            PriorityQueueEntry entry = new PriorityQueueEntry(priority);
            Enqueue(entry);
            SignalDataAvailable();
            return entry.Task;
        }

        private void Enqueue(PriorityQueueEntry entry)
        {
            _messageQueue.Enqueue(entry);
        }

        private bool TryDequeue(out PriorityQueueEntry entry)
        {
            return _messageQueue.TryDequeue(out entry);
        }

        private bool IsDataAvailable
        {
            get
            {
                return _messageQueue.IsDataAvailable;
            }
        }

        public async Task PumpToStreamAsync()
        {
            while (!_disposed)
            {
                // TODO: Attempt overlapped writes?
                PriorityQueueEntry entry;
                while (TryDequeue(out entry))
                {
                    try
                    {
                        if (entry.IsFlush)
                        {
                            await _stream.FlushAsync();
                        }
                        else
                        {
                            await _stream.WriteAsync(entry.Buffer, 0, entry.Buffer.Length);
                        }

                        entry.Complete();
                    }
                    catch (Exception ex)
                    {
                        entry.Fail(ex);
                    }
                }

                await WaitForDataAsync();
            }
        }

        private void SignalDataAvailable()
        {
            // Dispatch, as TrySetResult will synchronously execute the waiters callback and block our Write.
            Task.Run(() => _readWaitingForData.TrySetResult(null));
        }

        private Task WaitForDataAsync()
        {
            _readWaitingForData = new TaskCompletionSource<object>();

            if (IsDataAvailable || _disposed)
            {
                // Race, data could have arrived before we created the TCS.
                _readWaitingForData.TrySetResult(null);
            }

            return _readWaitingForData.Task;
        }

        public void Dispose()
        {
            _disposed = true;
            _readWaitingForData.TrySetResult(null);
        }
    }
}
