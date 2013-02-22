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
    public class Http2ClientSession : Http2BaseSession<Http2ClientStream>
    {
        private int _lastId = -1;

        public Http2ClientSession(Stream sessionStream, bool createHanshakeStream, CancellationToken handshakeCancel)
        {
            _sessionStream = sessionStream;
            _writeQueue = new WriteQueue(_sessionStream);
            _frameReader = new FrameReader(_sessionStream, CancellationToken.None);

            if (createHanshakeStream)
            {
                // The HTTP/1.1 handshake already happened, we're just waiting for the first 2.0 control frame response
                CreateStream(handshakeCancel);
            }
        }

        public void Dispose()
        {
            _sessionStream.Dispose();
        }

        protected override void DispatchIncomingFrame(Frame frame)
        {
            Http2ClientStream stream;
            if (frame.IsControl)
            {
                switch (frame.FrameType)
                {
                    case ControlFrameType.SynReply:
                        SynReplyFrame synReply = (SynReplyFrame)frame;
                        stream = GetStream(synReply.StreamId);
                        stream.SetReply(synReply);
                        break;

                    default:
                        base.DispatchIncomingFrame(frame);
                        break;
                }
            }
            else
            {
                base.DispatchIncomingFrame(frame);
            }
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
