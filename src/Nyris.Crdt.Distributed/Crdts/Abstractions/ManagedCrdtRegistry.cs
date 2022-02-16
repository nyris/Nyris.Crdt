using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Sets;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    public interface IIndex<in TKey, in TItem>
    {
        string UniqueName { get; }
        Task AddAsync(TKey key, TItem item, CancellationToken cancellationToken = default);
        Task RemoveAsync(TKey key, TItem item, CancellationToken cancellationToken = default);
    }

    public interface IIndex<in TInput, TKey, in TItem> : IIndex<TKey, TItem>
    {
        Task<IList<TKey>> FindAsync(TInput input);
    }

    public abstract class ManagedCrdtRegistry<TKey, TItem, TDto> : ManagedCRDT<TDto>
    {
        private readonly ConcurrentDictionary<string, IIndex<TKey, TItem>> _indexes = new();
        private readonly ILogger? _logger;

        /// <inheritdoc />
        protected ManagedCrdtRegistry(string instanceId, ILogger? logger = null) : base(instanceId, logger)
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

        protected Task RemoveFromIndexes(TKey key, TItem item, CancellationToken cancellationToken = default)
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

    /// <summary>
    /// An immutable key-value registry based on <see cref="ManagedOptimizedObservedRemoveSet{TImplementation,TActorId,TItem}"/>,
    /// that is itself a CRDT.
    /// </summary>
    /// <typeparam name="TActorId">Actor id is used to keep track of who (which node) added what.</typeparam>
    /// <typeparam name="TItemKey">Type of the keys of the registry.</typeparam>
    /// <typeparam name="TItemValue">Type of the values in the registry</typeparam>
    /// <typeparam name="TItemValueDto"></typeparam>
    /// <typeparam name="TItemValueFactory"></typeparam>
    /// <typeparam name="TImplementation"></typeparam>
    public abstract class ManagedCrdtRegistry<TImplementation, TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory>
        : ManagedCrdtRegistry<TItemKey, TItemValue, ManagedCrdtRegistry<TImplementation, TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory>.RegistryDto>,
            ICreateAndDeleteManagedCrdtsInside
        where TItemKey : IEquatable<TItemKey>, IComparable<TItemKey>, IHashable
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>, IHashable
        where TItemValue : ManagedCRDT<TItemValueDto>
        where TItemValueFactory : IManagedCRDTFactory<TItemValue, TItemValueDto>, new()
        where TImplementation : ManagedCrdtRegistry<TImplementation, TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory>
    {
        private readonly ILogger? _logger;
        private static readonly TItemValueFactory Factory = new();

        private readonly HashableOptimizedObservedRemoveSet<TActorId, TItemKey> _keys;
        private readonly ConcurrentDictionary<TItemKey, TItemValue> _dictionary;
        private readonly SemaphoreSlim _semaphore = new(1);
        private ManagedCrdtContext? _context;

        protected ManagedCrdtRegistry(string id, ILogger? logger = null) : base(id, logger)
        {
            _logger = logger;
            _keys = new HashableOptimizedObservedRemoveSet<TActorId, TItemKey>();
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>();
        }

        /// <inheritdoc />
        public override ulong Size => (ulong)_dictionary.Count;

        /// <inheritdoc />
        public override async IAsyncEnumerable<KeyValuePair<TItemKey, TItemValue>> EnumerateItems(CancellationToken cancellationToken = default)
        {
            foreach (var key in _keys.Value)
            {
                if (_dictionary.TryGetValue(key, out var item))
                {
                    yield return new KeyValuePair<TItemKey, TItemValue>(key, item);
                }
            }
        }

        public IReadOnlyDictionary<TItemKey, T> Value<T>(Func<TItemValue, T> func) => _dictionary
            .ToDictionary(pair => pair.Key, pair => func(pair.Value));

        public ManagedCrdtContext ManagedCrdtContext
        {
            get => _context ?? throw new ManagedCrdtContextSetupException(
                $"Managed CRDT of type {GetType()} was modified before Add method on a ManagedContext was called");
            set => _context = value;
        }

        /// <inheritdoc />
        Task ICreateAndDeleteManagedCrdtsInside.MarkForDeletionLocallyAsync(string instanceId,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task<bool> TryAddAsync(TItemKey key,
            TActorId actorId,
            TItemValue value,
            int waitForPropagationToNumNodes = 0,
            string traceId = "",
            CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_keys.Contains(key)) return false;

                _keys.Add(key, actorId);
                _dictionary.TryAdd(key, value);
                ManagedCrdtContext.Add(value, Factory);
            }
            finally
            {
                _semaphore.Release();
            }

            _logger?.LogDebug("TraceId: {TraceId}, item with key {ItemKey} added to registry, propagating",
                traceId, key);
            await StateChangedAsync(propagationCounter: waitForPropagationToNumNodes,
                traceId: traceId,
                cancellationToken: cancellationToken);
            _logger?.LogDebug("TraceId: {TraceId}, changes to registry propagated, end of {FuncName}",
                traceId, nameof(TryAddAsync));
            return true;
        }

        public bool TryGetValue(TItemKey key, [MaybeNullWhen(false)] out TItemValue value)
            => _dictionary.TryGetValue(key, out value);

        public async Task<TItemValue> GetOrCreateAsync(TItemKey key,
            Func<(TActorId, TItemValue)> createFunc,
            int waitForPropagationToNumNodes = 0,
            CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            TItemValue value;
            try
            {
                if (_dictionary.TryGetValue(key, out var v)) return v;

                var (actorId, newValue) = createFunc();

                _keys.Add(key, actorId);
                _dictionary.TryAdd(key, newValue);
                ManagedCrdtContext.Add(newValue, Factory);
                value = newValue;
            }
            finally
            {
                _semaphore.Release();
            }

            await StateChangedAsync(propagationCounter: waitForPropagationToNumNodes, cancellationToken: cancellationToken);
            return value;
        }

        public async Task RemoveAsync(TItemKey key,
            int waitForPropagationToNumNodes = 0,
            CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _keys.Remove(key);
                if (_dictionary.TryRemove(key, out var value))
                {
                    ManagedCrdtContext.Remove<TItemValue, TItemValueDto>(value);
                }
            }
            finally
            {
                _semaphore.Release();
                await StateChangedAsync(propagationCounter: waitForPropagationToNumNodes, cancellationToken: cancellationToken);
            }
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(RegistryDto other, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var keyResult = _keys.MaybeMerge(other.Keys);

                // drop values that no longer have keys
                if (keyResult != MergeResult.ConflictSolved) return keyResult;

                foreach (var keyToDrop in _dictionary.Keys.Except(_keys.Value))
                {
                    if (_dictionary.TryRemove(keyToDrop, out var crdt))
                    {
                        ManagedCrdtContext.Remove<TItemValue, TItemValueDto>(crdt);
                    }
                }

                // take all new keys, create crdt instances and add them to the context
                foreach (var keyToAdd in _keys.Value.Except(_dictionary.Keys))
                {
                    if (other.InstanceIds != null &&
                        other.InstanceIds.TryGetValue(keyToAdd, out var v) &&
                        _dictionary.TryAdd(keyToAdd, Factory.Create(v)))
                    {
                        ManagedCrdtContext.Add(_dictionary[keyToAdd], Factory);
                    }
                }

                return MergeResult.ConflictSolved;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public override async Task<RegistryDto> ToDtoAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                return new RegistryDto
                {
                    Keys = _keys.ToDto(),
                    InstanceIds = _dictionary.ToDictionary(pair => pair.Key, pair => pair.Value.InstanceId)
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<RegistryDto> EnumerateDtoBatchesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await ToDtoAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override ReadOnlySpan<byte> CalculateHash() => HashingHelper.Combine(_keys.CalculateHash(),
            HashingHelper.Combine(_dictionary.OrderBy(pair => pair.Key)));

        [ProtoContract]
        public sealed class RegistryDto
        {
            [ProtoMember(1)]
            public OptimizedObservedRemoveSet<TActorId, TItemKey>.OptimizedObservedRemoveSetDto? Keys { get; set; }

            [ProtoMember(2)]
            public Dictionary<TItemKey, string>? InstanceIds { get; set; }
        }
    }
}