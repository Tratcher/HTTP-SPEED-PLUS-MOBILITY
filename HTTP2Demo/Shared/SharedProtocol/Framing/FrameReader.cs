using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharedProtocol.Framing
{
    public class FrameReader
    {
        private Stream _stream;
        private CancellationToken _cancel;

        public FrameReader(Stream stream, CancellationToken cancel)
        {
            _stream = stream;
            _cancel = cancel;
        }

        public async Task<Frame> ReadFrameAsync()
        {
            Frame preamble = new Frame();
            if (!await TryFillAsync(preamble.Buffer, 0, preamble.Buffer.Length, _cancel))
            {
                return null;
            }

            // TODO: If this is the first frame, verify that it is in fact a control frame, and that it is not a HTTP/1.1 text request.
            // Not applicable after an HTTP/1.1->HTTP-01/2.0 upgrade handshake.

            if (preamble.IsControl && preamble.Version != Constants.CurrentProtocolVersion)
            {
                throw new NotSupportedException("This control frame uses an unsupported version: " + preamble.Version);
            }

            Frame wholeFrame = GetFrameType(preamble);
            if (!await TryFillAsync(wholeFrame.Buffer, Constants.FramePreambleSize, wholeFrame.Buffer.Length - Constants.FramePreambleSize, _cancel))
            {
                return null;
            }

            return wholeFrame;
        }

        private Frame GetFrameType(Frame preamble)
        {
            if (!preamble.IsControl)
            {
                return new DataFrame(preamble);
            }
            switch (preamble.FrameType)
            {
                case ControlFrameType.Credential:
                    return new CredentialFrame(preamble);

                case ControlFrameType.GoAway:
                    return new GoAwayFrame(preamble);

                case ControlFrameType.Headers:
                    return new HeadersFrame(preamble);

                case ControlFrameType.Ping:
                    return new PingFrame(preamble);

                case ControlFrameType.RstStream:
                    return new RstStreamFrame(preamble);

                case ControlFrameType.Settings:
                    return new SettingsFrame(preamble);

                case ControlFrameType.SynReply:
                    return new SynReplyFrame(preamble);

                case ControlFrameType.SynStream:
                    return new SynStreamFrame(preamble);

                case ControlFrameType.WindowUpdate:
                    return new WindowUpdateFrame(preamble);

                default:
                    throw new NotImplementedException("Frame type: " + preamble.FrameType);
            }
        }

        private async Task<bool> TryFillAsync(byte[] buffer, int offset, int count, CancellationToken cancel)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                cancel.ThrowIfCancellationRequested();
                // TODO: Over-read into a buffer to reduce the number of native read operations.
                int read = await _stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancel);
                if (read <= 0)
                {
                    // The stream ended before we could get as much as we needed.
                    return false;
                }
                totalRead += read;
            }
            return true;
        }
    }
}
