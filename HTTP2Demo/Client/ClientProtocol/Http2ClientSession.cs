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

        public Http2ClientStream GetStream(int id)
        {
            return (Http2ClientStream)_activeStreams[id];
        }

        public void Dispose()
        {
            _sessionStream.Dispose();
        }

        protected override void DispatchIncomingFrame(Frame frame)
        {
            Http2BaseStream stream;
            if (frame.IsControl)
            {
                switch (frame.FrameType)
                {
                    // New incoming request stream
                    case ControlFrameType.SynReply:
                        SynReplyFrame synReply = (SynReplyFrame)frame;
                        if (!_activeStreams.TryGetValue(synReply.StreamId, out stream))
                        {
                            // TODO: Session already gone? Send a reset?
                            throw new NotImplementedException("Stream id not found: " + frame.DataStreamId);
                        }
                        else
                        {
                            Http2ClientStream clientStream = (Http2ClientStream)stream;
                            clientStream.SetReply(synReply);
                        }
                        break;

                    default:
                        throw new NotImplementedException("Cannot dispatch frame type: " + frame.FrameType);
                }
            }
            else
            {
                if (!_activeStreams.TryGetValue(frame.DataStreamId, out stream))
                {
                    // TODO: Session already gone? Send a reset?
                    throw new NotImplementedException("Stream id not found: " + frame.DataStreamId);
                }
                else
                {
                    stream.ReceiveData((DataFrame)frame);
                }
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
