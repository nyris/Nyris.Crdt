
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace Nyris.Crdt.Distributed
{
    /// <summary>
    /// Taken almost as-is from https://stackoverflow.com/questions/7863573/awaitable-task-based-queue
    /// </summary>
    /// <typeparam name="T">The type of the queued element.</typeparam>
    internal sealed class AsyncQueue<T> : IAsyncEnumerable<T>, IDisposable
    {
        public long QueueLength = 0L;

        private readonly SemaphoreSlim _enumerationSemaphore = new(1);
        private readonly BufferBlock<T> _bufferBlock = new();

        public void Enqueue(T item)
        {
            Interlocked.Increment(ref QueueLength);
            _bufferBlock.Post(item);
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token = default)
        {
            // We lock this so we only ever enumerate once at a time.
            // That way we ensure all items are returned in a continuous
            // fashion with no 'holes' in the data when two foreach compete.
            await _enumerationSemaphore.WaitAsync(token);
            try {
                // Return new elements until cancellationToken is triggered.
                while (true) {
                    // Make sure to throw on cancellation so the Task will transfer into a canceled state
                    token.ThrowIfCancellationRequested();
                    yield return await _bufferBlock.ReceiveAsync(token);
                    Interlocked.Decrement(ref QueueLength);
                }
            } finally {
                _enumerationSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public void Dispose() => _enumerationSemaphore.Dispose();
    }
}
