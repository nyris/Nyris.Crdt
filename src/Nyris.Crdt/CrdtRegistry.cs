using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nyris.Crdt.Sets;
using ProtoBuf;

namespace Nyris.Crdt
{
    /// <summary>
    /// An immutable key-value registry based on <see cref="OptimizedObservedRemoveSet{TActorId,TItem}"/>,
    /// that is a CRDT.
    /// </summary>
    /// <typeparam name="TActorId"></typeparam>
    /// <typeparam name="TItemKey"></typeparam>
    /// <typeparam name="TItemValue"></typeparam>
    /// <typeparam name="TItemValueDto"></typeparam>
    /// <typeparam name="TItemValueDtoFactory"></typeparam>
    /// <typeparam name="TItemValueRepresentation"></typeparam>
    public sealed class CrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueDtoFactory, TItemValueRepresentation>
        : ICRDT<
            CrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueDtoFactory, TItemValueRepresentation>,
            Dictionary<TItemKey, TItemValueRepresentation>,
            CrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueDtoFactory, TItemValueRepresentation>.CrdtRegistryDto>
        where TItemKey : IEquatable<TItemKey>
        where TActorId : IEquatable<TActorId>
        where TItemValue : class, ICRDT<TItemValue, TItemValueRepresentation, TItemValueDto>
        where TItemValueDtoFactory : ICRDTFactory<TItemValue, TItemValueRepresentation, TItemValueDto>, new()
    {
        private static readonly TItemValueDtoFactory Factory = new();

        private readonly OptimizedObservedRemoveSet<TActorId, TItemKey> _keys;
        private readonly ConcurrentDictionary<TItemKey, TItemValue> _dictionary;
        private readonly object _mergeLock = new();

        /// <inheritdoc />
        public Dictionary<TItemKey, TItemValueRepresentation> Value
        {
            get
            {
                lock (_mergeLock)
                {
                    return _dictionary.ToDictionary(pair => pair.Key, pair => pair.Value.Value);
                }
            }
        }

        public CrdtRegistry()
        {
            _keys = new OptimizedObservedRemoveSet<TActorId, TItemKey>();
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>();
        }

        private CrdtRegistry(CrdtRegistryDto crdtRegistryDto)
        {
            _keys = OptimizedObservedRemoveSet<TActorId, TItemKey>.FromDto(crdtRegistryDto.Keys);
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>(crdtRegistryDto.Dict?.ToDictionary(pair => pair.Key, pair => Factory.Create(pair.Value))
                          ?? new Dictionary<TItemKey, TItemValue>());
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

        public MergeResult Merge(CrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueDtoFactory, TItemValueRepresentation> other)
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

        public CrdtRegistryDto ToDto()
        {
            lock (_mergeLock)
            {
                return new CrdtRegistryDto
                {
                    Keys = _keys.ToDto(),
                    Dict = _dictionary.ToDictionary(pair => pair.Key, pair => pair.Value.ToDto())
                };
            }
        }

        public static CrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueDtoFactory, TItemValueRepresentation> FromDto(CrdtRegistryDto crdtRegistryDto)
            => new(crdtRegistryDto);

        [ProtoContract]
        public sealed class CrdtRegistryDto
        {
            [ProtoMember(1)]
            public OptimizedObservedRemoveSet<TActorId, TItemKey>.OptimizedObservedRemoveSetDto Keys { get; set; }

            [ProtoMember(2)]
            public Dictionary<TItemKey, TItemValueDto> Dict { get; set; }
        }
    }
}
