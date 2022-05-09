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
        private HashSet<DottedItem<TActorId, TItem>> _delta;

        private readonly Dictionary<TActorId, uint> _versionVectors;

        // NOTE: Original Tombstones term represents the whole deleted Item {TItem} in original ORSet,
        // but this field only contains {Dot}s of deleted Items, this is also referred as "DotCloud" in https://bartoszsypytkowski.com/optimizing-state-based-crdts-part-2/
        private readonly Dictionary<Dot<TActorId>, HashSet<TActorId>> _tombstones;
        private readonly SemaphoreSlim _semaphore = new(1);
        private readonly TActorId _thisNodeId;

        protected ManagedOptimizedObservedRemoveSet(InstanceId id,
            TActorId thisNodeId,
            IAsyncQueueProvider? queueProvider = null,
            ILogger? logger = null) : base(id, queueProvider: queueProvider, logger: logger)
        {
            _thisNodeId = thisNodeId;
            _items = new();
            _delta = new();
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
            if ((other.SourceId is not null && _thisNodeId.Equals(other.SourceId)) ||
                (other.VersionVectors is null || other.VersionVectors.Count == 0)) return MergeResult.NotUpdated;

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
                var newerOtherItems = otherItems.Where(i =>
                        !(other.Tombstones is not null && other.Tombstones.ContainsKey(i.Dot)) &&
                        (!_versionVectors.TryGetValue(i.Dot.Actor, out var exitingVersion) || i.Dot.Version > exitingVersion))
                    .ToHashSet();

                var newerTombstones = other.Tombstones ?? new Dictionary<Dot<TActorId>, HashSet<TActorId>>();

                // NOTE: Delta is Identical when
                // 0. There are No new items
                // 1. There NO newer Tombstones, i.e No Items were deleted or No Items were deleted that this Node hasn't seen
                if (newerOtherItems.Count == 0 && newerTombstones.Count == 0)
                {
                    return MergeResult.Identical;
                }

                if (newerTombstones.Count != 0)
                {
                    // NOTE: Local Items are Deleted when it's Dot exists in other Tombstones
                    var deletedItems = _items.Where(i => newerTombstones.TryGetValue(i.Dot, out _)).ToHashSet();

                    _items.ExceptWith(deletedItems);
                    newerOtherItems.ExceptWith(deletedItems);
                }

                _items.UnionWith(newerOtherItems);
                _delta = _items.ToHashSet();

                // observed state is a element-wise max of two vectors.
                foreach (var (actorId, otherVersion) in other.VersionVectors)
                {
                    if (_versionVectors.TryGetValue(actorId, out var existingVersion))
                    {
                        _versionVectors[actorId] = Math.Max(otherVersion, existingVersion);
                    }
                    else
                    {
                        _versionVectors.Add(actorId, otherVersion);
                    }
                }

                var allKnownNodes = _versionVectors.Select(pair => pair.Key).OrderBy(id => id).ToHashSet();

                foreach (var (dot, actorIds) in newerTombstones)
                {
                    // NOTE: Update existing tombstone for given Dot (i.e pair.Key)
                    if (_tombstones.TryGetValue(dot, out var observedByActors))
                    {
                        observedByActors.UnionWith(actorIds);
                    }
                    else
                    {
                        observedByActors = actorIds;
                        _tombstones.Add(dot, observedByActors);
                    }

                    // NOTE: Delete Tombstones which every known Node has already seen.
                    if (allKnownNodes.SetEquals(observedByActors.OrderBy(id => id)))
                    {
                        _tombstones.Remove(dot);
                    }
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
                    Items = _delta,
                    VersionVectors = _versionVectors,
                    Tombstones = _tombstones
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

        public IReadOnlyCollection<TItem> Value
        {
            get
            {
                if (!_semaphore.Wait(TimeSpan.FromSeconds(15)))
                {
                    throw new NyrisException("Deadlock");
                }

                try
                {
                    return _items.Select(i => i.Value).ToList().AsReadOnly();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        public virtual async Task AddAsync(TItem item)
        {
            if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(15)))
            {
                throw new NyrisException("Deadlock");
            }

            try
            {
                _versionVectors.TryGetValue(_thisNodeId, out var version);

                version += 1;

                var newItem = new DottedItem<TActorId, TItem>(new Dot<TActorId>(_thisNodeId, version), item);

                _items.Add(newItem);
                _delta.Add(newItem);

                // notice that i.Actor.Equals(_thisNodeId) means that there may be multiple copies of item
                // stored at the same time. This is by design
                _items.RemoveWhere(i => i.Value.Equals(item) && i.Dot.Version < version);
                _versionVectors[_thisNodeId] = version;
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
                var itemsToBeRemoved = _items.Where(i => condition(i.Value)).ToHashSet();

                if (itemsToBeRemoved.Count > 0)
                {
                    _items.ExceptWith(itemsToBeRemoved);
                    _delta.ExceptWith(itemsToBeRemoved);

                    foreach (var dottedItem in itemsToBeRemoved)
                    {
                        if (_tombstones.TryGetValue(dottedItem.Dot, out var observedByActors))
                        {
                            observedByActors.Add(_thisNodeId);
                        }
                        else
                        {
                            _tombstones.Add(dottedItem.Dot, new HashSet<TActorId> { _thisNodeId });
                        }
                    }
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
            public abstract Dictionary<TActorId, uint>? VersionVectors { get; set; }
            public abstract Dictionary<Dot<TActorId>, HashSet<TActorId>>? Tombstones { get; set; }
            public abstract NodeId? SourceId { get; set; }
        }
    }
}
