using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerProtocol.Framing
{
    public class DataFrame : Frame
    {
        // For incoming
        public DataFrame(Frame preamble)
            : base(new byte[preamble.Length + Constants.FramePreambleSize])
        {
            System.Buffer.BlockCopy(preamble.Buffer, 0, Buffer, 0, Constants.FramePreambleSize);
        }

        // For outgoing
        public DataFrame(int streamId, ArraySegment<byte> data)
            : base(new byte[Constants.FramePreambleSize + data.Count])
        {
            IsControl = false;
            Length = data.Count;
            DataStreamId = streamId;
            System.Buffer.BlockCopy(data.Array, data.Offset, Buffer, Constants.FramePreambleSize, data.Count);
        }

        // For outgoing terminator frame
        public DataFrame(int streamId)
            : base(new byte[Constants.FramePreambleSize])
        {
            IsControl = false;
            Length = 0;
            DataStreamId = streamId;
            Flags = FrameFlags.Fin;
        }
    }
}
