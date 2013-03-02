using System;

namespace SharedProtocol.Framing
{
    public class CredentialFrame : ControlFrame
    {
        // The frame size in bytes except for the proof body and certificate blocks
        private const int InitialFrameSize = 16;

        // Incoming
        public CredentialFrame(Frame preamble)
            : base(preamble)
        {
        }

        // Outgoing
        public CredentialFrame(int slot, ArraySegment<byte> proof, ArraySegment<byte> certificateList)
            : base(new byte[InitialFrameSize + proof.Count + certificateList.Count])
        {
            FrameType = ControlFrameType.Credential;
            FrameLength = InitialFrameSize - Constants.FramePreambleSize + proof.Count + certificateList.Count;

            Slot = slot;
            ProofLength = proof.Count;
            System.Buffer.BlockCopy(proof.Array, proof.Offset, Buffer, InitialFrameSize, proof.Count);
            System.Buffer.BlockCopy(certificateList.Array, certificateList.Offset, Buffer, 
                InitialFrameSize + proof.Count, certificateList.Count);
        }

        public int Slot
        {
            get
            {
                return FrameHelpers.Get16BitsAt(Buffer, 8);
            }
            set
            {
                FrameHelpers.Set16BitsAt(Buffer, 8, value);
            }
        }

        public int ProofLength
        {
            get
            {
                return FrameHelpers.Get32BitsAt(Buffer, 10);
            }
            set
            {
                FrameHelpers.Set32BitsAt(Buffer, 10, value);
            }
        }

        public ArraySegment<byte> Proof
        {
            get
            {
                return new ArraySegment<byte>(Buffer, InitialFrameSize, ProofLength);
            }
        }

        // Certificates
        public ArraySegment<byte> this[int index]
        {
            get
            {
                int offset = InitialFrameSize + ProofLength;
                for (int i = 0; i < index; i++)
                {
                    int certSize = FrameHelpers.Get32BitsAt(Buffer, offset);
                    offset += 4 + certSize;
                }
                return new ArraySegment<byte>(Buffer, offset + 4, FrameHelpers.Get32BitsAt(Buffer, offset));
            }
        }

        // # of Certificates
        public int Count
        {
            get
            {
                int count = 0;
                int offset = InitialFrameSize + ProofLength;
                while (offset < Buffer.Length)
                {
                    int certSize = FrameHelpers.Get32BitsAt(Buffer, offset);
                    offset += 4 + certSize;
                    count++;
                }
                return count;
            }
        }
    }
}
