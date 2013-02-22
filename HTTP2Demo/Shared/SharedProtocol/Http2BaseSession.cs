using SharedProtocol.Framing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol
{
    public abstract class Http2BaseSession<T> where T: Http2BaseStream
    {
        protected bool _goAwayReceived;
        protected ConcurrentDictionary<int, T> _activeStreams;
        protected FrameReader _frameReader;
        protected WriteQueue _writeQueue;
        protected Stream _sessionStream;

        protected Http2BaseSession()
        {
            _goAwayReceived = false;
            _activeStreams = new ConcurrentDictionary<int, T>();
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

        protected virtual void DispatchIncomingFrame(Frame frame)
        {
            T stream;
            if (frame.IsControl)
            {
                switch (frame.FrameType)
                {

                    default:
                        throw new NotImplementedException(frame.FrameType.ToString());
                }
            }
            else
            {
                DataFrame dataFrame = (DataFrame)frame;
                stream = GetStream(dataFrame.StreamId);
                stream.ReceiveData(dataFrame);
            }
        }

        // Manage the outgoing queue of requests.
        private Task PumpOutgoingData()
        {
            return _writeQueue.PumpToStreamAsync();
        }

        public T GetStream(int id)
        {
            T stream;
            if (!_activeStreams.TryGetValue(id, out stream))
            {
                // TODO: Session already gone? Send a reset?
                throw new NotImplementedException("Stream id not found: " + id);
            }
            return stream;
        }
    }
}
