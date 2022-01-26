using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ProtoBuf;

namespace Nyris.Crdt
{
    public class LastWriteWinsRegistry<TKey, TValue, TTimeStamp>
        : ICRDT<LastWriteWinsRegistry<TKey, TValue, TTimeStamp>,
            IReadOnlyDictionary<TKey, TValue>,
            LastWriteWinsRegistry<TKey, TValue, TTimeStamp>.LastWriteWinsRegistryDto>
        where TKey : IEquatable<TKey>
        where TTimeStamp : IComparable<TTimeStamp>, IEquatable<TTimeStamp>
    {
        protected readonly ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> _items;
        private readonly object _mergeLock = new();

        public LastWriteWinsRegistry()
        {
            _items = new ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>();
        }

        protected LastWriteWinsRegistry(LastWriteWinsRegistryDto dto)
        {
            _items = new ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>(dto.Items);
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<TKey, TValue> Value {
            get
            {
                lock (_mergeLock)
                {
                    return _items.Where(pair => !pair.Value.Deleted)
                        .ToDictionary(pair => pair.Key, pair => pair.Value.Value);
                }
            }
        }

        /// <summary>
        /// NOTE that this property, by design, does not account for removed items (it will return both deleted and present items)
        /// </summary>
        // ReSharper disable once InconsistentlySynchronizedField - Keys property should already be atomic
        public ICollection<TKey> Keys => _items.Keys;

        public IEnumerable<TValue> Values => _items.Values.Where(v => !v.Deleted).Select(v => v.Value);

        public bool IsEmpty => _items.IsEmpty;

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            // ReSharper disable once InconsistentlySynchronizedField - updates on individual items are atomic
            if (!_items.TryGetValue(key, out var timeStampedItem) || timeStampedItem.Deleted)
            {
                value = default;
                return false;
            }

            value = timeStampedItem.Value;
            return true;
        }

        public bool TrySet(TKey key, TValue value, TTimeStamp timeStamp, out TimeStampedItem<TValue, TTimeStamp> item)
        {
            lock (_mergeLock)
            {
                item = _items.AddOrUpdate(key,
                    _ => new TimeStampedItem<TValue, TTimeStamp>(value, timeStamp, false),
                    (_, v) =>
                    {
                        if (v.TimeStamp.CompareTo(timeStamp) >= 0) return v;

                        v.Value = value;
                        v.Deleted = false;
                        v.TimeStamp = timeStamp;
                        return v;
                    });

                return item.TimeStamp.CompareTo(timeStamp) == 0;
            }
        }

        public bool TryRemove(TKey key, TTimeStamp timeStamp, out TimeStampedItem<TValue, TTimeStamp> item)
        {
            lock (_mergeLock)
            {
                item = _items.AddOrUpdate(key,
                    _ => new TimeStampedItem<TValue, TTimeStamp>(default, timeStamp, false),
                    (_, v) =>
                    {
                        if (v.TimeStamp.CompareTo(timeStamp) >= 0) return v;

                        v.Deleted = true;
                        v.TimeStamp = timeStamp;
                        return v;
                    });

                return item.TimeStamp.CompareTo(timeStamp) == 0;
            }
        }

        public MergeResult Merge(LastWriteWinsRegistry<TKey, TValue, TTimeStamp> other)
        {
            lock (_mergeLock)
            {
                var conflictSolved = false;
                foreach (var key in _items.Keys.Union(other._items.Keys))
                {
                    var iHave = _items.TryGetValue(key, out var myItem);
                    var otherHas = other._items.TryGetValue(key, out var otherItem);

                    conflictSolved = conflictSolved  // if conflict was in the previous key, keep the true value immediately
                                     || iHave != otherHas // or if current key is missing from one of the registries
                                     || myItem!.TimeStamp.CompareTo(otherItem!.TimeStamp) != 0;
                    // notes on last condition: notice that iHave and otherHas can have only 3 possibilities:
                    // true-false, false-true and true-true. false-false is not possible, since in that case we would not
                    // have this key to begin with. And true-false, false-true both satisfy iHave != otherHas
                    // and execution would have stopped there. Only when both items were retrieved successfully, will
                    // we evaluate the last condition.

                    // case when 'this' registry have newer version of item or other does not have an item at all
                    // no need to update anything
                    if (iHave && otherHas && myItem.TimeStamp.CompareTo(otherItem.TimeStamp) >= 0 ||
                        !otherHas)
                    {
                        continue;
                    }

                    if(iHave && myItem.TimeStamp.CompareTo(otherItem.TimeStamp) < 0 ||
                       !iHave)
                    {
                        _items.AddOrUpdate(key,
                            _ => otherItem,
                            (_, __) => otherItem);
                    }
                }

                return conflictSolved ? MergeResult.ConflictSolved : MergeResult.Identical;
            }
        }

        /// <inheritdoc />
        public LastWriteWinsRegistryDto ToDto() =>
            new()
            {
                Items = _items.ToDictionary(pair => pair.Key, pair => pair.Value)
            };

        [ProtoContract]
        public sealed class LastWriteWinsRegistryDto
        {
            [ProtoMember(1)]
            public Dictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> Items { get; set; } = new();
        }
    }
}