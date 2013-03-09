using System;

namespace SharedProtocol.Framing
{
    public class HeadersFrame : StreamControlFrame
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
            : base(new byte[InitialFrameSize + compressedHeaders.Length], streamId)
        {
            FrameType = ControlFrameType.Headers;
            FrameLength = InitialFrameSize - Constants.FramePreambleSize + compressedHeaders.Length;

            // Copy in the headers
            System.Buffer.BlockCopy(compressedHeaders, 0, Buffer, InitialFrameSize, compressedHeaders.Length);
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
