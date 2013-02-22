﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol.Framing
{
    public class RstStreamFrame : ControlFrame
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
            : base(new byte[InitialFrameSize])
        {
            FrameType = ControlFrameType.RstStream;
            FrameLength = InitialFrameSize - Constants.FramePreambleSize; // 8
            StreamId = id;
            StatusCode = statusCode;
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
