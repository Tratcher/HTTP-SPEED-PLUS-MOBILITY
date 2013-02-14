using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerProtocol.Framing
{
    // Helpers for reading binary fields of various sizes
    public static class FrameHelpers
    {
        public static int Get31BitsAt(byte[] buffer, int offset)
        {
            int highByte = (buffer[offset] & 0x7F);
            return (highByte << 24)
                | buffer[offset + 1] << 16
                | buffer[offset + 2] << 8
                | buffer[offset + 3];
        }

        public static void Set31BitsAt(byte[] buffer, int offset, int value)
        {
            buffer[offset] |= (byte)((value >> 24) & 0x7F);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}
