using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerProtocol.Framing
{
    [Flags]
    public enum FrameFlags
    {
        None = 0x00,
        Fin = 0x01,

        // Data frame:
        Compress = 0x02,

        // Control frame:
        ClearSettings = 0x01,
        Unidirectional = 0x02,
    }
}
