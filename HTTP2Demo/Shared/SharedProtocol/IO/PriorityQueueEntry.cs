using SharedProtocol.Framing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharedProtocol.IO
{
    public class PriorityQueueEntry
    {
        private TaskCompletionSource<object> _tcs;
        private Frame _frame; // null for FlushAsync
        private CancellationToken _cancel;
        private Priority _priority;

        // Flush
        public PriorityQueueEntry(Priority priority, CancellationToken cancel)
        {
            _frame = null;
            _priority = priority;
            _tcs = new TaskCompletionSource<object>();
            _cancel = cancel;
        }

        public PriorityQueueEntry(Frame frame, Priority priority, CancellationToken cancel)
        {
            _frame = frame;
            _priority = priority;
            _tcs = new TaskCompletionSource<object>();
            _cancel = cancel;
        }

        public Priority Priority { get { return _priority; } }

        public bool IsFlush { get { return _frame == null; } }

        public Frame Frame { get { return _frame; } }

        public byte[] Buffer { get { return (_frame  != null) ? _frame.Buffer : null; } }

        public Task Task { get { return _tcs.Task; } }

        public CancellationToken CancellationToken { get { return _cancel; } }

        internal void Complete()
        {
            _tcs.TrySetResult(null);
        }

        internal void Cancel()
        {
            _tcs.TrySetCanceled();
        }

        internal void Fail(Exception ex)
        {
            _tcs.TrySetException(ex);
        }
    }
}
