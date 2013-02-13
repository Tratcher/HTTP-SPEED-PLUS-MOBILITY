using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Owin.Types;
using System.Security.Cryptography.X509Certificates;
using ServerProtocol;

namespace ServerOwinMiddleware
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    // Http-01/2.0 uses a similar upgrade handshake to WebSockets. This middleware answers upgrade requests
    // using the Opaque Upgrade OWIN extension and then switches the pipeline to HTTP/2.0 binary framing.
    // Interestingly the HTTP/2.0 handshake does not need to be the first HTTP/1.1 request on a connection, only the last.
    public class Http2Middleware
    {
        private AppFunc _next;

        public Http2Middleware(AppFunc next)
        {
            _next = next;
        }

        public async Task Invoke(IDictionary<string, object> environment)
        {
            OwinRequest owinRequest = new OwinRequest(environment);
            OwinResponse owinResponse = new OwinResponse(environment);
            // Inspect the request to see if it is a HTTP/2.0 upgrade request
            if (!IsRequestForHttp2Upgrade(owinRequest) || !IsOpaqueUpgradePossible(owinRequest))
            {
                await _next(environment);
                return;
            }

            SetHttp2UpgradeHeadersAndStatusCode(owinResponse);
            Http2Session session = await CreateUpgradeSessionAsync(owinRequest);

            owinRequest.Upgrade(opaque =>
            {
                // Assign the opaque stream to the session and initiate session operation.
                return session.Start(opaque.Stream, opaque.CallCancelled);
            });

            // Officially we need to unwind the original request before continuing the opaque upgrade.
            // (In reality our callback will be invoked immediately.)
        }

        private bool IsRequestForHttp2Upgrade(OwinRequest request)
        {
            // TODO: Supported methods?  None called out in the 01 spec, but the sample uses GET.
            // POST would be problematic as you'd also have to consume all of the request data before completing the upgrade.

            // Headers
            // Connection: Upgrade
            // Upgrade: HTTP/2.0
            return string.Equals(request.GetHeader("Connection"), "Upgrade", StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.GetHeader("Upgrade"), "HTTP/2.0", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsOpaqueUpgradePossible(OwinRequest request)
        {
            return request.UpgradeDelegate != null;
        }

        private void SetHttp2UpgradeHeadersAndStatusCode(OwinResponse owinResponse)
        {
            owinResponse.StatusCode = 101;
            owinResponse.ReasonPhrase = "Switching Protocols";
            owinResponse.SetHeader("Connection", "Upgrade");
            owinResponse.SetHeader("Upgrade", "HTTP/2.0");
        }

        private async Task<Http2Session> CreateUpgradeSessionAsync(OwinRequest owinRequest)
        {
            // Create a new Http2Session object with the following:
            // A reference to AppFunc _next
            // Client cert (if any)
            // Stream with ID 1 containing request headers & properties copied from the current environment, queued for dispatch to _next AppFunc once upgrade is complete.
            // Misc extra keys? local & remote IPAddress & ports, IsLocal, capabilities, (not send-file? might still work with careful flushing)
            
            X509Certificate clientCert = null;
            if (string.Equals(owinRequest.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                Func<Task> loadCertAsync = owinRequest.Get<Func<Task>>("ssl.LoadClientCert");
                if (loadCertAsync != null)
                {
                    await loadCertAsync();
                }
                clientCert = owinRequest.Get<X509Certificate>("ssl.ClientCertificate");
            }

            // The opaque stream and CancellationToken will be provided after the opaque upgrade.
            return new Http2Session(_next, CreateTransportInfo(clientCert, owinRequest), CopyHandshakeRequest(owinRequest));
        }

        private TransportInformation CreateTransportInfo(X509Certificate clientCert, OwinRequest owinRequest)
        {
            return new TransportInformation()
            {
                ClientCertificate = clientCert,
                RemoteIpAddress = owinRequest.RemoteIpAddress,
                RemotePort = owinRequest.RemotePort,
                LocalIpAddress = owinRequest.LocalIpAddress,
                LocalPort = owinRequest.LocalPort,
            };
        }

        // Make a copy of the handshake request. The original environment will be out of scope
        // after the opaque upgrade.
        private IDictionary<string, object> CopyHandshakeRequest(OwinRequest owinRequest)
        {
            Dictionary<string, object> newRequest = new Dictionary<string, object>();

            string[] knownKeysToCopy = new[]
            {
                "owin.RequestMethod",
                "owin.RequestScheme",
                "owin.RequestPath",
                "owin.RequestPathBase",
                "owin.RequestQueryString",
                "owin.RequestProtocol",

                // "server.Capabilities", This middleware will become the new server, so the original server capabilities aren't relevant.
            };

            // Shallow copy data fields
            for (int i = 0; i < knownKeysToCopy.Length; i++)
            {
                object value;
                if (owinRequest.Dictionary.TryGetValue(knownKeysToCopy[i], out value))
                {
                    newRequest.Add(knownKeysToCopy[i], value);
                }
            }

            // TODO: Are request bodies allowed on an upgrade request?
            newRequest.Add("owin.RequestBody", Stream.Null);

            // Deep copy request headers
            IDictionary<string, string[]> oldRequestHeaders = owinRequest.Headers;
            Dictionary<string, string[]> newRequestHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string[]> headerPair in oldRequestHeaders)
            {
                newRequestHeaders.Add(headerPair.Key, headerPair.Value);
            }
            newRequest.Add("owin.RequestHeaders", newRequestHeaders);

            return newRequest;
        }
    }
}
