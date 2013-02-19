using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharedProtocol.Framing;

namespace ClientProtocol
{
    public static class HanshakeManager
    {
        private const int HandshakeResponseSizeLimit = 1024;
        private static readonly byte[] CRLFCRLF = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

        // Send a HTTP/1.1 upgrade request, expect a 101 response.
        // TODO: Failing a 101 response, we could fall back to HTTP/1.1, but
        // that is currently out of scope for this project.
        public static async Task<HandshakeResponse> DoHandshakeAsync(Stream stream, Uri uri, string method, string version,
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
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancel);

            // Read response headers
            return await Read11ResponseHeadersAsync(stream);
        }

        private static async Task<HandshakeResponse> Read11ResponseHeadersAsync(Stream stream)
        {
            byte[] buffer = new byte[HandshakeResponseSizeLimit];
            int lastInspectionOffset = 0;
            int readOffset = 0;
            int read = -1;
            do
            {
                read = await stream.ReadAsync(buffer, readOffset, buffer.Length - readOffset);
                if (read == 0)
                {
                    // TODO: Should this be a HandshakeResult? It's similar to a SockeException, IOException, etc..
                    throw new NotImplementedException("Early end of handshake stream.");
                }
                
                readOffset += read;
                int matchIndex;
                if (TryFindRangeMatch(buffer, lastInspectionOffset, readOffset, CRLFCRLF, out matchIndex))
                {
                    return InspectHanshake(buffer, matchIndex + CRLFCRLF.Length, readOffset);
                }

                lastInspectionOffset = Math.Max(0, readOffset - CRLFCRLF.Length);
                
                if (FrameHelpers.GetHighBitAt(buffer, 0))
                {
                    return new HandshakeResponse()
                    {
                        Result = HandshakeResult.UnexpectedControlFrame,
                        ExtraData = new ArraySegment<byte>(buffer, 0, readOffset),
                    };
                }
               
            } while (readOffset < HandshakeResponseSizeLimit);

            throw new NotImplementedException("Handshake response size limit exceeded");
        }

        private static bool TryFindRangeMatch(byte[] buffer, int offset, int limit, byte[] matchSequence, out int matchIndex)
        {
            matchIndex = 0;
            for (int master = offset; master < limit && master + matchSequence.Length <= limit; master++)
            {
                if (TryRangeMatch(buffer, master, limit, matchSequence))
                {
                    matchIndex = master;
                    return true;
                }
            }
            return false;
        }

        private static bool TryRangeMatch(byte[] buffer, int offset, int limit, byte[] matchSequence)
        {
            bool matched = (limit - offset) >= matchSequence.Length;
            for (int sequence = 0; sequence < matchSequence.Length && matched; sequence++)
            {
                matched = (buffer[offset + sequence] == matchSequence[sequence]);
            }
            if (matched)
            {
                return true;
            }
            return false;
        }

        // We've found a CRLFCRLF sequence.  Confirm the status code is 101 for upgrade.
        private static HandshakeResponse InspectHanshake(byte[] buffer, int split, int limit)
        {
            HandshakeResponse handshake = new HandshakeResponse()
            {
                ResponseBytes = new ArraySegment<byte>(buffer, 0, split),
                ExtraData = new ArraySegment<byte>(buffer, split, limit),
            };
            // Must be at least "HTTP/1.1 101\r\nConnection: Upgrade\r\nUpgrade: HTTP/2.0\r\n\r\n"
            string response = FrameHelpers.GetAsciiAt(buffer, 0, split).ToUpperInvariant();
            if (response.StartsWith("HTTP/1.1 101")
                && response.Contains("\r\nCONNECTION: UPGRADE\r\n")
                && response.Contains("\r\nUPGRADE: HTTP/2.0\r\n"))
            {
                handshake.Result = HandshakeResult.Upgrade;
            }
            else
            {
                handshake.Result = HandshakeResult.NonUpgrade;
            }
            return handshake;
        }
    }
}
