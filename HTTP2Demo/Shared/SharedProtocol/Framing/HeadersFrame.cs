using System;

namespace SharedProtocol.Framing
{
    public class HeadersFrame : ControlFrame
    {
        // The number of bytes in the frame, not including the compressed headers.
        private const int InitialFrameSize = 12;

        // Incoming
        public HeadersFrame(Frame preamble)
            : base(preamble)
        {
        }

        // Outgoing
        public HeadersFrame(int streamId, byte[] compressedHeaders)
            : base(new byte[InitialFrameSize + compressedHeaders.Length])
        {
            FrameType = ControlFrameType.Headers;
            FrameLength = InitialFrameSize - Constants.FramePreambleSize + compressedHeaders.Length;
            StreamId = streamId;

            // Copy in the headers
            System.Buffer.BlockCopy(compressedHeaders, 0, Buffer, InitialFrameSize, compressedHeaders.Length);
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
