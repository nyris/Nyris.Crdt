using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Extensions;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts
{
    public abstract class ManagedLastWriteWinsDeltaRegistry<TKey, TValue, TTimeStamp>
        : ManagedCRDT<
            ManagedLastWriteWinsDeltaRegistry<TKey, TValue, TTimeStamp>,
            Dictionary<TKey, TValue>,
            ManagedLastWriteWinsDeltaRegistry<TKey, TValue, TTimeStamp>.LastWriteWinsDto>
        where TKey : IEquatable<TKey>
        where TTimeStamp : IComparable<TTimeStamp>
    {
        private readonly ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> _items;
        private readonly SemaphoreSlim _semaphore = new(1);
        private List<TKey> _nextDto = new();

        protected ManagedLastWriteWinsDeltaRegistry(string id) : base(id)
        {
            _items = new ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>();
        }

        protected ManagedLastWriteWinsDeltaRegistry(ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> items) : base("")
        {
            _items = items;
        }

        /// <inheritdoc />
        public override Dictionary<TKey, TValue> Value =>
            _items.Where(pair => !pair.Value.Deleted)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Value);

        public IEnumerable<TValue> Values => _items.Values
            .Where(v => !v.Deleted)
            .Select(v => v.Value);

        public bool IsEmpty => _items.IsEmpty;

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            // ReSharper disable once InconsistentlySynchronizedField - updates on individual items are atomic
            var found = _items.TryGetValue(key, out var timeStampedItem);

            if (!found || timeStampedItem!.Deleted)
            {
                value = default;
                return false;
            }

            value = timeStampedItem.Value;
            return true;
        }

        /// <summary>
        /// Adds item into the registry. "out item" param always contains an item. If return value is true,
        /// item will contain item which was set. If false, it will contain item with the same key and later timestamp
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeStamp"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool TrySet(TKey key, TValue value, TTimeStamp timeStamp, out TimeStampedItem<TValue, TTimeStamp> item)
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

            if (item.TimeStamp.CompareTo(timeStamp) != 0) return false;

            _nextDto.Add(key);
            _ = StateChangedAsync();
            return true;
        }

        public bool TryRemove(TKey key, TTimeStamp timeStamp, out TimeStampedItem<TValue, TTimeStamp> item)
        {
            // Due to nature of the set, we have to record delete attempts even if such item was not found.
            // Just imagine if item was created and then deleted, but updates were reordered.
            item = _items.AddOrUpdate(key,
                _ => new TimeStampedItem<TValue, TTimeStamp>(default!, timeStamp, true),
                (_, v) =>
                {
                    if (v.TimeStamp.CompareTo(timeStamp) >= 0) return v;

                    v.Deleted = true;
                    v.TimeStamp = timeStamp;
                    return v;
                });

            _nextDto.Add(key);
            _ = StateChangedAsync();
            return item.TimeStamp.CompareTo(timeStamp) == 0;
        }

        public override async Task<MergeResult> MergeAsync(ManagedLastWriteWinsDeltaRegistry<TKey, TValue, TTimeStamp> other)
        {
            var conflictSolved = false;
            await _semaphore.WaitAsync();
            try
            {
                foreach (var key in _items.Keys.Union(other._items.Keys))
                {
                    CheckKeyForConflict(key, other, ref conflictSolved);
                }

                return conflictSolved ? MergeResult.ConflictSolved : MergeResult.Identical;
            }
            finally
            {
                _semaphore.Release();
                if(conflictSolved) await StateChangedAsync();
            }
        }

        /// <inheritdoc />
        public override async Task<LastWriteWinsDto> ToDtoAsync()
        {
            var keys = Interlocked.Exchange(ref _nextDto, new List<TKey>());
            var items = new Dictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>(keys.Count);

            foreach (var key in keys)
            {
                if (_items.TryGetValue(key, out var item))
                {
                    items.Add(key, item);
                }
            }

            return new LastWriteWinsDto
            {
                Items = items
            };
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<LastWriteWinsDto> EnumerateDtoBatchesAsync()
        {
            const int maxBatchSize = 1000;
            foreach (var batch in _items.Batch(maxBatchSize))
            {
                yield return new LastWriteWinsDto
                {
                    Items = new Dictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>(batch)
                };
            }
        }

        /// <inheritdoc />
        public override string TypeName { get; } = nameof(ManagedLastWriteWinsDeltaRegistry<TKey, TValue, TTimeStamp>);

        /// <inheritdoc />
        public override async Task<string> GetHashAsync()
            => _items
                .OrderBy(i => i.Key)
                .Aggregate(0, HashCode.Combine)
                .ToString();

        private void CheckKeyForConflict(TKey key, ManagedLastWriteWinsDeltaRegistry<TKey, TValue, TTimeStamp> other, ref bool conflictSolved)
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
            if (iHave && otherHas && myItem!.TimeStamp.CompareTo(otherItem!.TimeStamp) >= 0 || !otherHas)
            {
                return;
            }

            // reverse - update 'this' registry
            if (iHave && myItem!.TimeStamp.CompareTo(otherItem!.TimeStamp) < 0 || !iHave)
            {
                _nextDto.Add(key);
                _items.AddOrUpdate(key,
                    _ => otherItem!,
                    (_, __) => otherItem!);
            }
        }

        [ProtoContract]
        public sealed class LastWriteWinsDto
        {
            [ProtoMember(1)]
            public Dictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> Items { get; set; }
        }
    }
}