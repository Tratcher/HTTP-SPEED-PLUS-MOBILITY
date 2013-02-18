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

        public Task StartPumps()
        {
            // Listen for incoming Http/2.0 frames
            Task incomingTask = PumpIncommingData();
            // Send outgoing Http/2.0 frames
            Task outgoingTask = PumpOutgoingData();

            return Task.WhenAll(incomingTask, outgoingTask);
        }

        // Read HTTP/2.0 frames from the raw stream and dispatch them to the appropriate virtual streams for processing.
        private async Task PumpIncommingData()
        {
            while (!_goAwayReceived)
            {
                Frame frame = await _frameReader.ReadFrameAsync();
                DispatchIncomingFrame(frame);
            }
        }

        protected abstract void DispatchIncomingFrame(Frame frame);

        // Manage the outgoing queue of requests.
        private Task PumpOutgoingData()
        {
            return _writeQueue.PumpToStreamAsync();
        }
    }
}
