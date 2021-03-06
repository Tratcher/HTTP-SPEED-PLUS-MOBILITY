﻿using SharedProtocol.Framing;
using System;
using System.Threading;
using Xunit;

namespace SharedProtocol.IO.Tests
{
    public class PriorityQueueTests
    {
        [Fact]
        public void EnqueueAndDequeue()
        {
            PriorityQueue queue = new PriorityQueue();
            Assert.False(queue.IsDataAvailable);
            queue.Enqueue(new PriorityQueueEntry(Priority.Pri1));
            Assert.True(queue.IsDataAvailable);
            
            PriorityQueueEntry output;
            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri1, output.Priority);
            Assert.False(queue.IsDataAvailable);
        }

        [Fact]
        public void EnqueueAndDequeueInOrder()
        {
            PriorityQueue queue = new PriorityQueue();
            Assert.False(queue.IsDataAvailable);
            queue.Enqueue(new PriorityQueueEntry(Priority.Pri1));
            queue.Enqueue(new PriorityQueueEntry(Priority.Pri3));
            queue.Enqueue(new PriorityQueueEntry(Priority.Pri6));
            Assert.True(queue.IsDataAvailable);

            PriorityQueueEntry output;
            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri1, output.Priority);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri3, output.Priority);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri6, output.Priority);

            Assert.False(queue.IsDataAvailable);
        }

        [Fact(Skip = "Prioritization has been disabled, it breaks header decompression.")]
        public void EnqueueAndDequeueOutOfOrder()
        {
            PriorityQueue queue = new PriorityQueue();
            Assert.False(queue.IsDataAvailable);
            queue.Enqueue(new PriorityQueueEntry(Priority.Pri1));
            queue.Enqueue(new PriorityQueueEntry(Priority.Ping));
            queue.Enqueue(new PriorityQueueEntry(Priority.Pri3));
            Assert.True(queue.IsDataAvailable);

            PriorityQueueEntry output;
            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Ping, output.Priority);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri1, output.Priority);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri3, output.Priority);

            Assert.False(queue.IsDataAvailable);
        }

        [Fact(Skip = "Prioritization has been disabled, it breaks header decompression.")]
        public void EnqueueAndDequeueReverseOrder()
        {
            PriorityQueue queue = new PriorityQueue();
            Assert.False(queue.IsDataAvailable);
            queue.Enqueue(new PriorityQueueEntry(Priority.Pri7));
            queue.Enqueue(new PriorityQueueEntry(Priority.Pri4));
            queue.Enqueue(new PriorityQueueEntry(Priority.Pri3));
            Assert.True(queue.IsDataAvailable);

            PriorityQueueEntry output;
            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri3, output.Priority);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri4, output.Priority);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri7, output.Priority);

            Assert.False(queue.IsDataAvailable);
        }

        [Fact]
        public void EnqueueAndDequeueEqualPriority()
        {
            PriorityQueue queue = new PriorityQueue();
            Assert.False(queue.IsDataAvailable);
            queue.Enqueue(new PriorityQueueEntry(new DataFrame(1, new ArraySegment<byte>(new byte[1])), Priority.Pri1));
            queue.Enqueue(new PriorityQueueEntry(new DataFrame(2, new ArraySegment<byte>(new byte[1])), Priority.Pri1));
            queue.Enqueue(new PriorityQueueEntry(new DataFrame(3, new ArraySegment<byte>(new byte[1])), Priority.Pri1));
            Assert.True(queue.IsDataAvailable);

            PriorityQueueEntry output;
            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri1, output.Priority);
            Assert.Equal(1, ((DataFrame)output.Frame).StreamId);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri1, output.Priority);
            Assert.Equal(2, ((DataFrame)output.Frame).StreamId);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri1, output.Priority);
            Assert.Equal(3, ((DataFrame)output.Frame).StreamId);

            Assert.False(queue.IsDataAvailable);
        }

        [Fact(Skip="Prioritization has been disabled, it breaks header decompression.")]
        public void EnqueueAndDequeueEqualAndNotEqualPriority()
        {
            PriorityQueue queue = new PriorityQueue();
            Assert.False(queue.IsDataAvailable);
            queue.Enqueue(new PriorityQueueEntry(new DataFrame(1, new ArraySegment<byte>(new byte[1])), Priority.Pri3));
            queue.Enqueue(new PriorityQueueEntry(new DataFrame(1, new ArraySegment<byte>(new byte[1])), Priority.Pri1));
            queue.Enqueue(new PriorityQueueEntry(new DataFrame(2, new ArraySegment<byte>(new byte[1])), Priority.Pri1));
            queue.Enqueue(new PriorityQueueEntry(new DataFrame(2, new ArraySegment<byte>(new byte[1])), Priority.Pri3));
            queue.Enqueue(new PriorityQueueEntry(new DataFrame(3, new ArraySegment<byte>(new byte[1])), Priority.Pri1));
            queue.Enqueue(new PriorityQueueEntry(new DataFrame(3, new ArraySegment<byte>(new byte[1])), Priority.Pri3));
            Assert.True(queue.IsDataAvailable);

            PriorityQueueEntry output;
            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri1, output.Priority);
            Assert.Equal(1, ((DataFrame)output.Frame).StreamId);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri1, output.Priority);
            Assert.Equal(2, ((DataFrame)output.Frame).StreamId);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri1, output.Priority);
            Assert.Equal(3, ((DataFrame)output.Frame).StreamId);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri3, output.Priority);
            Assert.Equal(1, ((DataFrame)output.Frame).StreamId);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri3, output.Priority);
            Assert.Equal(2, ((DataFrame)output.Frame).StreamId);
            Assert.True(queue.IsDataAvailable);

            Assert.True(queue.TryDequeue(out output));
            Assert.NotNull(output);
            Assert.Equal(Priority.Pri3, output.Priority);
            Assert.Equal(3, ((DataFrame)output.Frame).StreamId);

            Assert.False(queue.IsDataAvailable);
        }
    }
}
