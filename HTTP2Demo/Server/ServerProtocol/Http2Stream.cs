using Owin.Types;
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
    class Http2Stream
    {
        private int _id;
        private TransportInformation _transportInfo;
        private IDictionary<string, object> _environment;
        private OwinRequest _owinRequest;
        private OwinResponse _owinResponse;

        public Http2Stream(int id, TransportInformation transportInfo, IDictionary<string, object> upgradeEnvironment,
            CancellationToken cancel)
        {
            Contract.Assert(id == 1, "This constructor is only used for the initial HTTP/1.1 handshake request.");
            _id = id;
            _transportInfo = transportInfo;

            _environment = new Dictionary<string, object>();
            _owinRequest = new OwinRequest(_environment);
            _owinResponse = new OwinResponse(_environment);

            OwinRequest upgradeRequest = new OwinRequest(upgradeEnvironment);

            _owinRequest.CallCancelled = cancel;
            _owinRequest.OwinVersion = upgradeRequest.OwinVersion;

            // Initial upgrade requests can't have a body, correct?
            _owinRequest.Body = Stream.Null;

            _owinRequest.Headers = upgradeRequest.Headers;
            _owinRequest.Method = upgradeRequest.Method;
            _owinRequest.Path = upgradeRequest.Path;
            _owinRequest.PathBase = upgradeRequest.PathBase;
            _owinRequest.Protocol = upgradeRequest.Protocol;
            _owinRequest.QueryString = upgradeRequest.QueryString;
            _owinRequest.Scheme = upgradeRequest.Scheme;

            _owinRequest.RemoteIpAddress = _transportInfo.RemoteIpAddress;
            _owinRequest.RemotePort = _transportInfo.RemotePort;
            _owinRequest.LocalIpAddress = _transportInfo.LocalIpAddress;
            _owinRequest.LocalPort = _transportInfo.LocalPort;
            _owinRequest.IsLocal = string.Equals(_transportInfo.RemoteIpAddress, _transportInfo.LocalPort);

            if (_transportInfo.ClientCertificate != null)
            {
                _owinRequest.Set(OwinConstants.CommonKeys.ClientCertificate, _transportInfo.ClientCertificate);
            }

            _owinResponse.Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            // TODO: Response body
        }

        public int Id { get { return _id; } }

        public IDictionary<string, object> Environment { get { return _environment; } }
    }
}
