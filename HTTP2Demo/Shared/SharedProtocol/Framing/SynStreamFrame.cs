using System;
using System.Diagnostics.Contracts;

namespace SharedProtocol.Framing
{
    public class SynStreamFrame : StreamControlFrame
    {
        // The number of bytes in the frame, not including the compressed headers.
        private const int InitialFrameSize = 18;

        // Create an outgoing frame
        public SynStreamFrame(int streamId, byte[] headerBytes)
            : base(new byte[InitialFrameSize + headerBytes.Length], streamId)
        {
            FrameType = ControlFrameType.SynStream;
            FrameLength = Buffer.Length - Constants.FramePreambleSize;

            // Copy in the headers
            System.Buffer.BlockCopy(headerBytes, 0, Buffer, InitialFrameSize, headerBytes.Length);
        }

        // Create an incoming frame
        public SynStreamFrame(Frame preamble)
            : base(preamble)
        {
        }

        public int AssociatedStreamId
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

        public Priority Priority
        {
            get
            {
                return (Priority)FrameHelpers.GetHigh3BitsAt(Buffer, 16);
            }
            set
            {
                FrameHelpers.SetHigh3BitsAt(Buffer, 16, (int)value);
            }
        }

        public int Unused
        {
            get
            {
                return FrameHelpers.Get5BitsAt(Buffer, 16);
            }
            set
            {
                FrameHelpers.Set5BitsAt(Buffer, 16, value);
            }
        }

        public int CertClot
        {
            get
            {
                return Buffer[17];
            }
            set
            {
                Contract.Assert(value <= 0xFF);
                Buffer[17] = (byte)value;
            }
        }

        public ArraySegment<byte> CompressedHeaders
        {
            get
            {
                return new ArraySegment<byte>(Buffer, 18, Buffer.Length - 18);
            }
        }
    }
}
