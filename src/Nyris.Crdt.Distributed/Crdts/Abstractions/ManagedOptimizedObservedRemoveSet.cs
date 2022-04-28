using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nyris.Contracts.Exceptions;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    /// <summary>
    /// Optimized Observed-Remove Set is a CRDT proposed by Annette Bieniusa & Co: https://softech.cs.uni-kl.de/homepage/staff/AnnetteBieniusa/paper/techrep2012-semantics.pdf
    /// It allows set of actors to add and remove elements unlimited number of times.
    /// Contrary to original Observed-Remove Set, it has an upper bound on memory usage.
    /// It is O(E*n + n), where E is the number of elements and n is the number of actors.
    /// </summary>
    [DebuggerDisplay("{_items.Count < 10 ? string.Join(';', _items) : \"... a lot of items ...\"}")]
    public abstract class ManagedOptimizedObservedRemoveSet<TActorId, TItem, TDto>
        : ManagedCRDT<TDto>
        where TItem : IEquatable<TItem>, IHashable
        where TActorId : IEquatable<TActorId>, IComparable<TActorId>, IHashable
        where TDto : ManagedOptimizedObservedRemoveSet<TActorId, TItem, TDto>.OrSetDto, new()
    {
        private readonly HashSet<DottedItem<TActorId, TItem>> _items;
        private readonly HashSet<DottedItem<TActorId, TItem>> _deltas;
        private readonly Dictionary<TActorId, VersionVector<TActorId>> _versionVectors;
        private readonly HashSet<Tombstone<TActorId>> _tombstones;
        private readonly SemaphoreSlim _semaphore = new(1);

        protected ManagedOptimizedObservedRemoveSet(InstanceId id,
            IAsyncQueueProvider? queueProvider = null,
            ILogger? logger = null) : base(id, queueProvider: queueProvider, logger: logger)
        {
            _items = new();
            _versionVectors = new();
            _tombstones = new();
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
            if (other.VersionVectors is null || other.VersionVectors.Count == 0) return MergeResult.NotUpdated;

            var otherItems = other.Items is null
                ? new HashSet<DottedItem<TActorId, TItem>>()
                : new HashSet<DottedItem<TActorId, TItem>>(other.Items);

            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                // NOTE: Keeps items from Other Node if Local Node has never seen those (i.e No VersionVector present) or they have newer Dot than Local Observed State
                // var newerOtherItems = otherItems.Where(i =>
                //         !_versionVectors.TryGetValue(i.Dot.Actor, out var exitingVersion) || i.Dot > exitingVersion)
                //     .ToHashSet();

                // NOTE: Delta (i.e Dto) has never been seen (0) or has newer Operations (1)
                // 0. Other VersionVectors has a VersionVector that this Node hasn't even seen
                // 1.If Other VersionVectors has a VersionVectors that is higher than it's local VersionVectors, it means either It has new items or some item was deleted
                var dtoHasNewOperations =
                    other.VersionVectors.Any(pair =>
                        !_versionVectors.TryGetValue(pair.Key, out var localVersion) || pair.Value > localVersion);

                if (!dtoHasNewOperations)
                {
                    return MergeResult.Identical;
                }

                if (other.Tombstones is not null)
                {
                    // NOTE: Local Items are Deleted when it's Dot exists in other Tombstones
                    var deletedItems = _items.Where(i => other.Tombstones.Any(t => t.Dot == i.Dot)).ToHashSet();

                    _items.ExceptWith(deletedItems);
                }

                if (other.Items is not null)
                {
                    _items.UnionWith(other.Items);
                }

                // observed state is a element-wise max of two vectors.
                foreach (var (actorId, _) in _versionVectors.Union(other.VersionVectors))
                {
                    var defaultValue = new VersionVector<TActorId>(actorId, 0);
                    var thisObservedState = _versionVectors.GetValueOrDefault(actorId, defaultValue);
                    // TODO: otherObservedState is null Struct
                    var otherObservedState = other.VersionVectors.GetValueOrDefault(actorId, defaultValue);

                    _versionVectors[actorId] =
                        thisObservedState > otherObservedState ? thisObservedState : otherObservedState;
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
                    Items = _items.Count == 0
                        ? null
                        : _items
                            .Select(i => new DottedItem<TActorId, TItem>(i.Dot, i.Value))
                            .ToHashSet(),
                    VersionVectors = _versionVectors.Count == 0
                        ? null
                        : _versionVectors
                            .ToDictionary(pair => pair.Key, pair => pair.Value),
                    Tombstones = _tombstones.Select(i => new Tombstone<TActorId>(i.Dot))
                        .ToHashSet(),
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
                var prevVersion = _versionVectors.GetValueOrDefault(actorPerformingAddition,
                    new VersionVector<TActorId>(actorPerformingAddition, 0));

                var nextVersion = prevVersion.Next();

                _items.Add(new DottedItem<TActorId, TItem>(new Dot<TActorId>(nextVersion), item));

                // notice that i.Actor.Equals(actorPerformingAddition) means that there may be multiple copies of item
                // stored at the same time. This is by design
                _items.RemoveWhere(i => i.Value.Equals(item) && i.Dot < nextVersion);
                _versionVectors[actorPerformingAddition] = nextVersion;
            }
            finally
            {
                _semaphore.Release();
            }

            await StateChangedAsync();
        }

        public virtual Task RemoveAsync(TItem item, TActorId actorPerformingAddition) =>
            RemoveAsync(i => i.Equals(item), actorPerformingAddition);

        public virtual async Task RemoveAsync(Func<TItem, bool> condition, TActorId actorPerformingAddition)
        {
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15)))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                var itemsToBeRemoved = _items.Where(i => condition(i.Value)).ToHashSet();

                if (itemsToBeRemoved.Count > 0)
                {
                    _items.ExceptWith(itemsToBeRemoved);

                    _tombstones.UnionWith(itemsToBeRemoved.Select(i => new Tombstone<TActorId>(i.Dot)));
                }
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
            public abstract Dictionary<TActorId, VersionVector<TActorId>>? VersionVectors { get; set; }
            public abstract HashSet<Tombstone<TActorId>>? Tombstones { get; set; }
        }
    }
}
