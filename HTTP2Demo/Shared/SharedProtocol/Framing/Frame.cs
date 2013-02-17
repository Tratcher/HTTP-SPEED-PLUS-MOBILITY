
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

        public Frame()
            : this(new byte[Constants.FramePreambleSize])
        {
        }

        public Frame(byte[] buffer)
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
                return FrameHelpers.GetHighBitAt(_buffer, 0);
            }
            set
            {
                FrameHelpers.SetHighBitAt(_buffer, 0, value);
            }
        }

        // 31 bits, 1-31, Data frame only
        public int DataStreamId
        {
            get
            {
                return FrameHelpers.Get31BitsAt(_buffer, 0);
            }
            set
            {
                FrameHelpers.Set31BitsAt(_buffer, 0, value);
            }
        }

        // 15 bits, 1-15
        public int Version
        {
            get
            {
                return FrameHelpers.Get15BitsAt(_buffer, 0);
            }
            set
            {
                FrameHelpers.Set15BitsAt(_buffer, 0, value);
            }
        }

        // 16 bits, 16-31
        public ControlFrameType FrameType
        {
            get
            {
                return (ControlFrameType)(FrameHelpers.Get16BitsAt(_buffer, 2));
            }
            set
            {
                FrameHelpers.Set16BitsAt(_buffer, 2, (int)value);
            }
        }

        // 8 bits, 32-39
        public FrameFlags Flags
        {
            get
            {
                return (FrameFlags)_buffer[4];
            }
            set
            {
                _buffer[4] = (byte)value;
            }
        }

        // 24 bits, 40-63
        public int Length
        {
            get
            {
                return FrameHelpers.Get24BitsAt(_buffer, 5);
            }
            set
            {
                FrameHelpers.Set24BitsAt(_buffer, 5, value);
            }
        }
    }
}
