using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts.Operations;
using Nyris.Crdt.Distributed.Crdts.Operations.Responses;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Extensions;
using Nyris.Crdt.Distributed.Utils;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    public abstract class ManagedLastWriteWinsDeltaRegistryWithSerializableOperations<TImplementation, TKey, TValue, TTimeStamp>
        : ManagedCRDTWithSerializableOperations<TImplementation,
            IReadOnlyDictionary<TKey, TValue>,
            ManagedLastWriteWinsDeltaRegistryWithSerializableOperations<TImplementation, TKey, TValue, TTimeStamp>.LWWDto,
            RegistryOperation,
            RegistryOperationResponse>
        where TImplementation : ManagedLastWriteWinsDeltaRegistryWithSerializableOperations<TImplementation, TKey, TValue, TTimeStamp>
        where TValue : IHashable
        where TKey : IEquatable<TKey>
        where TTimeStamp : IComparable<TTimeStamp>, IEquatable<TTimeStamp>
    {
        private readonly ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> _items;
        private readonly SemaphoreSlim _semaphore = new(1);
        private List<TKey> _nextDto = new();

        /// <inheritdoc />
        protected ManagedLastWriteWinsDeltaRegistryWithSerializableOperations(string instanceId) : base(instanceId)
        {
            _items = new ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>();
        }

        protected ManagedLastWriteWinsDeltaRegistryWithSerializableOperations(LWWDto dto, string instanceId) : base(instanceId)
        {
            _items = new ConcurrentDictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>(dto?.Items
                ?? new Dictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>());
        }

        /// <inheritdoc />
        public override IReadOnlyDictionary<TKey, TValue> Value =>
            _items.Where(pair => !pair.Value.Deleted)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Value);

        /// <inheritdoc />
        public override ulong Size => (ulong) _items.Count;

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

        /// <inheritdoc />
        public override async Task<RegistryOperationResponse> ApplyAsync(RegistryOperation operation)
        {
            switch (operation)
            {
                case GetValueOperation<TKey> getValueOperation:
                    if (!TryGetValue(getValueOperation.Key, out var value)) throw new KeyNotFoundException();
                    return new ValueResponse<TValue>(value);
                case AddValueOperation<TKey, TValue, TTimeStamp>(var key, var newValue, var timeStamp):
                    if (!TrySet(key, newValue, timeStamp, out var added))
                    {
                        throw new ApplicationException();
                    }

                    return new ValueResponse<TValue>(added.Value);
                default:
                    throw new GeneratedCodeExpectationsViolatedException(
                        $"Apply async received operation of unexpected type: {operation.GetType()}");
            }
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

        public override async Task<MergeResult> MergeAsync(TImplementation other, CancellationToken cancellationToken = default)
        {
            if (other._items.IsEmpty) return MergeResult.NotUpdated;

            var conflictSolved = false;
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                foreach (var key in other._items.Keys)
                {
                    CheckKeyForConflict(key, other, ref conflictSolved);
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
        public override async Task<LWWDto> ToDtoAsync(CancellationToken cancellationToken = default)
        {
            if (_nextDto.Count == 0) return new();

            var keys = Interlocked.Exchange(ref _nextDto, new List<TKey>());
            var items = new Dictionary<TKey, TimeStampedItem<TValue, TTimeStamp>>(keys.Count);

            foreach (var key in keys)
            {
                if (_items.TryGetValue(key, out var item))
                {
                    items.Add(key, item);
                }
            }

            return new LWWDto
            {
                Items = items
            };
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<LWWDto> EnumerateDtoBatchesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
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
                yield return new LWWDto { Items = items };
            }
        }

        /// <inheritdoc />
        public override ReadOnlySpan<byte> CalculateHash()
            => HashingHelper.Combine(_items.OrderBy(i => i.Value.TimeStamp));

        private void CheckKeyForConflict(TKey key, TImplementation other, ref bool conflictSolved)
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
        public sealed class LWWDto
        {
            [ProtoMember(1)]
            public Dictionary<TKey, TimeStampedItem<TValue, TTimeStamp>> Items { get; set; } = new();
        }
    }
}