using ServerProtocol.Framing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerProtocol
{
    public class ResponseStream : Stream
    {
        private WriteQueue _writeQueue;
        private Action _onStart;
        private int _streamId;

        public ResponseStream(int streamId, WriteQueue writeQueue, Action onStart)
        {
            _streamId = streamId;
            _writeQueue = writeQueue;
            _onStart = onStart;
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public override void Flush()
        {
            _onStart();
            _writeQueue.FlushAsync(CancellationToken.None).Wait();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            _onStart();
            return _writeQueue.FlushAsync(cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int ReadByte()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotImplementedException();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override void WriteByte(byte value)
        {
            base.WriteByte(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count).Wait();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // TODO: Compression?
            _onStart();
            DataFrame dataFrame = new DataFrame(_streamId, new ArraySegment<byte>(buffer, offset, count));
            // TODO: Flags?
            return _writeQueue.WriteFrameAsync(dataFrame, cancellationToken);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            // TODO:
            throw new NotImplementedException();
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            // TODO:
            throw new NotImplementedException();
        }
    }
}
