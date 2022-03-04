using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Nyris.Contracts.Exceptions;

namespace Nyris.Crdt.Distributed.Utils
{
    internal interface IAsyncQueue<T> : IAsyncEnumerable<T>
    {
        long QueueLength { get; }
        Task EnqueueAsync(T item, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Taken almost as-is from https://stackoverflow.com/questions/7863573/awaitable-task-based-queue
    /// </summary>
    /// <typeparam name="T">The type of the queued element.</typeparam>
    internal sealed class AsyncQueue<T> : IAsyncQueue<T>
    {
        private long _queueLength = 0L;

        private readonly SemaphoreSlim _enumerationSemaphore = new(1);
        private readonly BufferBlock<T> _bufferBlock = new(new DataflowBlockOptions
        {
            BoundedCapacity = 10,
            EnsureOrdered = true
        });

        public long QueueLength => _queueLength;

        public async Task EnqueueAsync(T item, CancellationToken cancellationToken)
        {
            var attempts = 0;
            Interlocked.Increment(ref _queueLength);
            while(attempts < 5)
            {
                var cts = new CancellationTokenSource();
                var t = _bufferBlock.SendAsync(item, cts.Token);
                await Task.WhenAny(t, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
                
                if(t.IsCompletedSuccessfully && t.Result) return;
                cts.Cancel();

                ++attempts;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }

            throw new NyrisException("Could not enqueue");
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
                    Interlocked.Decrement(ref _queueLength);
                }
            } finally {
                _enumerationSemaphore.Release();
            }
        }

        public void Dispose() => _enumerationSemaphore.Dispose();
    }
}