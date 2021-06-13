using System;
using System.Collections.Generic;
using System.Linq;
using Nyris.Crdt.Sets;

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
            CrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueDtoFactory, TItemValueRepresentation>.Dto>
        where TItemKey : IEquatable<TItemKey>
        where TActorId : IEquatable<TActorId>
        where TItemValue : class, ICRDT<TItemValue, TItemValueRepresentation, TItemValueDto>
        where TItemValueDtoFactory : ICRDTFactory<TItemValue, TItemValueRepresentation, TItemValueDto>, new()
    {
        private static readonly TItemValueDtoFactory Factory = new();

        private readonly OptimizedObservedRemoveSet<TActorId, TItemKey> _keys;
        private readonly Dictionary<TItemKey, TItemValue> _dictionary;
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
            _dictionary = new Dictionary<TItemKey, TItemValue>();
        }

        private CrdtRegistry(Dto dto)
        {
            _keys = OptimizedObservedRemoveSet<TActorId, TItemKey>.FromDto(dto.Keys);
            _dictionary = dto.Dict.ToDictionary(pair => pair.Key, pair => Factory.Create(pair.Value));
        }

        public TItemValue GetOrCreate(TItemKey key, Func<(TActorId, TItemValue)> createFunc)
        {
            lock (_mergeLock)
            {
                if (_dictionary.TryGetValue(key, out var value)) return value;

                var (actorId, newValue) = createFunc();

                _keys.Add(key, actorId);
                _dictionary.Add(key, newValue);

                return newValue;
            }
        }

        public void Remove(TItemKey key)
        {
            lock (_mergeLock)
            {
                _keys.Remove(key);
                _dictionary.Remove(key);
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
                        _dictionary.Remove(keyToDrop);
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
                        var valueResult = myValue.Merge(otherValue);
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

        public Dto ToDto()
        {
            lock (_mergeLock)
            {
                return new Dto
                {
                    Keys = _keys.ToDto(),
                    Dict = _dictionary.ToDictionary(pair => pair.Key, pair => pair.Value.ToDto())
                };
            }
        }

        public static CrdtRegistry<TActorId, TItemKey, TItemValue, TItemValueDto, TItemValueDtoFactory, TItemValueRepresentation> FromDto(Dto dto)
            => new(dto);

        public sealed class Dto
        {
            public OptimizedObservedRemoveSet<TActorId, TItemKey>.Dto Keys { get; set; }

            public Dictionary<TItemKey, TItemValueDto> Dict { get; set; }
        }
    }
}
