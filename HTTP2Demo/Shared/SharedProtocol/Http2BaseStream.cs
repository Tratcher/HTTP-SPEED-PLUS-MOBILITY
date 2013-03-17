using SharedProtocol.Compression;
using SharedProtocol.Framing;
using SharedProtocol.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedProtocol
{
    public abstract class Http2BaseStream : IDisposable
    {
        protected int _id;
        protected StreamState _state;
        protected WriteQueue _writeQueue;
        protected CompressionProcessor _compressor;
        protected CancellationToken _cancel;
        protected int _version;
        protected Priority _priority;
        protected Stream _incomingStream;
        protected OutputStream _outputStream;

        protected Http2BaseStream(int id, WriteQueue writeQueue, CancellationToken cancel)
        {
            _id = id;
            _writeQueue = writeQueue;
            _cancel = cancel;
            _compressor = new CompressionProcessor();
        }

        public int Id
        {
            get { return _id; }
        }

        protected CompressionProcessor Compressor
        {
            get { return _compressor; }
        }

        protected bool FinSent
        {
            get { return (_state & StreamState.FinSent) == StreamState.FinSent; }
            set { Contract.Assert(value); _state |= StreamState.FinSent; }
        }

        protected bool FinReceived
        {
            get { return (_state & StreamState.FinReceived) == StreamState.FinReceived; }
            set { Contract.Assert(value); _state |= StreamState.FinReceived; }
        }

        protected bool ResetSent
        {
            get { return (_state & StreamState.ResetSent) == StreamState.ResetSent; }
            set { Contract.Assert(value); _state |= StreamState.ResetSent; }
        }

        protected bool ResetReceived
        {
            get { return (_state & StreamState.ResetReceived) == StreamState.ResetReceived; }
            set { Contract.Assert(value); _state |= StreamState.ResetReceived; }
        }

        protected bool Disposed
        {
            get { return (_state & StreamState.Disposed) == StreamState.Disposed; }
            set { Contract.Assert(value); _state |= StreamState.Disposed; }
        }

        // Additional data has arrived for the request stream.  Add it to our request stream buffer, 
        // update any necessary state (e.g. FINs), and trigger any waiting readers.
        public void ReceiveData(DataFrame dataFrame)
        {
            if (Disposed)
            {
                // TODO: Send reset?
                return;
            }

            Contract.Assert(_incomingStream != null);
            ArraySegment<byte> data = dataFrame.Data;
            // TODO: Decompression?
            _incomingStream.Write(data.Array, data.Offset, data.Count);
            if (dataFrame.IsFin)
            {
                FinReceived = true;
                _incomingStream.Dispose();
            }
        }

        // Make sure the request/response has been started, and that the headers have been sent.
        // Start it if possible, throw otherwise.
        public abstract void EnsureStarted();

        public void SendExtraHeaders(IList<KeyValuePair<string, string>> headers, bool endOfMessage)
        {
            // Make sure the initial headers have been sent.
            EnsureStarted();
            // Assert the body is incomplete.
            Contract.Assert(!FinSent && !ResetSent && !ResetReceived);

            byte[] headerBytes = FrameHelpers.SerializeHeaderBlock(headers);
            headerBytes = Compressor.Compress(headerBytes);
            HeadersFrame frame = new HeadersFrame(_id, headerBytes);
            frame.IsFin = endOfMessage;

            // Set end-of-message state so we don't try to send an empty fin data frame.
            if (endOfMessage)
            {
                FinSent = true;
            }

            _writeQueue.WriteFrameAsync(frame, _priority, _cancel);
        }

        public void ReceiveExtraHeaders(HeadersFrame headerFrame)
        {
            if (Disposed)
            {
                return;
            }

            // TODO: Can this be offloaded to a worker thread? We don't want to do busywork (like decompression) and block the message pump.
            // Would that lead to potential ordering & concurrency issues? Ordering shouldn't be a problem because headers should stack.
            byte[] headerBytes = Compressor.Decompress(headerFrame.CompressedHeaders);
            IList<KeyValuePair<string, string>> headers = FrameHelpers.DeserializeHeaderBlock(headerBytes);

            // TODO: Where do we put them? How do we notify the stream owner that they're here?

            if (headerFrame.IsFin)
            {
                FinReceived = true;
                _incomingStream.Dispose();
            }
        }

        public void UpdateWindowSize(int delta)
        {
            Contract.Assert(_outputStream != null);
            _outputStream.AddFlowControlCredit(delta);
        }

        protected void SendWindowUpdate(int delta)
        {
            WindowUpdateFrame windowUpdate = new WindowUpdateFrame(Id, delta);
            _writeQueue.WriteFrameAsync(windowUpdate, Priority.Control, _cancel);
        }

        public virtual void Reset(ResetStatusCode statusCode)
        {
            ResetReceived = true;
            if (_outputStream != null)
            {
                _outputStream.Dispose();
            }
            if (_incomingStream != null && _incomingStream != Stream.Null && !FinReceived)
            {
                InputStream inputStream = (InputStream)_incomingStream;
                inputStream.Abort(statusCode.ToString());
            }
            _writeQueue.PurgeStream(Id);

            // Not disposing here because many of the resources may still be in use.
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            Disposed = true;
            if (disposing)
            {
                _compressor.Dispose();

                if (_incomingStream != null)
                {
                    _incomingStream.Dispose();
                }

                if (_outputStream != null)
                {
                    _outputStream.Dispose();
                }
            }
        }
    }
}
