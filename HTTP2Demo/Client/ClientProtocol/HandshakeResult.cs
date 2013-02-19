using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientProtocol
{
    public enum HandshakeResult
    {
        None,
        // Successful 101 Switching Protocols response
        Upgrade,
        // Some other status code, fall back to pre-HTTP/2.0 framing
        NonUpgrade,
        // We got back a HTTP/2.0 control frame (presumably a reset frame).
        // The server apparently only understands 2.0 on this port.
        UnexpectedControlFrame,
    }
}
