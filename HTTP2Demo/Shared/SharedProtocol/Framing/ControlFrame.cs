
namespace SharedProtocol.Framing
{
    // Base class for all control frames
    // |C| Version(15bits) | Type(16bits) |
    // +----------------------------------+
    // | Flags (8)  |  Length (24 bits)   |
    public abstract class ControlFrame : Frame
    {
        // Incoming
        protected ControlFrame(Frame preamble)
            : base(preamble)
        {
        }

        // Outgoing
        protected ControlFrame(byte[] buffer)
            : base(buffer)
        {
            IsControl = true;
            Version = Constants.CurrentProtocolVersion;
        }
    }
}
