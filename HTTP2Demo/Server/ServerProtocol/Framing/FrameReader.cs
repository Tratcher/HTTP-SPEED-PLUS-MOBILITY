using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerProtocol.Framing
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
            await FillAsync(preamble.Buffer, 0, preamble.Buffer.Length, _cancel);

            if (preamble.Version != Constants.CurrentProtocolVersion)
            {
                throw new NotSupportedException("This control frame uses an unsupported version: " + preamble.Version);
            }

            Frame wholeFrame = GetFrameType(preamble);
            await FillAsync(wholeFrame.Buffer, Constants.FramePreambleSize, wholeFrame.Buffer.Length - Constants.FramePreambleSize, _cancel);

            return preamble;
        }

        private Frame GetFrameType(Frame preamble)
        {
            if (!preamble.IsControl)
            {
                return new DataFrame(preamble);
            }
            switch (preamble.FrameType)
            {
                case ControlFrameType.SynStream:
                    return new SynFrame(preamble);

                default:
                    throw new NotImplementedException("Frame type: " + preamble.FrameType);
            }
        }

        private async Task FillAsync(byte[] buffer, int offset, int count, CancellationToken cancel)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                cancel.ThrowIfCancellationRequested();
                int read = await _stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancel);
                if (read <= 0)
                {
                    // The stream ended before we could get as much as we needed.
                    throw new ObjectDisposedException(_stream.GetType().FullName);
                }
                totalRead += read;
            }
        }
    }
}
