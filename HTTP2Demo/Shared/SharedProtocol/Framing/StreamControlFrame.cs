using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol.Framing
{
    public abstract class StreamControlFrame : ControlFrame
    {
        // Incoming
        protected StreamControlFrame(Frame preamble)
            : base(preamble)
        {
        }

        // Outgoing
        protected StreamControlFrame(byte[] buffer, int streamId)
            : base(buffer)
        {
            IsControl = true;
            StreamId = streamId;
            Version = Constants.CurrentProtocolVersion;
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
