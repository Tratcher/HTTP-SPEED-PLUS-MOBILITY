using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedProtocol.Framing;
using SharedProtocol;

namespace ServerProtocol
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class Http2ServerSession : Http2BaseSession<Http2ServerStream>
    {
        private AppFunc _next;
        private X509Certificate[] _clientCerts;
        private IDictionary<string, object> _upgradeRequest;
        private CancellationToken _cancel;
        private TransportInformation _transportInfo;

        public Http2ServerSession(AppFunc next, TransportInformation transportInfo, IDictionary<string, object> upgradeRequest = null)
            : base()
        {
            _next = next;
            _transportInfo = transportInfo;
            _clientCerts = new X509Certificate[Constants.DefaultClientCertVectorSize];
            _clientCerts[0] = _transportInfo.ClientCertificate;
            _upgradeRequest = upgradeRequest;
        }

        public Task Start(Stream stream, CancellationToken cancel)
        {
            Contract.Assert(_sessionStream == null, "Start called more than once");
            _sessionStream = stream;
            _cancel = cancel;
            _writeQueue = new WriteQueue(_sessionStream);
            _frameReader = new FrameReader(_sessionStream, _cancel);

            // Dispatch the original upgrade stream via _next;
            if (_upgradeRequest != null)
            {
                DispatchInitialRequest();
            }

            // Listen for incoming Http/2.0 frames
            // Send outgoing Http/2.0 frames
            // Complete the returned task only at the end of the session.  The connection will be terminated.
            return StartPumps();
        }

        private void DispatchInitialRequest()
        {
            Http2ServerStream stream = new Http2ServerStream(1, _transportInfo, _upgradeRequest, _writeQueue, _cancel);

            // GC the original
            _upgradeRequest = null;

            DispatchNewStream(1, stream);
        }

        private void DispatchNewStream(int id, Http2ServerStream stream)
        {
            _activeStreams[id] = stream;
            Task.Run(() => stream.Run(_next))
                .ContinueWith(task =>
                {
                    CompleteResponse(stream.Id, task);
                });
        }

        // Remove the stream from _activeStreams
        private void CompleteResponse(int id, Task appFuncTask)
        {
            // TODO: Should this happen inside of the Http2Stream?
            // throw new NotImplementedException();
        }

        protected override void DispatchIncomingFrame(Frame frame)
        {
            if (frame.IsControl)
            {
                switch (frame.FrameType)
                {
                    // New incoming request stream
                    case ControlFrameType.SynStream:
                        SynStreamFrame synFrame = (SynStreamFrame)frame;
                        // TODO: Validate this stream ID is in the correct sequence and not already in use.
                        Http2ServerStream stream = new Http2ServerStream(synFrame, _transportInfo, _writeQueue, _cancel);
                        DispatchNewStream(synFrame.StreamId, stream);
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
    }
}
