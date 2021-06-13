using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Sets;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts
{
    /// <summary>
    /// An immutable key-value registry based on <see cref="ManagedOptimizedObservedRemoveSet{TActorId,TItem}"/>,
    /// that is itself a CRDT.
    /// </summary>
    /// <typeparam name="TActorId">Actor id is used to keep track of who (which node) added what.</typeparam>
    /// <typeparam name="TItemKey">Type of the keys of the registry.</typeparam>
    /// <typeparam name="TItemValue">Type of the values in the registry</typeparam>
    /// <typeparam name="TItemValueDto"></typeparam>
    /// <typeparam name="TItemValueFactory"></typeparam>
    /// <typeparam name="TItemValueRepresentation"></typeparam>
    /// <typeparam name="TItemValueImplementation"></typeparam>
    public abstract class ManagedCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueImplementation, TItemValueRepresentation, TItemValueDto, TItemValueFactory>
        : ManagedCRDT<
            ManagedCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueImplementation, TItemValueRepresentation, TItemValueDto, TItemValueFactory>,
            Dictionary<TItemKey, TItemValueRepresentation>,
            ManagedCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueImplementation, TItemValueRepresentation, TItemValueDto, TItemValueFactory>.RegistryDto>,
            ICreateManagedCrdtsInside
        where TItemKey : IEquatable<TItemKey>
        where TActorId : IEquatable<TActorId>
        where TItemValue : ManagedCRDT<TItemValueImplementation, TItemValueRepresentation, TItemValueDto>, TItemValueImplementation
        where TItemValueImplementation : IAsyncCRDT<TItemValueImplementation, TItemValueRepresentation, TItemValueDto>
        where TItemValueFactory : IManagedCRDTFactory<TItemValue, TItemValueImplementation, TItemValueRepresentation, TItemValueDto>, new()
    {
        private static readonly TItemValueFactory Factory = new();

        private readonly OptimizedObservedRemoveSet<TActorId, TItemKey> _keys;
        private readonly ConcurrentDictionary<TItemKey, TItemValue> _dictionary;
        private readonly SemaphoreSlim _semaphore = new(1);
        private ManagedCrdtContext? _context;

        protected ManagedCrdtRegistry(string id) : base(id)
        {
            _keys = new OptimizedObservedRemoveSet<TActorId, TItemKey>();
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>();
        }

        protected ManagedCrdtRegistry(RegistryDto registryDto) : base("")
        {
            _keys = OptimizedObservedRemoveSet<TActorId, TItemKey>.FromDto(registryDto.Keys);
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>(registryDto.Dict
                .ToDictionary(pair => pair.Key, pair => Factory.Create(pair.Value)));
        }

        /// <inheritdoc />
        public override Dictionary<TItemKey, TItemValueRepresentation> Value
        {
            get
            {
                _semaphore.Wait();
                try { return _dictionary.ToDictionary(pair => pair.Key, pair => pair.Value.Value); }
                finally { _semaphore.Release(); }
            }
        }

        public ManagedCrdtContext ManagedCrdtContext
        {
            get => _context ?? throw new ManagedCrdtContextSetupException(
                $"Managed CRDT of type {GetType()} was modified before Add method on a ManagedContext was called");
            set => _context = value;
        }

        public async Task<TItemValue> GetOrCreateAsync(TItemKey key, Func<(TActorId, TItemValue)> createFunc)
        {
            TItemValue value;
            await _semaphore.WaitAsync();
            try
            {
                if (_dictionary.TryGetValue(key, out value!)) return value;

                var (actorId, newValue) = createFunc();
                value = newValue;

                _keys.Add(key, actorId);
                _dictionary.TryAdd(key, value);
                ManagedCrdtContext.Add(value, Factory);
            }
            finally
            {
                _semaphore.Release();
            }
            await StateChangedAsync();
            return value;
        }

        public async Task RemoveAsync(TItemKey key)
        {
            await _semaphore.WaitAsync();
            try
            {
                _keys.Remove(key);
                if (_dictionary.TryRemove(key, out var value))
                {
                    ManagedCrdtContext.Remove<TItemValue, TItemValueImplementation, TItemValueRepresentation, TItemValueDto>(value);
                }
            }
            finally
            {
                _semaphore.Release();
            }
            await StateChangedAsync();
        }

        public override async Task<MergeResult> MergeAsync(ManagedCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueImplementation, TItemValueRepresentation, TItemValueDto, TItemValueFactory> other)
        {
            bool conflict;
            await _semaphore.WaitAsync();
            try
            {
                var keyResult = _keys.Merge(other._keys);

                // drop values that no longer have keys
                if (keyResult == MergeResult.ConflictSolved)
                {
                    foreach (var keyToDrop in _dictionary.Keys.Except(_keys.Value))
                    {
                        _dictionary.TryRemove(keyToDrop, out _);
                    }
                }

                // merge values
                conflict = keyResult == MergeResult.ConflictSolved;
                foreach (var key in _keys.Value)
                {
                    var iHave = _dictionary.TryGetValue(key, out var myValue);
                    var otherHas = other._dictionary.TryGetValue(key, out var otherValue);

                    if (iHave && otherHas)
                    {
                        var valueResult = await myValue!.MergeAsync(otherValue!);
                        conflict = conflict || valueResult == MergeResult.ConflictSolved;
                    }
                    else if (otherHas)
                    {
                        _dictionary[key] = otherValue!;
                        conflict = true;
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }

            await StateChangedAsync();
            return conflict ? MergeResult.ConflictSolved : MergeResult.Identical;
        }

        public override async Task<RegistryDto> ToDtoAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var keyValuePairTasks =
                    _dictionary.Select(pair => new {key = pair.Key, valueTask = pair.Value.ToDtoAsync()}).ToList();
                await Task.WhenAll(keyValuePairTasks.Select(pair => pair.valueTask));
                return new RegistryDto
                {
                    Keys = _keys.ToDto(),
                    Dict = keyValuePairTasks.ToDictionary(pair => pair.key, pair => pair.valueTask.Result)
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        [ProtoContract]
        public sealed class RegistryDto
        {
            [ProtoMember(1)]
            public OptimizedObservedRemoveSet<TActorId, TItemKey>.Dto Keys { get; set; }

            [ProtoMember(2)]
            public Dictionary<TItemKey, TItemValueDto> Dict { get; set; }
        }
    }
}