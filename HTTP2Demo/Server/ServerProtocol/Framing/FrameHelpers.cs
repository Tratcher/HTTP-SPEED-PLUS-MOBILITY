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
        public static bool GetHighBitAt(byte[] buffer, int offset)
        {
            return ((0x80 & buffer[offset]) == 0x80);
        }

        public static void SetHighBitAt(byte[] buffer, int offset, bool value)
        {
            if (value)
            {
                buffer[offset] |= 0x80;
            }
            else
            {
                buffer[offset] &= 0x7F;
            }
        }

        public static int Get15BitsAt(byte[] buffer, int offset)
        {
            int highByte = (buffer[offset] & 0x7F);
            return (highByte << 8) | buffer[offset + 1];
        }

        public static void Set15BitsAt(byte[] buffer, int offset, int value)
        {
            buffer[offset] |= (byte)((value >> 8) & 0x7F);
            buffer[offset + 1] = (byte)value;
        }

        public static int Get16BitsAt(byte[] buffer, int offset)
        {
            return (buffer[offset] << 8) | buffer[offset + 1];
        }

        public static void Set16BitsAt(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        public static int Get24BitsAt(byte[] buffer, int offset)
        {
            return (buffer[offset] << 16) | (buffer[offset + 1] << 8) | buffer[offset + 2];
        }

        public static void Set24BitsAt(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 16);
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)value;
        }

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

        public static int Get32BitsAt(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24)
                | buffer[offset + 1] << 16
                | buffer[offset + 2] << 8
                | buffer[offset + 3];
        }

        public static void Set32BitsAt(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        public static void SetAsciiAt(byte[] buffer, int offset, string value)
        {
            Encoding.ASCII.GetBytes(value, 0, value.Length, buffer, offset);
        }
    }
}
