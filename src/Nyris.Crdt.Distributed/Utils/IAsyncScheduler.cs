using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Utils
{
    public interface IAsyncScheduler<T> : IAsyncEnumerable<T>
    {
        long QueueLength { get; }
        Task EnqueueAsync(T item, CancellationToken cancellationToken);
    }
}
