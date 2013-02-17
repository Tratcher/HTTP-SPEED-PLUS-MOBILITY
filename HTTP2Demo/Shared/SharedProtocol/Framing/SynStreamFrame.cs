using System;
using System.Diagnostics.Contracts;

namespace SharedProtocol.Framing
{
    public class SynStreamFrame : Frame
    {
        // The number of bytes in the frame, not including the compressed headers.
        // private const int InitialFrameSize = 18;
        /*
        // Create an outgoing frame
        public SynStreamFrame(byte[] headerBytes, int streamId)
            : base(new byte[InitialFrameSize + headerBytes.Length])
        {
            IsControl = true;
            Version = Constants.CurrentProtocolVersion;
            FrameType = ControlFrameType.SynReply;
            Length = Buffer.Length - Constants.FramePreambleSize;
            StreamId = streamId;

            // Copy in the headers
            System.Buffer.BlockCopy(headerBytes, 0, Buffer, InitialFrameSize, headerBytes.Length);
        }
        */
        // Create an incoming frame
        public SynStreamFrame(Frame preamble)
            : base(new byte[preamble.Length + Constants.FramePreambleSize])
        {
            System.Buffer.BlockCopy(preamble.Buffer, 0, Buffer, 0, Constants.FramePreambleSize);
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

        public int Priority
        {
            get
            {
                return FrameHelpers.GetHigh3BitsAt(Buffer, 16);
            }
            set
            {
                FrameHelpers.SetHigh3BitsAt(Buffer, 16, value);
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
