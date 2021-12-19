using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Sets;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class HashableCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory,
            TItemValueRepresentation>
        : ICRDT<
            HashableCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory, TItemValueRepresentation>,
            IReadOnlyDictionary<TItemKey, TItemValueRepresentation>,
            HashableCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory, TItemValueRepresentation>.HashableCrdtRegistryDto>,
            IHashable
        where TItemKey : IEquatable<TItemKey>, IHashable
        where TActorId : IEquatable<TActorId>, IHashable
        where TItemValue : class, ICRDT<TItemValue, TItemValueRepresentation, TItemValueDto>, IHashable
        where TItemValueFactory : ICRDTFactory<TItemValue, TItemValueRepresentation, TItemValueDto>, new()
    {
        private static readonly TItemValueFactory Factory = new();

        private readonly HashableOptimizedObservedRemoveSet<TActorId, TItemKey> _keys;
        private readonly ConcurrentDictionary<TItemKey, TItemValue> _dictionary;
        private readonly object _mergeLock = new();

        /// <inheritdoc />
        public IReadOnlyDictionary<TItemKey, TItemValueRepresentation> Value
        {
            get
            {
                lock (_mergeLock)
                {
                    return _dictionary.ToDictionary(pair => pair.Key, pair => pair.Value.Value);
                }
            }
        }

        public HashableCrdtRegistry()
        {
            _keys = new HashableOptimizedObservedRemoveSet<TActorId, TItemKey>();
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>();
        }

        private HashableCrdtRegistry(HashableCrdtRegistryDto hashableCrdtRegistryDto)
        {
            _keys = HashableOptimizedObservedRemoveSet<TActorId, TItemKey>.FromDto(hashableCrdtRegistryDto.Keys);
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>(hashableCrdtRegistryDto.Dict?.ToDictionary(pair => pair.Key, pair => Factory.Create(pair.Value))
                          ?? new Dictionary<TItemKey, TItemValue>());
        }

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash()
        {
            lock (_mergeLock)
            {
                return HashingHelper.Combine(HashingHelper.Combine(_keys),
                    HashingHelper.Combine(_dictionary.OrderBy(pair => pair.Key)));
            }
        }

        public bool TryAdd(TActorId actorId, TItemKey key, TItemValue value)
        {
            lock (_mergeLock)
            {
                if (!_dictionary.TryAdd(key, value)) return false;

                _keys.Add(key, actorId);
                return true;
            }
        }

        public bool TryGetValue(TItemKey key, [NotNullWhen(true)] out TItemValue? value)
            // ReSharper disable once InconsistentlySynchronizedField
            => _dictionary.TryGetValue(key, out value);

        public void Remove(TItemKey key)
        {
            lock (_mergeLock)
            {
                _keys.Remove(key);
                _dictionary.Remove(key, out _);
            }
        }

        public MergeResult Merge(HashableCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory, TItemValueRepresentation> other)
        {
            lock (_mergeLock)
            {
                var keyResult = _keys.Merge(other._keys);

                // drop values that no longer have keys
                if (keyResult == MergeResult.ConflictSolved)
                {
                    foreach (var keyToDrop in _dictionary.Keys.Except(_keys.Value))
                    {
                        _dictionary.Remove(keyToDrop, out _);
                    }
                }

                // merge values
                var conflict = keyResult == MergeResult.ConflictSolved;
                foreach (var key in _keys.Value)
                {
                    var iHave = _dictionary.TryGetValue(key, out var myValue);
                    var otherHas = other._dictionary.TryGetValue(key, out var otherValue);

                    if (iHave && otherHas)
                    {
                        var valueResult = myValue!.Merge(otherValue!);
                        conflict = conflict || valueResult == MergeResult.ConflictSolved;
                    }
                    else if (otherHas)
                    {
                        _dictionary[key] = otherValue;
                        conflict = true;
                    }
                }

                return conflict ? MergeResult.ConflictSolved : MergeResult.Identical;
            }
        }

        public HashableCrdtRegistryDto ToDto()
        {
            lock (_mergeLock)
            {
                return new HashableCrdtRegistryDto
                {
                    Keys = _keys.ToDto(),
                    Dict = _dictionary.ToDictionary(pair => pair.Key, pair => pair.Value.ToDto())
                };
            }
        }

        public static HashableCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory, TItemValueRepresentation> FromDto(HashableCrdtRegistryDto hashableCrdtRegistryDto)
            => new(hashableCrdtRegistryDto);

        [ProtoContract]
        public sealed class HashableCrdtRegistryDto
        {
            [ProtoMember(1)]
            public OptimizedObservedRemoveSet<TActorId, TItemKey>.OptimizedObservedRemoveSetDto Keys { get; set; }

            [ProtoMember(2)]
            public Dictionary<TItemKey, TItemValueDto> Dict { get; set; }
        }
    }
}