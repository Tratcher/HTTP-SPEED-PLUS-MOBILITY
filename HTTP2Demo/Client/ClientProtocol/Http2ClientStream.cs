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
        // TODO: Before completing the response task, assign the response stream to either Stream.Null or a QueueStream for data.
        private Stream _responseStream;

        public Http2ClientStream(int id, WriteQueue writeQueue, CancellationToken cancel)
            : base(id, writeQueue, cancel)
        {
            _responseTask = new TaskCompletionSource<SynReplyFrame>();
        }
        
        public Task<SynReplyFrame> GetResponseAsync()
        {
            return _responseTask.Task;
        }

        public Stream ResponseStream
        {
            get
            {
                Contract.Assert(_responseTask.Task.IsCompleted);
                return _responseStream;
            }
        }

        public void StartRequest(SynStreamFrame frame)
        {
            // TODO: Set stream state
            _writeQueue.WriteFrameAsync(frame, _cancel);
        }
    }
}
