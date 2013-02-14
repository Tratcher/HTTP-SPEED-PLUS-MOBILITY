using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerProtocol.Framing
{
    public enum ControlFrameType
    {
        None = 0,
        SynStream = 1,
        SynReply = 2,
        RstStream = 3,
        Settings = 4,
        // 5?
        Ping = 6,
        GoAway = 7,
        Headers = 8,
        WindowUpdate = 9,
        // 10?
        Credential = 11,
    }
}
