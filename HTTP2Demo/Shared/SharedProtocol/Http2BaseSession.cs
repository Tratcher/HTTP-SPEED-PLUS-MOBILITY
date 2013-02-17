using SharedProtocol.Framing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol
{
    public abstract class Http2BaseSession
    {
        protected bool _goAwayReceived;
        protected ConcurrentDictionary<int, Http2BaseStream> _activeStreams;
        protected FrameReader _frameReader;
        protected WriteQueue _writeQueue;

        protected Http2BaseSession()
        {
            _goAwayReceived = false;
            _activeStreams = new ConcurrentDictionary<int, Http2BaseStream>();
        }
    }
}
