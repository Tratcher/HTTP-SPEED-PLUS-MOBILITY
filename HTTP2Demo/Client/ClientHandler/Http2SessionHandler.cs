using ClientHandler.Transport;
using ClientProtocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Http2Protocol;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientHandler
{
    // Manages HTTP/2.0 using the System.Net.Http object model.
    // Note: This could also fall back to HTTP/1.1, but that's out of scope for this project.
    public class Http2SessionHandler : HttpMessageHandler
    {
        private SemaphoreSlim _connectingLock = new SemaphoreSlim(1, 1000);
        private IConnectionResolver _connectionResolver;
        private ISecureConnectionResolver _secureConnectionResolver;
        private Http2ClientSession _clientSession;
        private bool _do11Handshake;

        public Http2SessionHandler(bool do11Handshake)
            : this(do11Handshake, new SocketConnectionResolver())
        {
        }

        public Http2SessionHandler(bool do11Handshake, IConnectionResolver connectionResolver)
        {
            // TODO: Will we need a connection resolver that understands proxies?
            _connectionResolver = connectionResolver;
            _secureConnectionResolver = new SslConnectionResolver(_connectionResolver);
            _do11Handshake = do11Handshake;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Http2ClientStream stream;
            int? streamId;
            // Open the session if it hasn't been.
            if (await ConnectIfNeeded(request, cancellationToken))
            {
                // This request was submitted during the handshake as StreamId 1
                stream = _clientSession.GetStream(1);
            }
            // Verify the requested resource has not been pushed by the server
            else if ((streamId = CheckForPendingResource(request)).HasValue)
            {
                stream = _clientSession.GetStream(streamId.Value);
            }
            // Submit the request and start uploading any data. (What about 100-Continues?)
            else
            {
                stream = SubmitRequest(request, cancellationToken);
            }
            
            // Build the response
            return await GetResponseAsync(stream);
        }

        // Returns true if this request was used to perform the initial handshake and does not need to be submitted separately.
        private async Task<bool> ConnectIfNeeded(HttpRequestMessage request, CancellationToken cancel)
        {
            Stream sessionStream = null;
            // Async lock, only one at a time.
            await _connectingLock.WaitAsync();
            try
            {
                if (_clientSession == null)
                {
                    Uri requestUri = request.RequestUri;
                    object tempObject;
                    if (requestUri.Scheme.Equals(Uri.UriSchemeHttps)
                        && request.Properties.TryGetValue("ssl.ClientCertificate", out tempObject)
                        && tempObject is X509Certificate)
                    {
                        sessionStream = await _secureConnectionResolver.ConnectAsync(requestUri.DnsSafeHost, requestUri.Port, (X509Certificate)tempObject, cancel);
                    }
                    else
                    {
                        sessionStream = await _connectionResolver.ConnectAsync(requestUri.DnsSafeHost, requestUri.Port, cancel);
                    }

                    _clientSession = new Http2ClientSession(sessionStream);

                    if (_do11Handshake)
                    {
                        await DoHandshakeAsync(request, cancel);
                    }
                    _clientSession.StartPumps();
                    _connectingLock.Release(99); // Unblock all, this method no longer needs to be one at a time.
                    return _do11Handshake;
                }
                return false;
            }
            catch (Exception)
            {
                if (sessionStream != null)
                {
                    sessionStream.Dispose();
                }
                if (_clientSession != null)
                {
                    _clientSession.Dispose();
                    _clientSession = null;
                }
                throw;
            }
            finally
            {
                _connectingLock.Release();
            }
        }

        // The session was just created, we're under a lock, do the initial handshake.
        private async Task DoHandshakeAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            await _clientSession.DoHandshakeAsync(request.RequestUri, request.Method.ToString(), 
                "HTTP/" + request.Version.ToString(2), request.Headers, cancel);
            throw new NotImplementedException();
        }

        // Verify the requested resource has not been pushed by the server
        private int? CheckForPendingResource(HttpRequestMessage request)
        {
            return null;
            // Keyed on Uri, Method, Version, Cert
            // throw new NotImplementedException();
        }

        private Http2ClientStream SubmitRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Maintain the list of client certificates in sync with the server
            // Send a cert update if the server doesn't have this specific client cert yet.
            
            // Serialize the request as a SynStreamFrame and submit it. (FIN if there is no body)

            throw new NotImplementedException();
        }

        private Task<HttpResponseMessage> GetResponseAsync(Http2ClientStream stream)
        {
            // TODO: 100 continue?
            // Start data upload.
            
            // Wait for and desterilize the response SynReplyFrame
            // Set up a response content object to receive the reply data.
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (_clientSession != null)
            {
                _clientSession.Dispose();
            }
            _connectingLock.Dispose();
            base.Dispose(disposing);
        }
    }
}
