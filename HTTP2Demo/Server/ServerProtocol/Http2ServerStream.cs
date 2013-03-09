using Owin.Types;
using SharedProtocol;
using SharedProtocol.Framing;
using SharedProtocol.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ServerProtocol
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class Http2ServerStream : Http2BaseStream
    {
        private TransportInformation _transportInfo;
        private IDictionary<string, object> _environment;
        private OwinRequest _owinRequest;
        private OwinResponse _owinResponse;
        private object _responseStarted;
        private int _certSlot;

        private SynStreamFrame _synFrame;
        private IDictionary<string, object> _upgradeEnvironment;
        
        private Http2ServerStream(int id, TransportInformation transportInfo, WriteQueue writeQueue, CancellationToken cancel)
            : base(id, writeQueue, cancel)
        {
            _transportInfo = transportInfo;
        }

        // For use with HTTP/1.1 upgrade handshakes
        public Http2ServerStream(int id, TransportInformation transportInfo, IDictionary<string, object> upgradeEnvironment,
            WriteQueue writeQueue, CancellationToken cancel)
            : this(id, transportInfo, writeQueue, cancel)
        {
            Contract.Assert(id == 1, "This constructor is only used for the initial HTTP/1.1 handshake request.");
            _upgradeEnvironment = upgradeEnvironment;

            // Environment will be populated on another thread in Run
        }

        // For use with incoming HTTP2 binary frames
        public Http2ServerStream(SynStreamFrame synFrame, TransportInformation transportInfo, WriteQueue writeQueue, CancellationToken cancel)
            : this(synFrame.StreamId, transportInfo, writeQueue, cancel)
        {
            _synFrame = synFrame;
            if (synFrame.IsFin)
            {
                // TODO: Set stream state to request body complete and RST the stream if additional frames arrive.
                _incomingStream = Stream.Null;
            }
            else
            {
                _incomingStream = new InputStream(Constants.DefaultFlowControlCredit, SendWindowUpdate);
            }
        }

        public IDictionary<string, object> Environment { get { return _environment; } }

        // We've been offloaded onto a new thread. Decode the headers, invoke next, and do cleanup processing afterwards
        public async Task Run(AppFunc next)
        {
            try
            {
                PopulateEnvironment();

                await next(Environment);

                EndResponse();
            }
            catch (Exception ex)
            {
                EndResponse(ex);
            }
        }

        private void PopulateEnvironment()
        {
            _environment = new Dictionary<string, object>();
            _owinRequest = new OwinRequest(_environment);
            _owinResponse = new OwinResponse(_environment);

            _owinRequest.CallCancelled = _cancel;
            _owinRequest.OwinVersion = Constants.OwinVersion;

            _owinRequest.RemoteIpAddress = _transportInfo.RemoteIpAddress;
            _owinRequest.RemotePort = _transportInfo.RemotePort;
            _owinRequest.LocalIpAddress = _transportInfo.LocalIpAddress;
            _owinRequest.LocalPort = _transportInfo.LocalPort;
            _owinRequest.IsLocal = string.Equals(_transportInfo.RemoteIpAddress, _transportInfo.LocalPort);

            _owinResponse.Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            if (_upgradeEnvironment != null)
            {
                OwinRequest upgradeRequest = new OwinRequest(_upgradeEnvironment);

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
                    // TODO: Lazy cert key?
                    _certSlot = 1;
                }

                _version = Constants.CurrentProtocolVersion; // TODO: Undefined?
                _priority = Priority.Pri3; // Neutral // TODO: Undefined?

                _upgradeEnvironment = null;
            }
            else // SynFrame
            {
                DeserializeRequestHeaders();
                _version = _synFrame.Version;
                _priority = _synFrame.Priority;
                _certSlot = _synFrame.CertClot;

                if (_certSlot > 0)
                {
                    // TODO: Cert key
                    // TODO: Lazy cert key
                }

                _owinRequest.Body = _incomingStream;
                _synFrame = null;
            }

            _owinRequest.Set("http2.Priority", (int)_priority);

            _outputStream = new OutputStream(_id, _priority, _writeQueue, StartResponse);
            _owinResponse.Body = _outputStream;
        }

        // Includes method, path&query, version, host, scheme.
        private void DeserializeRequestHeaders()
        {
            Contract.Assert(_synFrame != null);

            _owinRequest.Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            ArraySegment<byte> compressedHeaders = _synFrame.CompressedHeaders;
            byte[] rawHeaders = _compressor.Decompress(compressedHeaders);
            IList<KeyValuePair<string, string>> pairs = FrameHelpers.DeserializeHeaderBlock(rawHeaders);
   
            foreach (KeyValuePair<string, string> pair in pairs)
            {
                if (pair.Key[0] == ':')
                {
                    MapRequestProperty(pair.Key, pair.Value);
                }
                else
                {
                    // Null separated list of values
                    _owinRequest.Headers[pair.Key] = pair.Value.Split('\0');
                }
            }

            VerifyRequiredRequestsPropertiesSet();
        }

        // HTTP/2.0 sends HTTP/1.1 request properties like path, query, etc, as headers prefixed with ':'
        private void MapRequestProperty(string key, string value)
        {
            // keys are required to be lower case
            if (":scheme".Equals(key, StringComparison.Ordinal))
            {
                _owinRequest.Scheme = value;
            }
            else if (":host".Equals(key, StringComparison.Ordinal))
            {
                _owinRequest.Host = value;
            }
            else if (":path".Equals(key, StringComparison.Ordinal))
            {
                // Split off query
                int queryIndex = value.IndexOf('?');
                string query = string.Empty;
                if (queryIndex >= 0)
                {
                    _owinRequest.QueryString = value.Substring(queryIndex + 1); // No leading '?'
                    value = value.Substring(0, queryIndex);
                }
                _owinRequest.Path = Uri.UnescapeDataString(value); // TODO: Is this the correct escaping?
                _owinRequest.PathBase = string.Empty;
            }
            else if (":method".Equals(key, StringComparison.Ordinal))
            {
                _owinRequest.Method = value;
            }
            else if (":version".Equals(key, StringComparison.Ordinal))
            {
                _owinRequest.Protocol = value;
            }
        }

        // Verify at least the minimum request properties were set:
        // scheme, host&port, path&query, method, version
        private void VerifyRequiredRequestsPropertiesSet()
        {
            // Set bitflags in MapRequestProperty?
            // TODO:
            // throw new NotImplementedException();
        }

        private void StartResponse()
        {
            StartResponse(null, headersOnly: false);
        }

        // First write, or stack unwind without writes
        private bool StartResponse(Exception ex, bool headersOnly)
        {
            if (Interlocked.CompareExchange(ref _responseStarted, new object(), null) != null)
            {
                // Already started
                return false;
            }

            if (ex != null)
            {
                Contract.Assert(headersOnly);
                _owinResponse.StatusCode = StatusCode.Code500InternalServerError;
                _owinResponse.ReasonPhrase = StatusCode.Reason500InternalServerError;
                _owinResponse.Headers.Clear();
                // TODO: trigger the CancellationToken?
            }
            else
            {
                // TODO: Fire OnSendingHeaders event
            }

            byte[] headerBytes = SerializeResponseHeaders();
            headerBytes = _compressor.Compress(headerBytes);

            // Prepare a SynReply frame and queue it
            SynReplyFrame synFrame = new SynReplyFrame(_id, headerBytes);
            if (headersOnly)
            {
                synFrame.IsFin = true;
            }

            // SynReplyFrames go in the control queue so they get sequenced properly with GoAway frames.
            _writeQueue.WriteFrameAsync(synFrame, Priority.Control, CancellationToken.None);

            return true;
        }

        private byte[] SerializeResponseHeaders()
        {
            IList<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();

            int statusCode = Get<int>("owin.ResponseStatusCode", 200);
            string statusCodeString = statusCode.ToString(CultureInfo.InvariantCulture);
            string reasonPhrase = Get<string>("owin.ResponseReasonPhrase", StatusCode.GetReasonPhrase(statusCode));
            string statusHeader = statusCodeString + (reasonPhrase != null ? " " + reasonPhrase : string.Empty);
            pairs.Add(new KeyValuePair<string, string>(":status", statusHeader));

            string version = Get<string>("owin.ResponseProtocol", "HTTP/1.1");
            pairs.Add(new KeyValuePair<string, string>(":version", version));

            IDictionary<string, string[]> responseHeaders = Get<IDictionary<string, string[]>>("owin.ResponseHeaders");
            foreach (KeyValuePair<string, string[]> pair in responseHeaders)
            {
                pairs.Add(new KeyValuePair<string, string>(pair.Key.ToLowerInvariant(),
                    string.Join("\0", pair.Value)));
            }

            return FrameHelpers.SerializeHeaderBlock(pairs);
        }

        private void EndResponse()
        {
            EndResponse(null);
        }

        private void EndResponse(Exception ex)
        {
            if (StartResponse(ex, headersOnly: true))
            {
                // Hadn't been started yet, FIN sent with headers.
                // Error code sent with headers, if any.
            }
            else if (ex != null)
            {
                // TODO: trigger the CancellationToken?
                _writeQueue.PurgeStream(Id);
                RstStreamFrame reset = new RstStreamFrame(Id, ResetStatusCode.InternalError);
                _writeQueue.WriteFrameAsync(reset, Priority.Control, CancellationToken.None);
            }
            else
            {
                // TODO: Send trailer headers (with fin)?

                DataFrame terminator = new DataFrame(_id);
                _writeQueue.WriteFrameAsync(terminator, _priority, CancellationToken.None);
            }
            Dispose();
        }

        public override void Reset(ResetStatusCode statusCode)
        {
            // TODO: Trigger the cancellation token in a Task.Run so we don't block the message pump.
            base.Reset(statusCode);
        }

        private T Get<T>(string key, T fallback = default(T))
        {
            object obj;
            if (Environment.TryGetValue(key, out obj)
                   && obj is T)
            {
                return (T)obj;
            }
            return fallback;
        }
    }
}
