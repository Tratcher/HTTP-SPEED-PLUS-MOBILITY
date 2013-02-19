
using System;

namespace SharedProtocol.Framing
{
    public class SynReplyFrame : Frame
    {
        // The number of bytes in the frame, not including the compressed headers.
        private const int InitialFrameSize = 12;

        // Create an outgoing frame
        public SynReplyFrame(byte[] headerBytes, int streamId)
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
        public SynReplyFrame(Frame preamble)
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

        public ArraySegment<byte> CompressedHeaders
        {
            get
            {
                return new ArraySegment<byte>(Buffer, InitialFrameSize, Buffer.Length - InitialFrameSize);
            }
        }
    }
}
