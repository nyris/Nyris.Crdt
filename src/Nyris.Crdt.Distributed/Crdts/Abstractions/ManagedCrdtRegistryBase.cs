using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    public abstract class ManagedCrdtRegistryBase<TKey, TItem, TDto> : ManagedCRDT<TDto>
    {
        private readonly ConcurrentDictionary<string, IIndex<TKey, TItem>> _indexes = new();
        private readonly ILogger? _logger;

        /// <inheritdoc />
        protected ManagedCrdtRegistryBase(InstanceId instanceId,
            IAsyncQueueProvider? queueProvider = null,
            ILogger? logger = null) : base(instanceId, queueProvider: queueProvider, logger: logger)
        {
            _logger = logger;
        }

        public abstract ulong Size { get; }

        public abstract IAsyncEnumerable<KeyValuePair<TKey, TItem>> EnumerateItems(
            CancellationToken cancellationToken = default);

        public void RemoveIndex(IIndex<TKey, TItem> index) => RemoveIndex(index.UniqueName);
        public void RemoveIndex(string name) => _indexes.TryRemove(name, out _);

        public async Task AddIndexAsync(IIndex<TKey, TItem> index, CancellationToken cancellationToken = default)
        {
            if (_indexes.ContainsKey(index.UniqueName)) return;

            await foreach (var (key, item) in EnumerateItems(cancellationToken))
            {
                await index.AddAsync(key, item, cancellationToken);
            }

            _indexes.TryAdd(index.UniqueName, index);
        }

        protected Task RemoveItemFromIndexes(TKey key, TItem item, CancellationToken cancellationToken = default)
            => Task.WhenAll(_indexes.Values.Select(i =>
            {
                _logger?.LogDebug("Removing {ItemKey} from {IndexName}", key, i.UniqueName);
                return i.RemoveAsync(key, item, cancellationToken);
            }));

        protected Task AddItemToIndexesAsync(TKey key, TItem item, CancellationToken cancellationToken = default)
        {
            return Task.WhenAll(_indexes.Values.Select(i =>
            {
                _logger?.LogDebug("Adding {ItemKey} to {IndexName}", key, i.UniqueName);
                return i.AddAsync(key, item, cancellationToken);
            }));
        }

        protected bool TryGetIndex<TIndex>(string indexName, [NotNullWhen(true)] out TIndex? result)
            where TIndex : class
        {
            if (!_indexes.TryGetValue(indexName, out var index))
            {
                result = default;
                return false;
            }

            result = index as TIndex;
            return result != null;
        }
    }
}