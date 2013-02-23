﻿
namespace SharedProtocol.Framing
{
    public class WindowUpdateFrame : ControlFrame
    {
        // The number of bytes in the frame.
        private const int InitialFrameSize = 16;
                
        // Incoming
        public WindowUpdateFrame(Frame preamble)
            : base(preamble)
        {
        }

        // Outgoing
        public WindowUpdateFrame(int id, int delta)
            : base(new byte[InitialFrameSize])
        {
            FrameType = ControlFrameType.WindowUpdate;
            FrameLength = InitialFrameSize - Constants.FramePreambleSize; // 8
            StreamId = id;
            Delta = delta;
        }

        // 31 bits
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

        // 31 bits
        public int Delta
        {
            get
            {
                return FrameHelpers.Get31BitsAt(Buffer, 12);
            }
            set
            {
                FrameHelpers.Set31BitsAt(Buffer, 12, value);
            }
        }
    }
}