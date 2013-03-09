using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol.Framing
{
    public class RstStreamFrame : StreamControlFrame
    {
        // The number of bytes in the frame.
        private const int InitialFrameSize = 16;

        // Incoming
        public RstStreamFrame(Frame preamble)
            : base(preamble)
        {
        }

        // Outgoing
        public RstStreamFrame(int id, ResetStatusCode statusCode)
            : base(new byte[InitialFrameSize], id)
        {
            FrameType = ControlFrameType.RstStream;
            FrameLength = InitialFrameSize - Constants.FramePreambleSize; // 8
            StatusCode = statusCode;
        }

        // 32 bits
        public ResetStatusCode StatusCode
        {
            get
            {
                return (ResetStatusCode)FrameHelpers.Get32BitsAt(Buffer, 12);
            }
            set
            {
                FrameHelpers.Set32BitsAt(Buffer, 12, (int)value);
            }
        }
    }
}
