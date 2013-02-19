using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace SharedProtocol.Framing
{
    // Helpers for reading binary fields of various sizes
    public static class FrameHelpers
    {
        public static bool GetHighBitAt(byte[] buffer, int offset)
        {
            Contract.Assert(offset >= 0 && offset < buffer.Length);
            return ((0x80 & buffer[offset]) == 0x80);
        }

        public static void SetHighBitAt(byte[] buffer, int offset, bool value)
        {
            Contract.Assert(offset >= 0 && offset < buffer.Length);
            if (value)
            {
                buffer[offset] |= 0x80;
            }
            else
            {
                buffer[offset] &= 0x7F;
            }
        }

        public static int GetHigh3BitsAt(byte[] buffer, int offset)
        {
            Contract.Assert(offset >= 0 && offset < buffer.Length);
            return ((0xE0 & buffer[offset]) >> 5);
        }

        public static void SetHigh3BitsAt(byte[] buffer, int offset, int value)
        {
            Contract.Assert(offset >= 0 && offset < buffer.Length);
            byte lower5Bits = (byte)(buffer[offset] & 0x1F);
            byte upper3Bits = (byte)(value << 5);
            buffer[offset] = (byte)(upper3Bits | lower5Bits);
        }

        public static int Get5BitsAt(byte[] buffer, int offset)
        {
            Contract.Assert(offset >= 0 && offset < buffer.Length);
            return (0x1F & buffer[offset]);
        }

        public static void Set5BitsAt(byte[] buffer, int offset, int value)
        {
            Contract.Assert(offset >= 0 && offset < buffer.Length);
            Contract.Assert(value <= 0x1F);
            byte lower5Bits = (byte)(value & 0x1F);
            byte upper3Bits = (byte)(buffer[offset] & 0xE0);
            buffer[offset] = (byte)(upper3Bits | lower5Bits);
        }

        public static int Get15BitsAt(byte[] buffer, int offset)
        {
            Contract.Assert(offset >= 0 && offset + 1 < buffer.Length);
            int highByte = (buffer[offset] & 0x7F);
            return (highByte << 8) | buffer[offset + 1];
        }

        public static void Set15BitsAt(byte[] buffer, int offset, int value)
        {
            Contract.Assert(offset >= 0 && offset + 1 < buffer.Length);
            Contract.Assert(value <= 0x7FFF);
            buffer[offset] |= (byte)((value >> 8) & 0x7F);
            buffer[offset + 1] = (byte)value;
        }

        public static int Get16BitsAt(byte[] buffer, int offset)
        {
            Contract.Assert(offset >= 0 && offset + 1 < buffer.Length);
            return (buffer[offset] << 8) | buffer[offset + 1];
        }

        public static void Set16BitsAt(byte[] buffer, int offset, int value)
        {
            Contract.Assert(offset >= 0 && offset + 1 < buffer.Length);
            Contract.Assert(value <= 0xFFFF);
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        public static int Get24BitsAt(byte[] buffer, int offset)
        {
            Contract.Assert(offset >= 0 && offset + 2 < buffer.Length);
            return (buffer[offset] << 16) | (buffer[offset + 1] << 8) | buffer[offset + 2];
        }

        public static void Set24BitsAt(byte[] buffer, int offset, int value)
        {
            Contract.Assert(offset >= 0 && offset + 2 < buffer.Length);
            Contract.Assert(value <= 0xFFFFF);
            buffer[offset] = (byte)(value >> 16);
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)value;
        }

        public static int Get31BitsAt(byte[] buffer, int offset)
        {
            Contract.Assert(offset >= 0 && offset + 3 < buffer.Length);
            int highByte = (buffer[offset] & 0x7F);
            return (highByte << 24)
                | buffer[offset + 1] << 16
                | buffer[offset + 2] << 8
                | buffer[offset + 3];
        }

        public static void Set31BitsAt(byte[] buffer, int offset, int value)
        {
            Contract.Assert(offset >= 0 && offset + 3 < buffer.Length);
            Contract.Assert(value <= 0x7FFFFF);
            buffer[offset] |= (byte)((value >> 24) & 0x7F);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        public static int Get32BitsAt(byte[] buffer, int offset)
        {
            Contract.Assert(offset >= 0 && offset + 3 < buffer.Length);
            return (buffer[offset] << 24)
                | buffer[offset + 1] << 16
                | buffer[offset + 2] << 8
                | buffer[offset + 3];
        }

        public static void Set32BitsAt(byte[] buffer, int offset, int value)
        {
            Contract.Assert(offset >= 0 && offset + 3 < buffer.Length);
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        // TODO: The spec reallly needs to change the header encoding to UTF8
        public static void SetAsciiAt(byte[] buffer, int offset, string value)
        {
            Contract.Assert(offset >= 0 && offset + value.Length - 1 < buffer.Length);
            Encoding.ASCII.GetBytes(value, 0, value.Length, buffer, offset);
        }

        public static string GetAsciiAt(ArraySegment<byte> segment)
        {
            return Encoding.ASCII.GetString(segment.Array, segment.Offset, segment.Count);
        }

        public static string GetAsciiAt(byte[] buffer, int offset, int length)
        {
            Contract.Assert(offset >= 0 && offset + length - 1 < buffer.Length);
            return Encoding.ASCII.GetString(buffer, offset, length);
        }

        public static byte[] SerializeHeaderBlock(IList<KeyValuePair<string, string>> pairs)
        {
            int encodedLength = 4 // 32 bit count of name value pairs
                + 8 * pairs.Count; // A 32 bit size per header and value;
            for (int i = 0; i < pairs.Count; i++)
            {
                encodedLength += pairs[i].Key.Length + pairs[i].Value.Length;
            }

            byte[] buffer = new byte[encodedLength];
            FrameHelpers.Set32BitsAt(buffer, 0, pairs.Count);
            int offset = 4;
            for (int i = 0; i < pairs.Count; i++)
            {
                KeyValuePair<string, string> pair = pairs[i];
                FrameHelpers.Set32BitsAt(buffer, offset, pair.Key.Length);
                offset += 4;
                FrameHelpers.SetAsciiAt(buffer, offset, pair.Key);
                offset += pair.Key.Length;
                FrameHelpers.Set32BitsAt(buffer, offset, pair.Value.Length);
                offset += 4;
                FrameHelpers.SetAsciiAt(buffer, offset, pair.Value);
                offset += pair.Value.Length;
            }
            return buffer;
        }
    }
}
