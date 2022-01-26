using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Sets;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    /// <summary>
    /// An immutable key-value registry based on <see cref="ManagedOptimizedObservedRemoveSet{TImplementation,TActorId,TItem}"/>,
    /// that is itself a CRDT.
    /// </summary>
    /// <typeparam name="TActorId">Actor id is used to keep track of who (which node) added what.</typeparam>
    /// <typeparam name="TItemKey">Type of the keys of the registry.</typeparam>
    /// <typeparam name="TItemValue">Type of the values in the registry</typeparam>
    /// <typeparam name="TItemValueDto"></typeparam>
    /// <typeparam name="TItemValueFactory"></typeparam>
    /// <typeparam name="TItemValueRepresentation"></typeparam>
    /// <typeparam name="TImplementation"></typeparam>
    public abstract class ManagedCrdtRegistry<TImplementation, TActorId, TItemKey, TItemValue, TItemValueRepresentation, TItemValueDto, TItemValueFactory>
        : ManagedCRDT<TImplementation,
            IReadOnlyDictionary<TItemKey, TItemValueRepresentation>,
            ManagedCrdtRegistry<TImplementation, TActorId, TItemKey, TItemValue, TItemValueRepresentation, TItemValueDto, TItemValueFactory>.RegistryDto>,
            ICreateAndDeleteManagedCrdtsInside
        where TItemKey : IEquatable<TItemKey>, IComparable<TItemKey>, IHashable
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>, IHashable
        where TItemValue : ManagedCRDT<TItemValue, TItemValueRepresentation, TItemValueDto>
        where TItemValueFactory : IManagedCRDTFactory<TItemValue, TItemValueRepresentation, TItemValueDto>, new()
        where TImplementation : ManagedCrdtRegistry<TImplementation, TActorId, TItemKey, TItemValue, TItemValueRepresentation, TItemValueDto, TItemValueFactory>
    {
        private static readonly TItemValueFactory Factory = new();

        private readonly HashableOptimizedObservedRemoveSet<TActorId, TItemKey> _keys;
        private readonly ConcurrentDictionary<TItemKey, TItemValue> _dictionary;
        private readonly SemaphoreSlim _semaphore = new(1);
        private ManagedCrdtContext? _context;

        protected ManagedCrdtRegistry(string id) : base(id)
        {
            _keys = new HashableOptimizedObservedRemoveSet<TActorId, TItemKey>();
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>();
        }

        protected ManagedCrdtRegistry(RegistryDto registryDto, string instanceId) : base(instanceId)
        {
            var keys = registryDto?.Keys ?? new OptimizedObservedRemoveSet<TActorId, TItemKey>.OptimizedObservedRemoveSetDto();
            _keys = HashableOptimizedObservedRemoveSet<TActorId, TItemKey>.FromDto(keys);
            var dict = registryDto?.InstanceIds
                           .ToDictionary(pair => pair.Key, pair => Factory.Create(pair.Value))
                       ?? new Dictionary<TItemKey, TItemValue>();
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>(dict);
        }

        /// <inheritdoc />
        public override IReadOnlyDictionary<TItemKey, TItemValueRepresentation> Value => _dictionary
            .ToDictionary(pair => pair.Key, pair => pair.Value.Value);

        public ManagedCrdtContext ManagedCrdtContext
        {
            get => _context ?? throw new ManagedCrdtContextSetupException(
                $"Managed CRDT of type {GetType()} was modified before Add method on a ManagedContext was called");
            set => _context = value;
        }

        /// <inheritdoc />
        async Task ICreateAndDeleteManagedCrdtsInside.MarkForDeletionAsync(string instanceId,
            CancellationToken cancellationToken)
        {
            var key = _dictionary.FirstOrDefault(pair => pair.Value.InstanceId == instanceId).Key;
            if (key.Equals(default)) return;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _dictionary.TryRemove(key, out _);
                _keys.Remove(key);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> TryAddAsync(TItemKey key,
            TActorId actorId,
            TItemValue value,
            int waitForPropagationToNumNodes = 0,
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

            await StateChangedAsync(propagationCounter: waitForPropagationToNumNodes, cancellationToken: cancellationToken);
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
                    ManagedCrdtContext.Remove<TItemValue, TItemValueRepresentation, TItemValueDto>(value);
                }
            }
            finally
            {
                _semaphore.Release();
                await StateChangedAsync(propagationCounter: waitForPropagationToNumNodes, cancellationToken: cancellationToken);
            }
        }

        public override async Task<MergeResult> MergeAsync(TImplementation other, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var keyResult = _keys.Merge(other._keys);

                // drop values that no longer have keys
                if (keyResult != MergeResult.ConflictSolved) return keyResult;

                foreach (var keyToDrop in _dictionary.Keys.Except(_keys.Value))
                {
                    if (_dictionary.TryRemove(keyToDrop, out var crdt))
                    {
                        ManagedCrdtContext.Remove<TItemValue, TItemValueRepresentation, TItemValueDto>(crdt);
                    }
                }

                // crdts that are not new, are already managed by the context (i.e. updates are propagated and synced)
                foreach (var keyToAdd in _keys.Value.Except(_dictionary.Keys))
                {
                    if (other._dictionary.TryGetValue(keyToAdd, out var v) &&
                        _dictionary.TryAdd(keyToAdd, v))
                    {
                        ManagedCrdtContext.Add(v, Factory);
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
            public OptimizedObservedRemoveSet<TActorId, TItemKey>.OptimizedObservedRemoveSetDto Keys { get; set; } = new();

            [ProtoMember(2)]
            public Dictionary<TItemKey, string> InstanceIds { get; set; } = new();
        }
    }
}