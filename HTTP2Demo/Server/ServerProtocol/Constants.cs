using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerProtocol
{
    public class Constants
    {
        // There are always at least 8 bytes in a control frame or data frame
        public const int FramePreambleSize = 8;
        public const int DefaultClientCertVectorSize = 8;
        public const int CurrentProtocolVersion = 3;
        public const int DefaultFlowControlCredit = 64 * 1024; // 64kb

        public const string OwinVersion = "1.0";
    }
}
