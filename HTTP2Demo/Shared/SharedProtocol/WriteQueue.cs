﻿using SharedProtocol.Framing;
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
    public sealed class WriteQueue : IDisposable
    {
        private ConcurrentQueue<QueueEntry> _messageQueue;
        private Stream _stream;
        private ManualResetEvent _dataAvailable;
        private bool _disposed;
        private TaskCompletionSource<object> _readWaitingForData;

        public WriteQueue(Stream stream)
        {
            _messageQueue = new ConcurrentQueue<QueueEntry>();
            _stream = stream;
            _dataAvailable = new ManualResetEvent(false);
            _readWaitingForData = new TaskCompletionSource<object>();
        }

        // Queue up a fully rendered frame to send
        public Task WriteFrameAsync(Frame frame, Priority priority, CancellationToken cancel)
        {
            QueueEntry entry = new QueueEntry(frame.Buffer, priority, cancel);
            _messageQueue.Enqueue(entry);
            SignalDataAvailable();
            return entry.Task;
        }

        // Completes when any frames ahead of it have been processed
        // TODO: Have this only flush messages from one specific HTTP2Stream
        public Task FlushAsync(/*int streamId, */ Priority priority, CancellationToken cancel)
        {
            if (_messageQueue.Count == 0)
            {
                return Task.FromResult<object>(null);
            }

            QueueEntry entry = new QueueEntry(null, priority, cancel);
            _messageQueue.Enqueue(entry);
            SignalDataAvailable();
            return entry.Task;
        }

        public async Task PumpToStreamAsync()
        {
            while (!_disposed)
            {
                // TODO: Attempt overlapped writes?
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

            if (!_messageQueue.IsEmpty || _disposed)
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

        private class QueueEntry
        {
            private TaskCompletionSource<object> _tcs;
            private byte[] _buffer; // null for FlushAsync
            private CancellationToken _cancel;
            private Priority _priority;

            public QueueEntry(byte[] buffer, Priority priority, CancellationToken cancel)
            {
                _buffer = buffer;
                _priority = priority;
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
