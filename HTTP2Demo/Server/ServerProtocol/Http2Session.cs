using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        public Http2Session(AppFunc next, TransportInformation transportInfo, IDictionary<string, object> upgradeRequest = null)
        {
            _next = next;
            _transportInfo = transportInfo;
            _clientCerts = new X509Certificate[10];
            _clientCerts[0] = _transportInfo.ClientCertificate;
            _upgradeRequest = upgradeRequest;
            _goAwayReceived = false;
        }

        public Task Start(Stream stream, CancellationToken cancel)
        {
            Contract.Assert(_rawStream == null, "Start called more than once");
            _rawStream = stream;
            _cancel = cancel;

            // Dispatch the original upgrade stream via _next;
            if (_upgradeRequest != null)
            {
                // TODO:
            }

            // Listen for incoming Http/2.0 frames
            Task incomingTask = PumpIncommingData();
            // Send outgoing Http/2.0 frames
            Task outgoingTask = PumpOutgoingData();

            // Complete the returned task only at the end of the session.  The connection will be terminated.
            return Task.WhenAll(incomingTask, outgoingTask);
        }

        // Read HTTP/2.0 frames from the raw stream and dispatch them to the appropriate virtual streams for processing.
        private async Task PumpIncommingData()
        {
            while (!_goAwayReceived)
            {
                byte[] frame = await ReadFrameAsync();
                DispatchIncomingFrame(frame);
            }
        }

        private Task<byte[]> ReadFrameAsync()
        {
            throw new NotImplementedException();
        }

        private void DispatchIncomingFrame(byte[] frame)
        {
            throw new NotImplementedException();
        }

        // Manage the outgoing queue of requests.
        private Task PumpOutgoingData()
        {
            throw new NotImplementedException();
        }
    }
}
