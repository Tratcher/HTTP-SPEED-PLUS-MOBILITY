using SharedProtocol;
using SharedProtocol.Framing;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientProtocol
{
    public class Http2ClientStream : Http2BaseStream
    {
        private TaskCompletionSource<SynReplyFrame> _responseTask;
        private Stream _outputStream;

        public Http2ClientStream(int id, WriteQueue writeQueue, CancellationToken cancel)
            : base(id, writeQueue, cancel)
        {
            _responseTask = new TaskCompletionSource<SynReplyFrame>();
            _outputStream = new OutputStream(id, writeQueue);
        }

        public Stream RequestStream
        {
            get
            {
                // TODO: Assert request sent
                return _outputStream;
            }
        }

        public Stream ResponseStream
        {
            get
            {
                Contract.Assert(_responseTask.Task.IsCompleted);
                return _incomingStream;
            }
        }

        public void StartRequest(SynStreamFrame frame)
        {
            // TODO: Set stream state
            _writeQueue.WriteFrameAsync(frame, _cancel);
        }

        public void SetReply(SynReplyFrame frame)
        {
            Contract.Assert(!_responseTask.Task.IsCompleted);
            if (frame.IsFin)
            {
                _incomingStream = Stream.Null;
            }
            else
            {
                _incomingStream = new QueueStream();
            }
            _responseTask.TrySetResult(frame);
        }
        
        public Task<SynReplyFrame> GetResponseAsync()
        {
            return _responseTask.Task;
        }

        // Send a Fin frame
        public void EndRequest()
        {
            DataFrame terminator = new DataFrame(_id);
            _writeQueue.WriteFrameAsync(terminator, _cancel);
        }
    }
}
