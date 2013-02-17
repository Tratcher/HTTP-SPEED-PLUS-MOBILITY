using SharedProtocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientProtocol
{
    public class Http2ClientSession : Http2BaseSession
    {
        private Stream _sessionStream;

        public Http2ClientSession(Stream sessionStream)
        {
            _sessionStream = sessionStream;
        }

        // Send a HTTP/1.1 upgrade request, expect a 101 response.
        // TODO: Failing a 101 response, we could fall back to HTTP/1.1, but
        // that is currently out of scope for this project.
        public async Task DoHandshakeAsync(Uri uri, string method, string version, 
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, CancellationToken cancel)
        {
            // TODO: Verify session state, handshake needed.
            
            // Build the request
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("{0} {1} {2}\r\n", method, uri.PathAndQuery, version);
            builder.AppendFormat("Host: {0}\r\n", uri.GetComponents(UriComponents.Host | UriComponents.StrongPort, UriFormat.UriEscaped));
            builder.Append("Connection: Upgrade\r\n");
            builder.Append("Upgrade: HTTP/2.0\r\n");
            foreach (KeyValuePair<string, IEnumerable<string>> headerPair in headers)
            {
                foreach (string value in headerPair.Value)
                {
                    builder.AppendFormat("{0}: {1}\r\n", headerPair.Key, value);
                }
            }
            builder.Append("\r\n");

            byte[] requestBytes = Encoding.ASCII.GetBytes(builder.ToString());
            await _sessionStream.WriteAsync(requestBytes, 0, requestBytes.Length, cancel);

            // Read response headers
            string responseHeaders = await Read11ResponseHeadersAsync();
        }

        private Task<string> Read11ResponseHeadersAsync()
        {
            throw new NotImplementedException();
        }

        public Http2ClientStream GetStream(int id)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _sessionStream.Dispose();
        }

        public void StartPumps()
        {
            // TODO: Start pumping incoming and outgoing frames on separate threads
        }
    }
}
