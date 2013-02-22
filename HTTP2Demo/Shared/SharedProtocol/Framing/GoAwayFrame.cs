
namespace SharedProtocol.Framing
{
    public class GoAwayFrame : ControlFrame
    {
        // The number of bytes in the frame.
        private const int InitialFrameSize = 16;

        // Incoming
        public GoAwayFrame(Frame preamble)
            : base(preamble)
        {
        }

        // Outgoing
        public GoAwayFrame(int lastStreamId, GoAwayStatusCode statusCode)
            : base(new byte[InitialFrameSize])
        {
            FrameType = ControlFrameType.GoAway;
            FrameLength = InitialFrameSize - Constants.FramePreambleSize; // 8
            LastGoodStreamId = lastStreamId;
            StatusCode = statusCode;
        }

        // 31 bits
        public int LastGoodStreamId
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
        public GoAwayStatusCode StatusCode
        {
            get
            {
                return (GoAwayStatusCode)FrameHelpers.Get32BitsAt(Buffer, 12);
            }
            set
            {
                FrameHelpers.Set32BitsAt(Buffer, 12, (int)value);
            }
        }
    }
}
