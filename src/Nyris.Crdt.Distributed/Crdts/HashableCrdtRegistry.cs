using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Sets;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts
{
    public sealed class HashableCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory>
        : ICRDT<HashableCrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory>.HashableCrdtRegistryDto>,
            IHashable
        where TItemKey : IEquatable<TItemKey>, IComparable<TItemKey>, IHashable
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>, IHashable
        where TItemValue : class, ICRDT<TItemValueDto>, IHashable
        where TItemValueFactory : ICRDTFactory<TItemValue, TItemValueDto>, new()
    {
        private static readonly TItemValueFactory Factory = new();

        private readonly HashableOptimizedObservedRemoveSet<TActorId, TItemKey> _keys;
        private readonly ConcurrentDictionary<TItemKey, TItemValue> _dictionary;
        private readonly object _mergeLock = new();

        public HashableCrdtRegistry()
        {
            _keys = new HashableOptimizedObservedRemoveSet<TActorId, TItemKey>();
            _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>();
        }

        public IReadOnlyDictionary<TItemKey, T> Value<T>(Func<TItemValue, T> func)
        {
            lock (_mergeLock)
            {
                return _dictionary.ToDictionary(pair => pair.Key, pair => func(pair.Value));
            }
        }

        public HashSet<TItemKey> Keys => _keys.Value;

        // ReSharper disable once InconsistentlySynchronizedField
        public TItemValue this[TItemKey key] => _dictionary[key];

        public bool TryGetValue(in TItemKey key, [NotNullWhen(true)] out TItemValue? value)
            // ReSharper disable once InconsistentlySynchronizedField
            => _dictionary.TryGetValue(key, out value);

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash()
        {
            lock (_mergeLock)
            {
                return HashingHelper.Combine(_keys.CalculateHash(),
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

        public void Remove(TItemKey key)
        {
            lock (_mergeLock)
            {
                _keys.Remove(key);
                _dictionary.Remove(key, out _);
            }
        }

        /// <inheritdoc />
        public MergeResult Merge(HashableCrdtRegistryDto other)
        {
            lock (_mergeLock)
            {
                var keyResult = _keys.MaybeMerge(other.Keys);

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
                    var otherValue = default(TItemValueDto);
                    var iHave = _dictionary.TryGetValue(key, out var myValue);
                    var otherHas = other.Dict?.TryGetValue(key, out otherValue) ?? false;

                    if (iHave && otherHas)
                    {
                        var valueResult = myValue!.Merge(otherValue!);
                        conflict = conflict || valueResult == MergeResult.ConflictSolved;
                    }
                    else if (otherHas)
                    {
                        _dictionary[key] = Factory.Create(otherValue!);
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

        [ProtoContract]
        public sealed class HashableCrdtRegistryDto
        {
            [ProtoMember(1)]
            public OptimizedObservedRemoveSet<TActorId, TItemKey>.OptimizedObservedRemoveSetDto? Keys { get; set; }

            [ProtoMember(2)]
            public Dictionary<TItemKey, TItemValueDto>? Dict { get; set; }
        }
    }
}