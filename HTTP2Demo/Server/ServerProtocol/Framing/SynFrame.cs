using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerProtocol.Framing
{
    public class SynFrame : Frame
    {
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
