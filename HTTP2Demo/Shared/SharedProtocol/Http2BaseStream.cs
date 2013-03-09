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
        protected WriteQueue _writeQueue;
        protected CompressionProcessor _compressor;
        protected CancellationToken _cancel;
        protected int _version;
        protected Priority _priority;
        protected Stream _incomingStream;
        protected bool _disposed;
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

        // Additional data has arrived for the request stream.  Add it to our request stream buffer, 
        // update any necessary state (e.g. FINs), and trigger any waiting readers.
        public void ReceiveData(DataFrame dataFrame)
        {
            if (_disposed)
            {
                return;
            }

            Contract.Assert(_incomingStream != null);
            ArraySegment<byte> data = dataFrame.Data;
            // TODO: Decompression?
            _incomingStream.Write(data.Array, data.Offset, data.Count);
            if (dataFrame.IsFin)
            {
                // TODO: How can we signal the difference between an aborted stream and a finished stream? CancellationToken?
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

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            _disposed = true;
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
