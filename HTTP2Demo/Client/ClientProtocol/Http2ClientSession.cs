using SharedProtocol;
using SharedProtocol.Framing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientProtocol
{
    public class Http2ClientSession : Http2BaseSession
    {
        private Stream _sessionStream;
        private int _lastId = -1;

        public Http2ClientSession(Stream sessionStream, bool createHanshakeStream, CancellationToken handshakeCancel)
        {
            _sessionStream = sessionStream;

            if (createHanshakeStream)
            {
                // The HTTP/1.1 handshake already happened, we're just waiting for the first 2.0 control frame response
                CreateStream(handshakeCancel);
            }
        }

        public Http2ClientStream GetStream(int id)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _sessionStream.Dispose();
        }

        protected override void DispatchIncomingFrame(Frame frame)
        {
            throw new NotImplementedException();
        }

        public Http2ClientStream CreateStream(CancellationToken cancel)
        {
            Http2ClientStream handshakeStream = new Http2ClientStream(GetNextId(), _writeQueue, cancel);
            _activeStreams[handshakeStream.Id] = handshakeStream;
            return handshakeStream;
        }

        private int GetNextId()
        {
            _lastId += 2;
            return _lastId;
        }
    }
}
