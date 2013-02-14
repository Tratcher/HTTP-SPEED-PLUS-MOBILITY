
namespace ServerProtocol.Framing
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
                return ((0x80 & _buffer[0]) == 0x80);
            }
            set
            {
                if (value)
                {
                    _buffer[0] |= 0x80;
                }
                else
                {
                    _buffer[0] &= 0x7F;
                }
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
                int highByte = (_buffer[0] & 0x7F);
                return (highByte << 8) | _buffer[1];
            }
            set
            {
                _buffer[0] |= (byte)(value >> 8);
                _buffer[1] = (byte)value;
            }
        }

        // 16 bits, 16-31
        public ControlFrameType FrameType
        {
            get
            {
                return (ControlFrameType)((_buffer[2] << 8) | _buffer[3]);
            }
            set
            {
                _buffer[2] = (byte)((int)value >> 8);
                _buffer[3] = (byte)value;
            }
        }

        // 24 bits, 40-63
        public int Length
        {
            get
            {
                return (_buffer[5] << 16) | (_buffer[6] << 8) | _buffer[7];
            }
            set
            {
                _buffer[5] = (byte)(value >> 16);
                _buffer[6] = (byte)(value >> 8);
                _buffer[7] = (byte)value;
            }
        }
    }
}
