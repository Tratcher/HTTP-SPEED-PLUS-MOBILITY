using System;

namespace SharedProtocol.Framing
{
    public class SynReplyFrame : StreamControlFrame
    {
        // The number of bytes in the frame, not including the compressed headers.
        private const int InitialFrameSize = 12;

        // Create an incoming frame
        public SynReplyFrame(Frame preamble)
            : base(preamble)
        {
        }

        // Create an outgoing frame
        public SynReplyFrame(int streamId, byte[] compressedHeaders)
            : base(new byte[InitialFrameSize + compressedHeaders.Length], streamId)
        {
            FrameType = ControlFrameType.SynReply;
            FrameLength = Buffer.Length - Constants.FramePreambleSize;

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
