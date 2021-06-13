using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;

namespace Nyris.Crdt
{
    public class LastWriteWinsRegistry<TKey, TValue, TTimeStamp>
        : ICRDT<
            LastWriteWinsRegistry<TKey, TValue, TTimeStamp>,
            Dictionary<TKey, TValue>>
        where TKey : IEquatable<TKey>
        where TTimeStamp : IComparable<TTimeStamp>
    {
        private readonly ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> _items;
        private readonly object _mergeLock = new();

        public LastWriteWinsRegistry()
        {
            _items = new ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>();
        }

        private LastWriteWinsRegistry(ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> items)
        {
            _items = items;
        }

        /// <inheritdoc />
        public Dictionary<TKey, TValue> Value {
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
            var found = _items.TryGetValue(key, out var timeStampedItem);

            if (!found || timeStampedItem.Deleted)
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

        public MergeResult MergeAsync(LastWriteWinsRegistry<TKey, TValue, TTimeStamp> other)
        {
            lock (_mergeLock)
            {
                var conflictSolved = false;
                foreach (var key in _items.Keys.Union(other._items.Keys))
                {
                    var iHave = _items.TryGetValue(key, out var myItem);
                    var otherHas = _items.TryGetValue(key, out var otherItem);

                    if (!conflictSolved || iHave != otherHas || myItem.TimeStamp.CompareTo(otherItem.TimeStamp) != 0)
                    {
                        conflictSolved = true;
                    }

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

        public string Serialize()
        {
            lock (_mergeLock)
            {
                return JsonConvert.SerializeObject(_items);
            }
        }

        public static LastWriteWinsRegistry<TKey, TValue, TTimeStamp> Deserialize(string data)
        {
            var items = JsonConvert.DeserializeObject<ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>>(data);
            return new LastWriteWinsRegistry<TKey, TValue, TTimeStamp>(items);
        }

        public LastWriteWinsRegistry<TKey, TValue, TTimeStamp> Copy(IEnumerable<TKey> keys)
        {
            var resultingItems = new ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>();

            lock (_mergeLock)
            {
                foreach (var key in keys)
                {
                    _items.TryGetValue(key, out var value);
                    resultingItems.TryAdd(key, value);
                }
            }

            return new LastWriteWinsRegistry<TKey, TValue, TTimeStamp>(resultingItems);
        }

        /// <summary>
        /// Removes items from registry entirely.
        /// Warning - this is NOT a CRDT compliant operation.
        /// </summary>
        /// <param name="data"></param>
        public void Purge(LastWriteWinsRegistry<TKey, TValue, TTimeStamp> data)
        {
            lock (_mergeLock)
            {
                foreach (var pair in data._items)
                {
                    ((ICollection<KeyValuePair<TKey, TimeStampedItem<TValue, TTimeStamp>>>) _items).Remove(pair);
                }
            }
        }
    }
}