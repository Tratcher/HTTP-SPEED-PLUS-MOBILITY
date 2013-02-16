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
using ServerProtocol.Framing;

namespace ServerProtocol
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class Http2Session
    {
        private AppFunc _next;
        private X509Certificate[] _clientCerts;
        private IDictionary<string, object> _upgradeRequest;
        private Stream _rawStream;
        private CancellationToken _cancel;
        private TransportInformation _transportInfo;
        private bool _goAwayReceived;
        private ConcurrentDictionary<int, Http2Stream> _activeStreams;
        private FrameReader _reader;
        private WriteQueue _writeQueue;

        public Http2Session(AppFunc next, TransportInformation transportInfo, IDictionary<string, object> upgradeRequest = null)
        {
            _next = next;
            _transportInfo = transportInfo;
            _clientCerts = new X509Certificate[Constants.DefaultClientCertVectorSize];
            _clientCerts[0] = _transportInfo.ClientCertificate;
            _upgradeRequest = upgradeRequest;
            _goAwayReceived = false;
            _activeStreams = new ConcurrentDictionary<int, Http2Stream>();
        }

        public Task Start(Stream stream, CancellationToken cancel)
        {
            Contract.Assert(_rawStream == null, "Start called more than once");
            _rawStream = stream;
            _cancel = cancel;
            _writeQueue = new WriteQueue(_rawStream);
            _reader = new FrameReader(_rawStream, _cancel);

            // Dispatch the original upgrade stream via _next;
            if (_upgradeRequest != null)
            {
                DispatchInitialRequest();
            }

            // Listen for incoming Http/2.0 frames
            Task incomingTask = PumpIncommingData();
            // Send outgoing Http/2.0 frames
            Task outgoingTask = PumpOutgoingData();

            // Complete the returned task only at the end of the session.  The connection will be terminated.
            return Task.WhenAll(incomingTask, outgoingTask);
        }

        private void DispatchInitialRequest()
        {
            Http2Stream stream = new Http2Stream(1, _transportInfo, _upgradeRequest, _writeQueue, _cancel);

            // GC the original
            _upgradeRequest = null;

            DispatchNewStream(1, stream);
        }

        private void DispatchNewStream(int id, Http2Stream stream)
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

        // Read HTTP/2.0 frames from the raw stream and dispatch them to the appropriate virtual streams for processing.
        private async Task PumpIncommingData()
        {
            while (!_goAwayReceived)
            {
                Frame frame = await _reader.ReadFrameAsync();
                DispatchIncomingFrame(frame);
            }
        }

        private void DispatchIncomingFrame(Frame frame)
        {
            if (frame.IsControl)
            {
                switch (frame.FrameType)
                {
                    // New incoming request stream
                    case ControlFrameType.SynStream:
                        SynFrame synFrame = (SynFrame)frame;
                        Http2Stream stream = new Http2Stream(synFrame, _transportInfo, _writeQueue, _cancel);
                        DispatchNewStream(synFrame.StreamId, stream);
                        return;

                    default:
                        throw new NotImplementedException("Cannot dispatch frame type: " + frame.FrameType);
                }
            }
            else
            {
                Http2Stream stream;
                if (!_activeStreams.TryGetValue(frame.DataStreamId, out stream))
                {
                    // TODO: Session already gone? Send a reset?
                    throw new NotImplementedException("Stream id not found: " + frame.DataStreamId);
                }
                else
                {
                    stream.ReceiveRequestData((DataFrame)frame);
                }
                return;
            }
            throw new NotImplementedException();
        }

        // Manage the outgoing queue of requests.
        private Task PumpOutgoingData()
        {
            return _writeQueue.PumpToStreamAsync();
        }
    }
}
