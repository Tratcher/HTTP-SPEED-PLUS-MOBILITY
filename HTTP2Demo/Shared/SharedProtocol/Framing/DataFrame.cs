using System;

namespace SharedProtocol.Framing
{
    public class DataFrame : Frame
    {
        // For incoming
        public DataFrame(Frame preamble)
            : base(preamble)
        {
        }

        // For outgoing
        public DataFrame(int streamId, ArraySegment<byte> data)
            : base(new byte[Constants.FramePreambleSize + data.Count])
        {
            IsControl = false;
            FrameLength = data.Count;
            DataStreamId = streamId;
            System.Buffer.BlockCopy(data.Array, data.Offset, Buffer, Constants.FramePreambleSize, data.Count);
        }

        // For outgoing terminator frame
        public DataFrame(int streamId)
            : base(new byte[Constants.FramePreambleSize])
        {
            IsControl = false;
            FrameLength = 0;
            DataStreamId = streamId;
            Flags = FrameFlags.Fin;
        }

        public ArraySegment<byte> Data
        {
            get
            {
                return new ArraySegment<byte>(Buffer, Constants.FramePreambleSize, 
                    Buffer.Length - Constants.FramePreambleSize);
            }
        }
    }
}
