using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedProtocol.IO
{
    public class PriorityQueue
    {
        private readonly ConcurrentQueue<PriorityQueueEntry> _queue;

        public PriorityQueue()
        {
            _queue = new ConcurrentQueue<PriorityQueueEntry>();
        }

        public void Enqueue(PriorityQueueEntry entry)
        {
            _queue.Enqueue(entry);
        }

        public bool TryDequeue(out PriorityQueueEntry entry)
        {
            return _queue.TryDequeue(out entry);
        }

        public bool IsDataAvailable()
        {
            return !_queue.IsEmpty;
        }
    }
}
