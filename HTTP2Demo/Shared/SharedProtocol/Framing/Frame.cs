
namespace SharedProtocol.Framing
{
    // Represents the initial frame fields on every frame.
    //
    // Control frames:
    // |C| Version(15bits) | Type(16bits) |
    // +----------------------------------+
    // | Flags (8)  |  Length (24 bits)   |
    //
    // Data frames:
    // |C|       Stream-ID (31bits)       |
    // +----------------------------------+
    // | Flags (8)  |  Length (24 bits)   |
    public class Frame
    {
        private byte[] _buffer;

        // For reading the preamble to determine the frame type and length
        public Frame()
            : this(new byte[Constants.FramePreambleSize])
        {
        }

        // For incoming frames
        protected Frame(Frame preamble)
            : this(new byte[Constants.FramePreambleSize + preamble.FrameLength])
        {
            System.Buffer.BlockCopy(preamble.Buffer, 0, Buffer, 0, Constants.FramePreambleSize);
        }

        // For outgoing frames
        protected Frame(byte[] buffer)
        {
            _buffer = buffer;
        }

        public byte[] Buffer
        {
            get { return _buffer; } 
        }

        // 1 bit, 0
        public bool IsControl
        {
            get
            {
                return FrameHelpers.GetHighBitAt(Buffer, 0);
            }
            set
            {
                FrameHelpers.SetHighBitAt(Buffer, 0, value);
            }
        }

        // Control frame specific, but we need to check it before attempting to interpret the frame.
        // 15 bits, 1-15
        public int Version
        {
            get
            {
                return FrameHelpers.Get15BitsAt(Buffer, 0);
            }
            set
            {
                FrameHelpers.Set15BitsAt(Buffer, 0, value);
            }
        }

        // Control frame specific, but used to interpret incoming preambles.
        // 16 bits, 16-31
        public ControlFrameType FrameType
        {
            get
            {
                return (ControlFrameType)FrameHelpers.Get16BitsAt(Buffer, 2);
            }
            set
            {
                FrameHelpers.Set16BitsAt(Buffer, 2, (int)value);
            }
        }

        // 8 bits, 32-39
        public FrameFlags Flags
        {
            get
            {
                return (FrameFlags)Buffer[4];
            }
            set
            {
                Buffer[4] = (byte)value;
            }
        }

        // 24 bits, 40-63
        public int FrameLength
        {
            get
            {
                return FrameHelpers.Get24BitsAt(Buffer, 5);
            }
            set
            {
                FrameHelpers.Set24BitsAt(Buffer, 5, value);
            }
        }
    }
}
