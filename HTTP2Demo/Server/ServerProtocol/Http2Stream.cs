using Owin.Types;
using ServerProtocol.Framing;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerProtocol
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    class Http2Stream
    {
        private int _id;
        private TransportInformation _transportInfo;
        private IDictionary<string, object> _environment;
        private OwinRequest _owinRequest;
        private OwinResponse _owinResponse;
        
        // For use with incoming HTTP2 binary frames
        private Http2Stream(TransportInformation transportInfo, CancellationToken cancel)
        {
            _transportInfo = transportInfo;

            _environment = new Dictionary<string, object>();
            _owinRequest = new OwinRequest(_environment);
            _owinResponse = new OwinResponse(_environment);

            _owinRequest.CallCancelled = cancel;
            _owinRequest.OwinVersion = "1.0";

            _owinRequest.RemoteIpAddress = _transportInfo.RemoteIpAddress;
            _owinRequest.RemotePort = _transportInfo.RemotePort;
            _owinRequest.LocalIpAddress = _transportInfo.LocalIpAddress;
            _owinRequest.LocalPort = _transportInfo.LocalPort;
            _owinRequest.IsLocal = string.Equals(_transportInfo.RemoteIpAddress, _transportInfo.LocalPort);
        }

        // For use with HTTP/1.1 upgrade handshakes
        public Http2Stream(int id, TransportInformation transportInfo, IDictionary<string, object> upgradeEnvironment,
            CancellationToken cancel)
            : this(transportInfo, cancel)
        {
            Contract.Assert(id == 1, "This constructor is only used for the initial HTTP/1.1 handshake request.");
            _id = id;

            OwinRequest upgradeRequest = new OwinRequest(upgradeEnvironment);

            // Initial upgrade requests can't have a body, correct?
            _owinRequest.Body = Stream.Null;

            _owinRequest.Headers = upgradeRequest.Headers;
            _owinRequest.Method = upgradeRequest.Method;
            _owinRequest.Path = upgradeRequest.Path;
            _owinRequest.PathBase = upgradeRequest.PathBase;
            _owinRequest.Protocol = upgradeRequest.Protocol;
            _owinRequest.QueryString = upgradeRequest.QueryString;
            _owinRequest.Scheme = upgradeRequest.Scheme;

            if (_transportInfo.ClientCertificate != null)
            {
                _owinRequest.Set(OwinConstants.CommonKeys.ClientCertificate, _transportInfo.ClientCertificate);
            }

            _owinResponse.Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            // TODO: Response body
        }

        // For use with incoming HTTP2 binary frames
        public Http2Stream(SynFrame synFrame, TransportInformation transportInfo, CancellationToken cancel)
            : this(transportInfo, cancel)
        {
            _id = synFrame.StreamId;
            // TODO: Request body
            // TODO: Response body

            // TODO: Decode headers (on another thread? From a start method?)
            // TODO: Populate env
        }

        public int Id { get { return _id; } }

        public IDictionary<string, object> Environment { get { return _environment; } }

        // Additional data has arrived for the request stream.  Add it to our request stream buffer, 
        // update any necessary state (e.g. FINs), and trigger any waiting readers.
        internal void ReceiveRequestData(DataFrame dataFrame)
        {
            throw new NotImplementedException();
        }

        // We've been offloaded onto a new thread. Decode the headers, invoke next, and do cleanup processing afterwards
        internal async Task Start(AppFunc next)
        {
            try
            {
                // TODO: Decompress headers

                await next(Environment);

                // OnStart
                // End
            }
            catch (Exception)
            {
                // TODO: Cleanup
                // 500 response?
                throw;
            }
        }
    }
}
