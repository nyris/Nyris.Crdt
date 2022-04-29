using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Contracts.Exceptions;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.Distributed.Test.Unit.Helpers
{
    public class OldMergeManagedOptimizedObservedRemoveSet<TActorId, TItem, TDto> : ManagedCRDT<TDto>
        where TItem : IEquatable<TItem>, IHashable
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>, IHashable
        where TDto : OldMergeManagedOptimizedObservedRemoveSet<TActorId, TItem, TDto>.OrSetDto, new()
    {
        private HashSet<DottedItem<TActorId, TItem>> _items;
        private readonly Dictionary<TActorId, uint> _versionVectors;
        private readonly SemaphoreSlim _semaphore = new(1);

        protected OldMergeManagedOptimizedObservedRemoveSet(InstanceId id,
            IAsyncQueueProvider? queueProvider = null,
            ILogger? logger = null) : base(id, queueProvider: queueProvider, logger: logger)
        {
            _items = new();
            _versionVectors = new();
        }

        /// <inheritdoc />
        public override ReadOnlySpan<byte> CalculateHash()
        {
            if (!_semaphore.Wait(TimeSpan.FromSeconds(15)))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                return HashingHelper.Combine(
                    HashingHelper.Combine(_items.OrderBy(i => i.Dot.Actor)),
                    HashingHelper.Combine(_versionVectors.OrderBy(pair => pair.Key)));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(TDto other, CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(other.VersionVectors, null) || other.VersionVectors.Count == 0)
                return MergeResult.NotUpdated;
            other.Items ??= new HashSet<DottedItem<TActorId, TItem>>();

            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                // variables names a taken from the paper, they do not have obvious meaning by themselves
                var m = _items.Intersect(other.Items).ToHashSet();

                // we need to check if received dto is identical to this instance in order to return correct merge result
                if (m.Count == _items.Count
                    && _items.OrderBy(item => item.Dot.Actor).SequenceEqual(other.Items.OrderBy(item => item.Dot.Actor))
                    && _versionVectors.OrderBy(pair => pair.Key)
                        .SequenceEqual(other.VersionVectors.OrderBy(pair => pair.Key)))
                {
                    return MergeResult.Identical;
                }

                var m1 = _items
                    .Except(other.Items)
                    .Where(i => !other.VersionVectors.TryGetValue(i.Dot.Actor, out var otherVersion)
                                || i.Dot.Version > otherVersion);

                var m2 = other.Items
                    .Except(_items)
                    .Where(i => !_versionVectors.TryGetValue(i.Dot.Actor, out var myVersion)
                                || i.Dot.Version > myVersion);

                var u = m.Union(m1).Union(m2);

                // TODO: maybe make it faster then O(n^2)?
                var o = _items
                    .Where(item => _items.Any(i => item.Value.Equals(i.Value) && item.Dot < i.Dot));

                _items = u.Except(o).ToHashSet();

                // observed state is a element-wise max of two vectors.
                foreach (var actorId in _versionVectors.Keys.ToList().Union(other.VersionVectors.Keys))
                {
                    _versionVectors.TryGetValue(actorId, out var thisVersion);
                    other.VersionVectors.TryGetValue(actorId, out var otherVersion);

                    _versionVectors[actorId] = Math.Max(thisVersion, otherVersion);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return MergeResult.ConflictSolved;
        }

        public override async Task<TDto> ToDtoAsync(CancellationToken cancellationToken = default)
        {
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                return new TDto
                {
                    Items = _items
                        .Select(i => new DottedItem<TActorId, TItem>(i.Dot, i.Value))
                        .ToHashSet(),
                    VersionVectors = _versionVectors
                        .ToDictionary(pair => pair.Key, pair => pair.Value)
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<TDto> EnumerateDtoBatchesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return
                await ToDtoAsync(cancellationToken); // unfortunately making ORSet a delta Crdt is not an easy task
        }

        public HashSet<TItem> Value
        {
            get
            {
                if (!_semaphore.Wait(TimeSpan.FromSeconds(15)))
                {
                    throw new NyrisException("Deadlock");
                }

                try
                {
                    return _items.Select(i => i.Value).ToHashSet();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        public virtual async Task AddAsync(TItem item, TActorId actorPerformingAddition)
        {
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15)))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                _versionVectors.TryGetValue(actorPerformingAddition, out var observedVersion);

                observedVersion += 1;

                _items.Add(new DottedItem<TActorId, TItem>(new Dot<TActorId>(actorPerformingAddition, observedVersion), item));

                // notice that i.Actor.Equals(actorPerformingAddition) means that there may be multiple copies of item
                // stored at the same time. This is by design
                _items.RemoveWhere(i => i.Value.Equals(item) && i.Dot.Version < observedVersion);
                _versionVectors[actorPerformingAddition] = observedVersion;
            }
            finally
            {
                _semaphore.Release();
            }

            await StateChangedAsync();
        }

        public virtual Task RemoveAsync(TItem item) => RemoveAsync(i => i.Equals(item));

        public virtual async Task RemoveAsync(Func<TItem, bool> condition)
        {
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15)))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                _items.RemoveWhere(i => condition(i.Value));
            }
            finally
            {
                _semaphore.Release();
            }

            await StateChangedAsync();
        }

        public abstract class OrSetDto
        {
            public abstract HashSet<DottedItem<TActorId, TItem>>? Items { get; set; }
            public abstract Dictionary<TActorId, uint>? VersionVectors { get; set; }
        }
    }
}
