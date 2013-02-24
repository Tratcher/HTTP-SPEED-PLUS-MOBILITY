using SharedProtocol;
using SharedProtocol.Framing;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientProtocol
{
    public class Http2ClientSession : Http2BaseSession<Http2ClientStream>
    {
        private int _lastId = -1;

        public Http2ClientSession(bool createHanshakeStream, CancellationToken handshakeCancel)
        {
            _nextPingId = 1; // Client pings are odd
            if (createHanshakeStream)
            {
                // The HTTP/1.1 handshake already happened, we're just waiting for the first 2.0 control frame response
                CreateStream(handshakeCancel);
            }
        }

        public override Task Start(Stream stream, CancellationToken cancel)
        {
            Contract.Assert(_sessionStream == null, "Start called more than once");
            _sessionStream = stream;
            _cancel = cancel;
            _writeQueue = new WriteQueue(_sessionStream);
            _frameReader = new FrameReader(_sessionStream, true, _cancel);
            return StartPumps();
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

        public Http2ClientStream SendRequest(IList<KeyValuePair<string, string>> pairs, X509Certificate clientCert, bool hasRequestBody, CancellationToken cancel)
        {
            Http2ClientStream clientStream = CreateStream(cancel);

            int certIndex = UpdateClientCertificates(clientCert);

            // Serialize the request as a SynStreamFrame and submit it. (FIN if there is no body)
            byte[] headerBytes = FrameHelpers.SerializeHeaderBlock(pairs);
            headerBytes = clientStream.Compressor.Compress(headerBytes);
            SynStreamFrame frame = new SynStreamFrame(clientStream.Id, headerBytes);
            frame.CertClot = certIndex;
            frame.IsFin = !hasRequestBody;
            clientStream.StartRequest(frame);

            return clientStream;
        }

        // Maintain the list of client certificates in sync with the server
        // Send a cert update if the server doesn't have this specific client cert yet.
        private int UpdateClientCertificates(X509Certificate clientCert)
        {
            // throw new NotImplementedException();
            return 0;
        }

        private Http2ClientStream CreateStream(CancellationToken cancel)
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
