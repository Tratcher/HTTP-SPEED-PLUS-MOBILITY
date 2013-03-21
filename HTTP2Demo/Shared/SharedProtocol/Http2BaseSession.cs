using SharedProtocol.Compression;
using SharedProtocol.Framing;
using SharedProtocol.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharedProtocol
{
    public abstract class Http2BaseSession<T> : IDisposable where T: Http2BaseStream
    {
        protected bool _goAwayReceived;
        protected ConcurrentDictionary<int, T> _activeStreams;
        protected FrameReader _frameReader;
        protected WriteQueue _writeQueue;
        protected Stream _sessionStream;
        protected Ping _currentPing;
        protected int _nextPingId;
        protected CancellationToken _cancel;
        protected bool _disposed;
        protected ISettingsManager _settingsManager;
        protected HeaderWriter _headerWriter;
        protected CompressionProcessor _decompressor;

        protected Http2BaseSession()
        {
            _goAwayReceived = false;
            _activeStreams = new ConcurrentDictionary<int, T>();
            _settingsManager = new EmptySettingsManager();
            _decompressor = new CompressionProcessor();
        }

        protected CompressionProcessor Decompressor
        {
            get { return _decompressor; }
        }

        protected Task StartPumps()
        {
            // TODO: Assert not started

            // Listen for incoming Http/2.0 frames
            Task incomingTask = PumpIncommingData();
            // Send outgoing Http/2.0 frames
            Task outgoingTask = PumpOutgoingData();

            return Task.WhenAll(incomingTask, outgoingTask);
        }

        // Read HTTP/2.0 frames from the raw stream and dispatch them to the appropriate virtual streams for processing.
        private async Task PumpIncommingData()
        {
            while (!_goAwayReceived && !_disposed)
            {
                Frame frame;
                try
                {
                    frame = await _frameReader.ReadFrameAsync();
                }
                catch (Exception)
                {
                    // Read failure, abort the connection/session.
                    Dispose();
                    throw;
                }

                if (frame == null)
                {
                    // Stream closed
                    Dispose();
                    break;
                }

                DispatchIncomingFrame(frame);
            }
        }

        protected virtual void DispatchIncomingFrame(Frame frame)
        {
            T stream;
            if (frame.IsControl)
            {
                switch (frame.FrameType)
                {
                    case ControlFrameType.Ping:
                        PingFrame pingFrame = (PingFrame)frame;
                        ReceivePing(pingFrame.Id);
                        break;
                    case ControlFrameType.Settings:
                        _settingsManager.ProcessSettings((SettingsFrame)frame);
                        break;
                    case ControlFrameType.WindowUpdate:
                        WindowUpdateFrame windowFrame = (WindowUpdateFrame)frame;
                        stream = GetStream(windowFrame.StreamId);
                        stream.UpdateWindowSize(windowFrame.Delta);
                        break;
                    case ControlFrameType.Headers:
                        HeadersFrame headersFrame = (HeadersFrame)frame;
                        stream = GetStream(headersFrame.StreamId);
                        byte[] decompressedHeaders = Decompressor.Decompress(headersFrame.CompressedHeaders);
                        IList<KeyValuePair<string, string>> headers = FrameHelpers.DeserializeHeaderBlock(decompressedHeaders);
                        stream.ReceiveExtraHeaders(headersFrame, headers);
                        break;
                    default:
                        throw new NotImplementedException(frame.FrameType.ToString());
                }
            }
            else
            {
                DataFrame dataFrame = (DataFrame)frame;
                stream = GetStream(dataFrame.StreamId);
                stream.ReceiveData(dataFrame);
            }
        }

        // Manage the outgoing queue of requests.
        private Task PumpOutgoingData()
        {
            return _writeQueue.PumpToStreamAsync();
        }

        public T GetStream(int id)
        {
            T stream;
            if (!_activeStreams.TryGetValue(id, out stream))
            {
                // TODO: Session already gone? Send a reset?
                throw new NotImplementedException("Stream id not found: " + id);
            }
            return stream;
        }

        public Task<TimeSpan> PingAsync()
        {
            Contract.Assert(_currentPing == null || _currentPing.Task.IsCompleted);
            Ping ping = new Ping(_nextPingId);
            _nextPingId += 2;
            _currentPing = ping;
            PingFrame pingFrame = new PingFrame(_currentPing.Id);
            _writeQueue.WriteFrameAsync(pingFrame, Priority.Ping);
            return ping.Task;
        }

        public void ReceivePing(int id)
        {
            // Even or odd?
            if (id % 2 != _nextPingId % 2)
            {
                // Not one of ours, response ASAP
                _writeQueue.WriteFrameAsync(new PingFrame(id), Priority.Ping);
                return;
            }

            Ping currentPing = _currentPing;
            if (currentPing != null && id == currentPing.Id)
            {
                currentPing.Complete();
            }
            // Ignore extra pings
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            _disposed = true;
            if (!disposing)
            {
                return;
            }

            if (_writeQueue != null)
            {
                _writeQueue.Dispose();
            }

            Ping currentPing = _currentPing;
            if (currentPing != null)
            {
                currentPing.Cancel();
            }

            // Dispose of all streams
            foreach (T stream in _activeStreams.Values)
            {
                stream.Reset(ResetStatusCode.Cancel);
                stream.Dispose();
            }

            // Just disposing of the stream should stop the FrameReader and the WriteQueue
            if (_sessionStream != null)
            {
                _sessionStream.Dispose();
            }

            _headerWriter.Dispose();
            _decompressor.Dispose();
        }
    }
}
