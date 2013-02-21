using System;

namespace SharedProtocol.Framing
{
    public class CredentialFrame : Frame
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
            IsControl = true;
            Version = Constants.CurrentProtocolVersion;
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

        public ArraySegment<byte> CertificateListBlob
        {
            get
            {
                return new ArraySegment<byte>(Buffer, InitialFrameSize + ProofLength,
                    FrameLength + Constants.FramePreambleSize - InitialFrameSize - ProofLength);
            }
        }
    }
}
