using Owin.Types;
using ServerProtocol.Compression;
using ServerProtocol.Framing;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ServerProtocol
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    internal class Http2Stream
    {
        private int _id;
        private TransportInformation _transportInfo;
        private IDictionary<string, object> _environment;
        private OwinRequest _owinRequest;
        private OwinResponse _owinResponse;
        private object _responseStarted;
        private WriteQueue _writeQueue;
        private CompressionProcessor _compressor;
        private CancellationToken _cancel;
        private int _version;
        private int _priority;
        private int _certSlot;

        private SynStreamFrame _synFrame;
        private IDictionary<string, object> _upgradeEnvironment;
        
        private Http2Stream(TransportInformation transportInfo, WriteQueue writeQueue, CancellationToken cancel)
        {
            _transportInfo = transportInfo;
            _writeQueue = writeQueue;
            _cancel = cancel;
            _compressor = new CompressionProcessor();
        }

        // For use with HTTP/1.1 upgrade handshakes
        public Http2Stream(int id, TransportInformation transportInfo, IDictionary<string, object> upgradeEnvironment,
            WriteQueue writeQueue, CancellationToken cancel)
            : this(transportInfo, writeQueue, cancel)
        {
            Contract.Assert(id == 1, "This constructor is only used for the initial HTTP/1.1 handshake request.");
            _id = id;
            _upgradeEnvironment = upgradeEnvironment;

            // Environment will be populated on another thread in Run
        }

        // For use with incoming HTTP2 binary frames
        public Http2Stream(SynStreamFrame synFrame, TransportInformation transportInfo, WriteQueue writeQueue, CancellationToken cancel)
            : this(transportInfo, writeQueue, cancel)
        {
            _id = synFrame.StreamId;
            _synFrame = synFrame;
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
        internal async Task Run(AppFunc next)
        {
            try
            {
                PopulateEnvironment();

                await next(Environment);

                EndResponse();
            }
            catch (Exception)
            {
                // TODO: Cleanup
                // 500 response?
                throw;
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
                _priority = 4; // Neutral // TODO: Undefined?

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

                // TODO: Request body

                _synFrame = null;
            }

            _owinResponse.Body = new ResponseStream(_id, _writeQueue, StartResponse);
        }

        // Includes method, path&query, version, host, scheme.
        // +------------------------------------+
        // | Number of Name/Value pairs (int32) |
        // +------------------------------------+
        // |     Length of name (int32)         |
        // +------------------------------------+
        // |           Name (string)            |
        // +------------------------------------+
        // |     Length of value  (int32)       |
        // +------------------------------------+
        // |          Value   (string)          |
        // +------------------------------------+
        // |           (repeats)                |
        private void DeserializeRequestHeaders()
        {
            Contract.Assert(_synFrame != null);

            _owinRequest.Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            ArraySegment<byte> compressedHeaders = _synFrame.CompressedHeaders;
            byte[] rawHeaders = _compressor.Decompress(compressedHeaders);

   
            int offset = 0;
            int headerCount = FrameHelpers.Get32BitsAt(rawHeaders, offset);
            offset += 4;
            for (int i = 0; i < headerCount; i++)
            {
                int keyLength = FrameHelpers.Get32BitsAt(rawHeaders, offset);
                Contract.Assert(keyLength > 0);
                offset += 4;
                string key = FrameHelpers.GetAsciiAt(rawHeaders, offset, keyLength);
                offset += keyLength;
                int valueLength = FrameHelpers.Get32BitsAt(rawHeaders, offset);
                offset += 4;
                string value = FrameHelpers.GetAsciiAt(rawHeaders, offset, valueLength);
                offset += valueLength;

                if (key[0] == ':')
                {
                    MapRequestProperty(key, value);
                }
                else
                {
                    // Null separated list of values
                    _owinRequest.Headers[key] = value.Split('\0');
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
            StartResponse(headersOnly: false);
        }

        // First write, or stack unwind without writes
        private bool StartResponse(bool headersOnly)
        {
            if (Interlocked.CompareExchange(ref _responseStarted, new object(), null) != null)
            {
                // Already started
                return false;
            }

            // TODO: Fire OnSendingHeaders event

            byte[] headerBytes = SerializeResponseHeaders();
            headerBytes = _compressor.Compress(headerBytes);

            // Prepare a SynReply frame and queue it
            SynReplyFrame synFrame = new SynReplyFrame(headerBytes, _id);
            if (headersOnly)
            {
                synFrame.Flags = FrameFlags.Fin;
            }

            _writeQueue.WriteFrameAsync(synFrame, CancellationToken.None);

            return true;
        }

        // Includes status code, reason phrase, and version.
        // +------------------------------------+
        // | Number of Name/Value pairs (int32) |
        // +------------------------------------+
        // |     Length of name (int32)         |
        // +------------------------------------+
        // |           Name (string)            |
        // +------------------------------------+
        // |     Length of value  (int32)       |
        // +------------------------------------+
        // |          Value   (string)          |
        // +------------------------------------+
        // |           (repeats)                |
        private byte[] SerializeResponseHeaders()
        {
            IList<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();

            string statusCode = Get<int>("owin.ResponseStatusCode", 200).ToString(CultureInfo.InvariantCulture);
            string reasonPhrase = Get<string>("owin.ResponseReasonPhrase", null);
            string statusHeader = statusCode + reasonPhrase != null ? " " + reasonPhrase : string.Empty;
            pairs.Add(new KeyValuePair<string, string>(":status", statusHeader));

            string version = Get<string>("owin.ResponseProtocol", "HTTP/1.1");
            pairs.Add(new KeyValuePair<string, string>(":version", version));

            IDictionary<string, string[]> responseHeaders = Get<IDictionary<string, string[]>>("owin.ResponseHeaders");
            foreach (KeyValuePair<string, string[]> pair in responseHeaders)
            {
                pairs.Add(new KeyValuePair<string, string>(pair.Key.ToLowerInvariant(),
                    string.Join("\0", pair.Value)));
            }

            int encodedLength = 4 // 32 bit count of name value pairs
                + 8 * pairs.Count; // A 32 bit size per header and value;
            for (int i = 0; i < pairs.Count; i++)
            {
                encodedLength += pairs[i].Key.Length + pairs[i].Value.Length;
            }

            byte[] buffer = new byte[encodedLength];
            FrameHelpers.Set32BitsAt(buffer, 0, pairs.Count);
            int offset = 4;
            for (int i = 0; i < pairs.Count; i++)
            {
                KeyValuePair<string, string> pair = pairs[i];
                FrameHelpers.Set32BitsAt(buffer, offset, pair.Key.Length);
                offset += 4;
                FrameHelpers.SetAsciiAt(buffer, offset, pair.Key);
                offset += pair.Key.Length;
                FrameHelpers.Set32BitsAt(buffer, offset, pair.Value.Length);
                offset += 4;
                FrameHelpers.SetAsciiAt(buffer, offset, pair.Value);
                offset += pair.Value.Length;
            }
            return buffer;
        }

        private void EndResponse()
        {
            if (StartResponse(headersOnly: true))
            {
                // Hadn't been started yet, FIN sent with headers.
                return;
            }

            DataFrame terminator = new DataFrame(_id);
            _writeQueue.WriteFrameAsync(terminator, CancellationToken.None);
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
