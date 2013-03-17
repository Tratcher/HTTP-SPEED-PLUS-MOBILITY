using SharedProtocol;
using SharedProtocol.Framing;
using SharedProtocol.IO;
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
        private CancellationTokenRegistration _cancellation;

        public Http2ClientStream(int id, Priority priority, WriteQueue writeQueue, CancellationToken cancel)
            : base(id, writeQueue, cancel)
        {
            _priority = priority;
            _responseTask = new TaskCompletionSource<SynReplyFrame>();
            _outputStream = new OutputStream(id, _priority, writeQueue);
            _cancellation = _cancel.Register(Cancel, this);
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

        private bool RequestHeadersSent
        {
            get { return (_state & StreamState.RequestHeaders) == StreamState.RequestHeaders; }
            set { Contract.Assert(value); _state |= StreamState.RequestHeaders; }
        }

        private bool ResponseHeadersReceived
        {
            get { return (_state & StreamState.ResponseHeaders) == StreamState.ResponseHeaders; }
            set { Contract.Assert(value); _state |= StreamState.ResponseHeaders; }
        }

        public override void EnsureStarted()
        {
            if (!RequestHeadersSent)
            {
                throw new InvalidOperationException("Request not set yet.");
            }
        }

        public void StartRequest(IList<KeyValuePair<string, string>> pairs, int certIndex, bool hasRequestBody)
        {
            // Serialize the request as a SynStreamFrame and submit it. (FIN if there is no body)
            byte[] headerBytes = FrameHelpers.SerializeHeaderBlock(pairs);
            headerBytes = Compressor.Compress(headerBytes);
            SynStreamFrame frame = new SynStreamFrame(_id, headerBytes);
            frame.CertClot = certIndex;
            frame.Priority = _priority;

            // Set stream state
            if (!hasRequestBody)
            {
                frame.IsFin = true;
                FinSent = true;
            }
            RequestHeadersSent = true;

            // Note that SynStreamFrames have to be sent in sequential ID order, so they're 
            // put into the control priority queue.
            _writeQueue.WriteFrameAsync(frame, Priority.Control, _cancel);
        }

        public void SetReply(SynReplyFrame frame)
        {
            // May have been cancelled already
            if (!_responseTask.Task.IsCompleted)
            {
                ResponseHeadersReceived = true;
                if (frame.IsFin)
                {
                    FinReceived = true;
                    _incomingStream = Stream.Null;
                }
                else
                {
                    _incomingStream = new InputStream(Constants.DefaultFlowControlCredit, SendWindowUpdate); // TODO: Needs to handle flow control, send window updates.
                }
                // Dispatch, as TrySetResult will synchronously execute the waiters callback and block our message pump.
                Task.Run(() => _responseTask.TrySetResult(frame));
            }
        }

        public async Task<IList<KeyValuePair<string, string>>> GetResponseAsync()
        {
            // Wait for and desterilize the response SynReplyFrame
            SynReplyFrame responseFrame = await _responseTask.Task;
            // Decompress and distribute headers
            byte[] rawHeaders = Compressor.Decompress(responseFrame.CompressedHeaders);
            IList<KeyValuePair<string, string>> pairs = FrameHelpers.DeserializeHeaderBlock(rawHeaders);
            return pairs;
        }

        // Send a Fin frame
        public void EndRequest()
        {
            DataFrame terminator = new DataFrame(_id);
            FinSent = true;
            _writeQueue.WriteFrameAsync(terminator, _priority, _cancel);
        }

        private static void Cancel(object obj)
        {
            Http2ClientStream clientStream = (Http2ClientStream)obj;
            if (!clientStream.Disposed)
            {
                // Abort locally
                clientStream.Reset(ResetStatusCode.Cancel);
                // Note we send the reset even if we've sent and received a FIN because when the write queue got flushed,
                // we're not sure if our fin actually got sent.
                if (!clientStream.ResetSent)
                {
                    RstStreamFrame reset = new RstStreamFrame(clientStream.Id, ResetStatusCode.Cancel);
                    clientStream.ResetSent = true;
                    clientStream._writeQueue.WriteFrameAsync(reset, Priority.Control, CancellationToken.None);
                }
                clientStream.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellation.Dispose();
                _responseTask.TrySetCanceled();
                _outputStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
