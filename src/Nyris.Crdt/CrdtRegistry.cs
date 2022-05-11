using Nyris.Crdt.Sets;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Nyris.Crdt;

/// <summary>
/// An immutable key-value registry based on <see cref="OptimizedObservedRemoveSet{TActorId,TItem}"/>,
/// that is a CRDT.
/// </summary>
/// <typeparam name="TActorId"></typeparam>
/// <typeparam name="TItemKey"></typeparam>
/// <typeparam name="TItemValue"></typeparam>
/// <typeparam name="TItemValueDto"></typeparam>
/// <typeparam name="TItemValueFactory"></typeparam>
public class CrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory>
    : ICRDT<CrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueFactory>.CrdtRegistryDto>
    where TItemKey : IEquatable<TItemKey>
    where TActorId : IEquatable<TActorId>
    where TItemValue : class, ICRDT<TItemValueDto>
    where TItemValueFactory : ICRDTFactory<TItemValue, TItemValueDto>, new()
{
    private static readonly TItemValueFactory Factory = new();

    private readonly OptimizedObservedRemoveSet<TActorId, TItemKey> _keys;
    private readonly ConcurrentDictionary<TItemKey, TItemValue> _dictionary;
    private readonly object _mergeLock = new();

    public Dictionary<TItemKey, T> Value<T>(Func<TItemValue, T> expression)
    {
        lock (_mergeLock)
        {
            return _dictionary.ToDictionary(pair => pair.Key, pair => expression(pair.Value));
        }
    }

    public CrdtRegistry()
    {
        _keys = new OptimizedObservedRemoveSet<TActorId, TItemKey>();
        _dictionary = new ConcurrentDictionary<TItemKey, TItemValue>();
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

    /// <inheritdoc />
    public MergeResult Merge(CrdtRegistryDto other)
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

    [ProtoContract]
    public sealed class CrdtRegistryDto
    {
        [ProtoMember(1)]
        public OptimizedObservedRemoveSet<TActorId, TItemKey>.OptimizedObservedRemoveSetDto? Keys { get; set; }

        [ProtoMember(2)]
        public Dictionary<TItemKey, TItemValueDto>? Dict { get; set; }
    }
}
