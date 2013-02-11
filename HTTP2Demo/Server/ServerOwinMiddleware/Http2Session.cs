using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerOwinMiddleware
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    class Http2Session
    {
        private AppFunc _next;
        private X509Certificate[] _clientCerts;
        private IDictionary<string, object> _upgradeRequest;
        private Stream _rawStream;
        private CancellationToken _cancel;

        public Http2Session(AppFunc next, X509Certificate clientCert, IDictionary<string, object> upgradeRequest)
        {
            _next = next;
            _clientCerts = new X509Certificate[10];
            _clientCerts[0] = clientCert;
            _upgradeRequest = upgradeRequest;
        }

        public Task Start(Stream stream, CancellationToken cancel)
        {
            Contract.Assert(_rawStream == null, "Start called more than once");
            _rawStream = stream;
            _cancel = cancel;

            // Dispatch the original upgrade stream via _next;
            // Listen for incoming Http/2.0 frames
            // Send outgoing Http/2.0 frames
            // Complete the returned task only at the end of the session.  The connection will be terminated.

            Task incomingTask = PumpIncommingData();
            Task outgoingTask = PumpOutgoingData();

            return Task.WhenAll(incomingTask, outgoingTask);
        }

        // Read HTTP/2.0 frames from the raw stream and dispatch them to the appropriate virtual streams for processing.
        private Task PumpIncommingData()
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
