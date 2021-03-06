﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientProtocol
{
    public struct HandshakeResponse
    {
        // Response data up through the \r\n\r\n terminator
        public ArraySegment<byte> ResponseBytes;
        // Any data we accidently read past the terminator, pass on to the frame parser
        public ArraySegment<byte> ExtraData;
        public HandshakeResult Result;
    }
}
