using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Contracts.Exceptions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Model;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    public abstract class ManagedLastWriteWinsDeltaRegistry<TKey, TValue, TTimeStamp>
        : ManagedCrdtRegistryBase<TKey, TValue,
            ManagedLastWriteWinsDeltaRegistry<TKey, TValue, TTimeStamp>.LastWriteWinsDto>
        where TValue : IHashable
        where TKey : IEquatable<TKey>, IComparable<TKey>
        where TTimeStamp : IComparable<TTimeStamp>, IEquatable<TTimeStamp>
    {
        private readonly ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> _items;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private ConcurrentBag<TKey> _nextDto = new();

        protected ManagedLastWriteWinsDeltaRegistry(InstanceId id,
            IAsyncQueueProvider? queueProvider = null,
            ILogger? logger = null) : base(id, queueProvider: queueProvider, logger: logger)
        {
            _items = new ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>();
        }

        public IReadOnlyDictionary<TKey, TValue> Value =>
            _items.Where(pair => !pair.Value.Deleted)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Value);

        public IEnumerable<TValue> Values => _items.Values
            .Where(v => !v.Deleted)
            .Select(v => v.Value);

        public bool IsEmpty => _items.IsEmpty;

        /// <inheritdoc />
        public override ulong Size => (ulong) Values.Count();

        /// <inheritdoc />
        public override ulong StorageSize => (ulong) _items.Count;

        public bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value)
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

        public async Task<TimeStampedItem<TValue, TTimeStamp>> SetAsync(TKey key,
            TValue value,
            TTimeStamp timeStamp,
            uint propagateToNodes = 0,
            string? traceId = null,
            CancellationToken cancellationToken = default)
        {
            var item = _items.AddOrUpdate(key,
                _ => new TimeStampedItem<TValue, TTimeStamp>(value, timeStamp, false),
                (__, v) =>
                {
                    // NOTE: If existing value was old then update
                    if (v.TimeStamp.CompareTo(timeStamp) < 0)
                    {
                        _ = RemoveItemFromIndexes(key, v.Value, cancellationToken);
                        v.Value = value;
                        v.Deleted = false;
                        v.TimeStamp = timeStamp;
                    }

                    return v;
                });

            if (item.TimeStamp.CompareTo(timeStamp) != 0) return item;

            // Updated / Concurrent / new TimeStampedItem
            await AddItemToIndexesAsync(key, item.Value, cancellationToken: cancellationToken);
            _nextDto.Add(key);
            await StateChangedAsync(propagateToNodes: propagateToNodes, traceId: traceId,
                cancellationToken: cancellationToken);
            return item;
        }

        public async Task<TimeStampedItem<TValue, TTimeStamp>> RemoveAsync(TKey key, TTimeStamp timeStamp,
            uint propagateToNodes = 0,
            string? traceId = null,
            CancellationToken cancellationToken = default)
        {
            // Due to nature of the set, we have to record delete attempts even if such item was not found.
            // Just imagine if item was created and then deleted, but updates were reordered.
            var item = _items.AddOrUpdate(key,
                _ => new TimeStampedItem<TValue, TTimeStamp>(default!, timeStamp, true),
                (_, v) =>
                {
                    if (v.TimeStamp.CompareTo(timeStamp) >= 0) return v;

                    v.Deleted = true;
                    v.TimeStamp = timeStamp;
                    return v;
                });

            if (item.TimeStamp.CompareTo(timeStamp) != 0) return item;

            await RemoveItemFromIndexes(key, item.Value, cancellationToken);
            _nextDto.Add(key);
            await StateChangedAsync(propagateToNodes: propagateToNodes, traceId: traceId,
                cancellationToken: cancellationToken);
            return item;
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(LastWriteWinsDto other,
            CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(other.Items, null) || other.Items.Count == 0) return MergeResult.NotUpdated;

            var conflictSolved = false;
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                foreach (var key in other.Items.Keys)
                {
                    conflictSolved |= await CheckKeyForConflictAsync(key, other);
                }

                return conflictSolved ? MergeResult.ConflictSolved : MergeResult.NotUpdated;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <param name="cancellationToken"></param>
        /// <inheritdoc />
        public override async Task<LastWriteWinsDto> ToDtoAsync(CancellationToken cancellationToken = default)
        {
            if (_nextDto.Count == 0) return new LastWriteWinsDto();

            var keys = Interlocked.Exchange(ref _nextDto, new ConcurrentBag<TKey>());
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
        public override async IAsyncEnumerable<LastWriteWinsDto> EnumerateDtoBatchesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            const int maxBatchSize = 10;
            foreach (var batch in _items.Batch(maxBatchSize))
            {
                var items = new Dictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>(batch.Length);
                for (var i = 0; i < batch.Length; ++i)
                {
                    var (key, item) = batch.Span[i];
                    items.Add(key, item);
                }

                yield return new LastWriteWinsDto {Items = items};
            }
        }

        /// <inheritdoc />
        public override ReadOnlySpan<byte> CalculateHash()
            => HashingHelper.Combine(_items.OrderBy(i => i.Key));

        /// <inheritdoc />
        public override async IAsyncEnumerable<KeyValuePair<TKey, TValue>> EnumerateItems(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var (key, value) in _items.Where(i => !i.Value.Deleted))
            {
                yield return new KeyValuePair<TKey, TValue>(key, value.Value);
            }
        }

        private async Task<bool> CheckKeyForConflictAsync(TKey key, LastWriteWinsDto other)
        {
            var otherItem = default(TimeStampedItem<TValue, TTimeStamp>);
            var iHave = _items.TryGetValue(key, out var myItem);
            var otherHas = other.Items?.TryGetValue(key, out otherItem) ?? false;

            // var conflictSolved = iHave != otherHas // or if current key is missing from one of the registries
            //                  || myItem!.TimeStamp!.CompareTo(otherItem!.TimeStamp) != 0;
            // notes on last condition: notice that iHave and otherHas can have only 3 possibilities:
            // true-false, false-true and true-true. false-false is not possible, since in that case we would not
            // have this key to begin with. And true-false, false-true both satisfy iHave != otherHas
            // and execution would have stopped there. Only when both items were retrieved successfully, will
            // we evaluate the last condition.

            // case when 'this' registry have newer version of item or other does not have an item at all
            // no need to update anything
            if (iHave && otherHas && myItem!.TimeStamp!.CompareTo(otherItem!.TimeStamp) >= 0 || !otherHas)
            {
                return false;
            }

            // reverse - update 'this' registry
            if (iHave && myItem!.TimeStamp!.CompareTo(otherItem!.TimeStamp) < 0 || !iHave)
            {
                if (iHave) await RemoveItemFromIndexes(key, myItem!.Value);

                _nextDto.Add(key);
                _items.AddOrUpdate(key,
                    _ => otherItem!,
                    (_, _) => otherItem!);
                await AddItemToIndexesAsync(key, otherItem!.Value);
            }

            return true;
        }

        [ProtoContract]
        public sealed class LastWriteWinsDto
        {
            [ProtoMember(1)]
            public Dictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>? Items { get; set; }
        }
    }
}
