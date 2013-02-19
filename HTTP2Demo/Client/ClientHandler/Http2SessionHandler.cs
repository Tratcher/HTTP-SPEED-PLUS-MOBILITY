﻿using ClientHandler.Transport;
using ClientProtocol;
using SharedProtocol.Framing;
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
                // TODO: This code path will actually be quite different from the normal flow.
                // The response has already arrived (as a SynStreamFrame), we can't submit a body,
                // and we have to decompress the headers to even execute CheckForPendeingResource.
                throw new NotImplementedException();
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
                    sessionStream = await ConnectAsync(request, cancel);

                    bool didHandshake = _do11Handshake;
                    if (_do11Handshake)
                    {
                        HandshakeResponse handshake = await DoHandshakeAsync(sessionStream, request, cancel);
                        if (handshake.Result == HandshakeResult.NonUpgrade)
                        {
                            throw new NotSupportedException("HTTP/1.1 handshake fallback not implemented: \r\n" 
                                + FrameHelpers.GetAsciiAt(handshake.ResponseBytes));
                        }
                        else if (handshake.Result == HandshakeResult.UnexpectedControlFrame)
                        {
                            // The server only accepts direct 2.0 upgrade, try again without the 1.1 handshake.
                            didHandshake = false;
                            sessionStream.Dispose();
                            sessionStream = await ConnectAsync(request, cancel);
                        }
                        else if (handshake.Result != HandshakeResult.Upgrade)
                        {
                            throw new NotImplementedException(handshake.Result.ToString());
                        }
                    }

                    _clientSession = new Http2ClientSession(sessionStream, 
                        createHanshakeStream: didHandshake, 
                        handshakeCancel: didHandshake ? cancel : CancellationToken.None);

                    // TODO: Listen to task for errors?
                    Task pumpTasks = _clientSession.StartPumps();

                    _connectingLock.Release(999); // Unblock all, this method no longer needs to be one at a time.
                    return didHandshake;
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

        private Task<Stream> ConnectAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            Uri requestUri = request.RequestUri;
            object tempObject;
            if (requestUri.Scheme.Equals(Uri.UriSchemeHttps))
            {
                request.Properties.TryGetValue("ssl.ClientCertificate", out tempObject);
                return _secureConnectionResolver.ConnectAsync(requestUri.DnsSafeHost, requestUri.Port, tempObject as X509Certificate, cancel);
            }

            return _connectionResolver.ConnectAsync(requestUri.DnsSafeHost, requestUri.Port, cancel);
        }

        // The session was just created, we're under a lock, do the initial handshake.
        private Task<HandshakeResponse> DoHandshakeAsync(Stream sessionStream, HttpRequestMessage request, CancellationToken cancel)
        {
            return HanshakeManager.DoHandshakeAsync(sessionStream, request.RequestUri, request.Method.ToString(), 
                "HTTP/" + request.Version.ToString(2), request.Headers, cancel);
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
            Http2ClientStream clientStream = _clientSession.CreateStream(cancellationToken);
            
            int certIndex = UpdateClientCertificates(request);

            IList<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();

            string method = request.Method.ToString();
            string path = request.RequestUri.PathAndQuery;
            string version = "HTTP/" + request.Version.ToString(2);
            string scheme = request.RequestUri.Scheme;
            string host = request.Headers.Host
                ?? request.RequestUri.GetComponents(UriComponents.Host | UriComponents.StrongPort, UriFormat.UriEscaped);
            request.Headers.Host = null;

            pairs.Add(new KeyValuePair<string, string>(":method", method));
            pairs.Add(new KeyValuePair<string, string>(":path", path));
            pairs.Add(new KeyValuePair<string, string>(":version", version));
            pairs.Add(new KeyValuePair<string, string>(":host", host));
            pairs.Add(new KeyValuePair<string, string>(":scheme", scheme));

            foreach (KeyValuePair<string, IEnumerable<string>> pair in request.Headers)
            {
                pairs.Add(new KeyValuePair<string, string>(pair.Key.ToLowerInvariant(),
                    string.Join("\0", pair.Value)));
            }
            if (request.Content != null)
            {
                // Compute lazy content-length via TryComputeLength
                request.Content.Headers.ContentLength = request.Content.Headers.ContentLength;

                // TODO: De-dupe custom headers between request and content.
                foreach (KeyValuePair<string, IEnumerable<string>> pair in request.Content.Headers)
                {
                    pairs.Add(new KeyValuePair<string, string>(pair.Key.ToLowerInvariant(),
                        string.Join("\0", pair.Value)));
                }
            }

            // Serialize the request as a SynStreamFrame and submit it. (FIN if there is no body)
            byte[] headerBytes = FrameHelpers.SerializeHeaderBlock(pairs);
            headerBytes = clientStream.Compressor.Compress(headerBytes);
            SynStreamFrame frame = new SynStreamFrame(clientStream.Id, headerBytes);
            frame.CertClot = certIndex;

            // TODO: Set priority from request.Properties

            // TODO: Uploads?
            frame.Flags = FrameFlags.Fin;

            clientStream.StartRequest(frame);
            return clientStream;
        }

        // Maintain the list of client certificates in sync with the server
        // Send a cert update if the server doesn't have this specific client cert yet.
        private int UpdateClientCertificates(HttpRequestMessage request)
        {
            return 0;
            // throw new NotImplementedException();
        }

        private async Task<HttpResponseMessage> GetResponseAsync(Http2ClientStream stream)
        {
            // TODO: 100 continue? Start data upload (on separate thread so we can do bidirectional).

            // Wait for and desterilize the response SynReplyFrame
            // Set up a response content object to receive the reply data.
            SynReplyFrame responseFrame = await stream.GetResponseAsync();
            StreamContent streamContent = new StreamContent(stream.ResponseStream);
            HttpResponseMessage response = new HttpResponseMessage();
            response.Content = streamContent;

            // TODO: Decompress and distribute headers
            // TODO: Associate with the original HttpRequestMessage
            // TODO: How do we receive trailer headers?
            // TODO: How do we receive (or discard) pushed resources?
                        
            throw new NotImplementedException();

            return response;
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