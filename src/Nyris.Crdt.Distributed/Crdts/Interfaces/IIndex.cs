using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{
    public interface IIndex<in TInput, TKey, in TItem> : IIndex<TKey, TItem>
    {
        Task<IList<TKey>> FindAsync(TInput input);
    }

    public interface IIndex<in TKey, in TItem>
    {
        string UniqueName { get; }
        Task AddAsync(TKey key, TItem item, CancellationToken cancellationToken = default);
        Task RemoveAsync(TKey key, TItem item, CancellationToken cancellationToken = default);
    }
}