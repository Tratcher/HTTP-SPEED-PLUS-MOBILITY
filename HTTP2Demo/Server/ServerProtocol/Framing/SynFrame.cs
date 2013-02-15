using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerProtocol.Framing
{
    public class SynFrame : Frame
    {
        // The number of bytes in the frame, not including the compressed headers.
        // Headers will be added later.
        private const int InitialFrameSize = 12;

        // Create an outgoing frame
        public SynFrame(byte[] headerBytes, int streamId)
            : base(new byte[InitialFrameSize + headerBytes.Length])
        {
            IsControl = true;
            Version = Constants.CurrentProtocolVersion;
            FrameType = ControlFrameType.SynReply;
            Length = Buffer.Length - Constants.FramePreambleSize;
            StreamId = streamId;

            // Copy in the headers
            System.Buffer.BlockCopy(headerBytes, 0, Buffer, InitialFrameSize, headerBytes.Length);
        }

        // Create an incoming frame
        public SynFrame(Frame preamble)
            : base(new byte[preamble.Length + Constants.FramePreambleSize])
        {
            System.Buffer.BlockCopy(preamble.Buffer, 0, Buffer, 0, Constants.FramePreambleSize);
        }

        // 31 bits, 65-95
        public int StreamId
        {
            get
            {
                return FrameHelpers.Get31BitsAt(Buffer, 8);
            }
            set
            {
                FrameHelpers.Set31BitsAt(Buffer, 8, value);
            }
        }
    }
}
