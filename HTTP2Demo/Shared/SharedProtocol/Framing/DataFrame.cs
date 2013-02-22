using System;

namespace SharedProtocol.Framing
{
    // |C|       Stream-ID (31bits)       |
    // +----------------------------------+
    // | Flags (8)  |  Length (24 bits)   |
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
            StreamId = streamId;
            System.Buffer.BlockCopy(data.Array, data.Offset, Buffer, Constants.FramePreambleSize, data.Count);
        }

        // For outgoing terminator frame
        public DataFrame(int streamId)
            : base(new byte[Constants.FramePreambleSize])
        {
            IsControl = false;
            FrameLength = 0;
            StreamId = streamId;
            Flags = FrameFlags.Fin;
        }

        // 31 bits, 1-31, Data frame only
        public int StreamId
        {
            get
            {
                return FrameHelpers.Get31BitsAt(Buffer, 0);
            }
            set
            {
                FrameHelpers.Set31BitsAt(Buffer, 0, value);
            }
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
