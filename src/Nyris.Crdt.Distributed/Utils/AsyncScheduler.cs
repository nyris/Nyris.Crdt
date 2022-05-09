using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Nyris.Contracts.Exceptions;

namespace Nyris.Crdt.Distributed.Utils
{
    /// <summary>
    /// Taken almost as-is from https://stackoverflow.com/questions/7863573/awaitable-task-based-queue
    /// </summary>
    /// <typeparam name="T">The type of the queued element.</typeparam>
    internal sealed class AsyncScheduler<T> : IAsyncScheduler<T>
    {
        private long _queueLength;
        private readonly SemaphoreSlim _enumerationSemaphore = new(1);
        private readonly BufferBlock<T> _bufferBlock;

        public AsyncScheduler(int queueCapacity)
        {
            _bufferBlock = new(new DataflowBlockOptions
            {
                BoundedCapacity = queueCapacity,
                EnsureOrdered = true
            });
        }

        public long QueueLength => _queueLength;

        public async Task EnqueueAsync(T item, CancellationToken cancellationToken)
        {
            var success = await _bufferBlock.SendAsync(item, cancellationToken);
            if (!success) throw new NyrisException($"Message was not queued");
            Interlocked.Increment(ref _queueLength);
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token = default)
        {
            // We lock this so we only ever enumerate once at a time.
            // That way we ensure all items are returned in a continuous
            // fashion with no 'holes' in the data when two foreach compete.
            await _enumerationSemaphore.WaitAsync(token);
            try
            {
                // Return new elements until cancellationToken is triggered.
                while (true)
                {
                    // Make sure to throw on cancellation so the Task will transfer into a canceled state
                    token.ThrowIfCancellationRequested();
                    yield return await _bufferBlock.ReceiveAsync(token);
                    Interlocked.Decrement(ref _queueLength);
                }
            }
            finally
            {
                _enumerationSemaphore.Release();
            }
        }

        public void Dispose() => _enumerationSemaphore.Dispose();
    }
}
